using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Procurement.RequestForQuotation;

public class SupplierQuotationService : ISupplierQuotationService
{
    private readonly DomainDbContext _dbContext;
    private readonly IRfqTokenService _tokenService;
    private readonly IExchangeRateService? _exchangeRateService;

    public SupplierQuotationService(
        DomainDbContext dbContext,
        IRfqTokenService tokenService,
        IExchangeRateService? exchangeRateService = null)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _exchangeRateService = exchangeRateService;
    }

    public async Task<PublicRfqDetailsDto?> GetPublicRfqDetailsByTokenAsync(string token, CancellationToken ct = default)
    {
        var rfqSupplier = await _tokenService.ValidateTokenAsync(token, ct);
        if (rfqSupplier == null || rfqSupplier.RequestForQuotation == null)
            return null;

        var rfq = rfqSupplier.RequestForQuotation;

        if (rfq.Status is RequestForQuotationStatus.Cancelled or RequestForQuotationStatus.Closed or RequestForQuotationStatus.Awarded)
        {
            throw new InvalidOperationException(
                $"This RFQ is currently '{rfq.Status}' and is no longer accepting quotations.");
        }

        // Check if quotation already submitted
        var alreadySubmitted = await _dbContext.SupplierQuotations
            .AnyAsync(q => q.RFQId == rfq.Id && q.SupplierId == rfqSupplier.SupplierId, ct);

        var currency = await GetDefaultCurrencyAsync(rfq.TenantId, rfq.CompanyId, ct);

        // Opening the invitation records the supplier response state (Step 3 progress).
        if (rfqSupplier.InvitationStatus == RFQSupplierInvitationStatus.Sent)
        {
            rfqSupplier.InvitationStatus = RFQSupplierInvitationStatus.Opened;
            await _dbContext.SaveChangesAsync(ct);
        }

        // Map public details - ONLY for this supplier, NO competitor quotes, NO target prices!
        return new PublicRfqDetailsDto
        {
            RFQId = rfq.Id,
            RFQNumber = rfq.RFQNumber,
            Title = rfq.Title,
            ClosingDate = rfq.ClosingDate,
            DeliveryRequiredDate = rfq.DeliveryRequiredDate,
            DeliveryTerms = rfq.DeliveryTerms,
            Instructions = rfq.Instructions,
            SupplierName = rfqSupplier.SupplierName,
            SupplierCode = rfqSupplier.SupplierCode,
            AlreadySubmitted = alreadySubmitted,
            CurrencyId = currency?.CurrencyId ?? Guid.Empty,
            CurrencyCode = currency?.CurrencyCode ?? string.Empty,
            Lines = rfq.Lines.OrderBy(l => l.LineNumber).Select(l => new PublicRfqLineDto
            {
                RFQLineId = l.Id,
                LineNumber = l.LineNumber,
                ProductId = l.ProductId,
                ProductCode = l.ProductCode,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                UnitOfMeasureId = l.UnitOfMeasureId,
                UnitOfMeasureCode = l.UnitOfMeasureCode,
                RequiredDate = l.RequiredDate,
                TechnicalSpecifications = l.TechnicalSpecifications
            }).ToList()
        };
    }

    public async Task<SubmitQuotationResultDto> SubmitPublicQuotationAsync(
        string token,
        SubmitPublicQuotationRequest request,
        CancellationToken ct = default)
    {
        var rfqSupplier = await _tokenService.ValidateTokenAsync(token, ct);
        if (rfqSupplier == null || rfqSupplier.RequestForQuotation == null)
            return new SubmitQuotationResultDto { Success = false, Message = "Invalid or expired submission token." };

        var rfq = rfqSupplier.RequestForQuotation;

        if (rfq.Status is RequestForQuotationStatus.Cancelled or RequestForQuotationStatus.Closed or RequestForQuotationStatus.Awarded)
            return new SubmitQuotationResultDto { Success = false, Message = $"RFQ is currently '{rfq.Status}' and cannot accept submissions." };

        if (request.Lines == null || request.Lines.Count == 0)
            return new SubmitQuotationResultDto { Success = false, Message = "Quotation must contain at least one line item." };

        var quotationDate = request.QuotationDate == default ? DateTime.UtcNow : request.QuotationDate;

        if (request.ValidUntil == default)
            return new SubmitQuotationResultDto { Success = false, Message = "Quotation validity date is required." };

        if (request.ValidUntil.Date < DateTime.UtcNow.Date)
            return new SubmitQuotationResultDto { Success = false, Message = "Quotation validity date cannot be in the past." };

        if (request.ValidUntil.Date < quotationDate.Date)
            return new SubmitQuotationResultDto { Success = false, Message = "Quotation validity date cannot be before the quotation date." };

        var alreadySubmitted = await _dbContext.SupplierQuotations
            .AnyAsync(q => q.TenantId == rfq.TenantId && q.RFQId == rfq.Id && q.SupplierId == rfqSupplier.SupplierId, ct);

        if (alreadySubmitted)
            return new SubmitQuotationResultDto { Success = false, Message = "A quotation has already been submitted for this invitation." };

        Currency? currency;
        if (request.CurrencyId.HasValue && request.CurrencyId.Value != Guid.Empty)
        {
            currency = await _dbContext.Currencies
                .FirstOrDefaultAsync(c => c.TenantId == rfq.TenantId && c.CurrencyId == request.CurrencyId.Value, ct);

            if (currency == null)
                return new SubmitQuotationResultDto { Success = false, Message = "Specified currency not found." };
        }
        else
        {
            currency = await GetDefaultCurrencyAsync(rfq.TenantId, rfq.CompanyId, ct);

            if (currency == null)
                return new SubmitQuotationResultDto { Success = false, Message = "No default currency is configured for this company. Please contact the procurement officer." };
        }

        // Validate every submitted line against the RFQ before persisting anything.
        var seenRfqLineIds = new HashSet<Guid>();
        foreach (var lineReq in request.Lines)
        {
            if (lineReq.RFQLineId == Guid.Empty || !seenRfqLineIds.Add(lineReq.RFQLineId))
                return new SubmitQuotationResultDto { Success = false, Message = "Each requested item must be quoted exactly once." };

            var rfqLine = rfq.Lines.FirstOrDefault(l => l.Id == lineReq.RFQLineId);
            if (rfqLine == null)
                return new SubmitQuotationResultDto { Success = false, Message = "The quotation contains an item that is not part of this RFQ." };

            var validationError = ValidateLineValues(
                lineReq.QuotedQuantity,
                lineReq.UnitPrice,
                lineReq.DiscountPercentage,
                lineReq.TaxRate,
                lineReq.LeadTimeDays);

            if (validationError != null)
                return new SubmitQuotationResultDto { Success = false, Message = $"{rfqLine.ProductCode}: {validationError}" };

            if (lineReq.QuotedQuantity > rfqLine.Quantity)
                return new SubmitQuotationResultDto { Success = false, Message = $"{rfqLine.ProductCode}: quoted quantity cannot exceed the requested quantity of {rfqLine.Quantity}." };

            if (lineReq.UnitOfMeasureId != Guid.Empty && lineReq.UnitOfMeasureId != rfqLine.UnitOfMeasureId)
                return new SubmitQuotationResultDto { Success = false, Message = $"{rfqLine.ProductCode}: unit of measure does not match the requested item." };

            if ((lineReq.TechnicalRemarks?.Length ?? 0) > 1000)
                return new SubmitQuotationResultDto { Success = false, Message = $"{rfqLine.ProductCode}: remarks cannot exceed 1000 characters." };
        }

        // Resolve company base currency for exchange rate / base totals.
        var company = await _dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == rfq.TenantId && c.Id == rfq.CompanyId, ct);

        decimal exchangeRate = 1.0m;
        if (company?.BaseCurrencyId.HasValue == true && company.BaseCurrencyId.Value != currency.CurrencyId && _exchangeRateService != null)
        {
            try
            {
                var resolved = await _exchangeRateService.ResolveAsync(
                    rfq.TenantId,
                    currency.CurrencyId,
                    company.BaseCurrencyId.Value,
                    quotationDate);
                exchangeRate = resolved.Rate > 0 ? resolved.Rate : 1.0m;
            }
            catch
            {
                exchangeRate = 1.0m;
            }
        }

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = rfq.TenantId,
            CompanyId = rfq.CompanyId,
            BranchId = rfq.BranchId,
            RFQId = rfq.Id,
            SupplierId = rfqSupplier.SupplierId,
            SupplierCode = rfqSupplier.SupplierCode,
            SupplierName = rfqSupplier.SupplierName,
            QuotationReference = string.IsNullOrWhiteSpace(request.QuotationReference)
                ? $"QT-{rfqSupplier.SupplierCode}-{DateTime.UtcNow:yyyyMMddHHmm}"
                : request.QuotationReference.Trim(),
            QuotationDate = quotationDate,
            ValidUntil = request.ValidUntil,
            CurrencyId = currency.CurrencyId,
            CurrencyCode = currency.CurrencyCode,
            ExchangeRate = exchangeRate,
            Status = SupplierQuotationStatus.Submitted,
            Notes = request.Notes?.Trim(),
            IsManualEntry = false
        };

        decimal subTotal = 0m;
        decimal totalDiscount = 0m;
        decimal totalTax = 0m;

        foreach (var lineReq in request.Lines)
        {
            var rfqLine = rfq.Lines.FirstOrDefault(l => l.Id == lineReq.RFQLineId);
            if (rfqLine == null)
                continue; // already validated above

            var lineGross = RoundMoney(lineReq.QuotedQuantity * lineReq.UnitPrice);
            var lineDiscount = RoundMoney(lineGross * (lineReq.DiscountPercentage / 100m));
            var lineNet = lineGross - lineDiscount;
            var lineTax = RoundMoney(lineNet * (lineReq.TaxRate / 100m));
            var lineTotal = RoundMoney(lineNet + lineTax);

            subTotal += lineGross;
            totalDiscount += lineDiscount;
            totalTax += lineTax;

            quote.Lines.Add(new SupplierQuotationLine
            {
                Id = Guid.NewGuid(),
                TenantId = rfq.TenantId,
                CompanyId = rfq.CompanyId,
                SupplierQuotationId = quote.Id,
                RFQLineId = rfqLine.Id,
                ProductId = rfqLine.ProductId,
                ProductCode = rfqLine.ProductCode,
                ProductName = rfqLine.ProductName,
                QuotedQuantity = lineReq.QuotedQuantity,
                UnitOfMeasureId = rfqLine.UnitOfMeasureId,
                UnitOfMeasureCode = rfqLine.UnitOfMeasureCode,
                UnitPrice = lineReq.UnitPrice,
                DiscountPercentage = lineReq.DiscountPercentage,
                TaxCodeId = lineReq.TaxCodeId,
                TaxRate = lineReq.TaxRate,
                LineTotal = lineTotal,
                LeadTimeDays = lineReq.LeadTimeDays,
                TechnicalRemarks = lineReq.TechnicalRemarks?.Trim()
            });
        }

        quote.SubTotal = RoundMoney(subTotal);
        quote.DiscountAmount = RoundMoney(totalDiscount);
        quote.TaxAmount = RoundMoney(totalTax);
        quote.TotalAmount = RoundMoney(subTotal - totalDiscount + totalTax);
        quote.BaseTotalAmount = RoundMoney(quote.TotalAmount * exchangeRate);

        await _dbContext.SupplierQuotations.AddAsync(quote, ct);
        _dbContext.Entry(quote).Property(x => x.CreatedBy).CurrentValue = rfqSupplier.SupplierId;

        // Invalidate token and record response
        rfqSupplier.ResponseReceivedAt = DateTime.UtcNow;
        rfqSupplier.InvitationStatus = RFQSupplierInvitationStatus.Submitted;
        rfqSupplier.TokenExpiresAt = DateTime.UtcNow; // Token invalidated

        await _dbContext.SaveChangesAsync(ct);

        return new SubmitQuotationResultDto
        {
            Success = true,
            Message = "Quotation submitted successfully.",
            QuotationId = quote.Id,
            QuotationReference = quote.QuotationReference
        };
    }

    public async Task<SupplierQuotationDto> RecordManualQuotationAsync(
        Guid rfqId,
        RecordManualQuotationRequest request,
        Guid userId,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        if (rfq.Status is RequestForQuotationStatus.Cancelled or RequestForQuotationStatus.Closed or RequestForQuotationStatus.Awarded)
            throw new InvalidOperationException($"Cannot record a quotation for an RFQ in '{rfq.Status}' status.");

        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("Quotation must contain at least one line item.", nameof(request.Lines));

        var quotationDate = request.QuotationDate == default ? DateTime.UtcNow : request.QuotationDate;

        if (request.ValidUntil == default)
            throw new ArgumentException("Quotation validity date is required.", nameof(request.ValidUntil));

        if (request.ValidUntil.Date < DateTime.UtcNow.Date)
            throw new ArgumentException("Quotation validity date cannot be in the past.", nameof(request.ValidUntil));

        if (request.ValidUntil.Date < quotationDate.Date)
            throw new ArgumentException("Quotation validity date cannot be before the quotation date.", nameof(request.ValidUntil));

        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SupplierId == request.SupplierId, ct);

        if (supplier == null)
            throw new InvalidOperationException($"Supplier with ID {request.SupplierId} not found.");

        Currency? currency;
        if (request.CurrencyId != Guid.Empty)
        {
            currency = await _dbContext.Currencies
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyId == request.CurrencyId, ct);

            if (currency == null)
                throw new InvalidOperationException($"Currency with ID {request.CurrencyId} not found.");
        }
        else
        {
            currency = await GetDefaultCurrencyAsync(tenantId, companyId, ct);

            if (currency == null)
                throw new InvalidOperationException("No default currency is configured for this company.");
        }

        var company = await _dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId, ct);

        var isBaseCurrency = company?.BaseCurrencyId == currency.CurrencyId;
        var exchangeRate = isBaseCurrency ? 1.0m : (request.ExchangeRate > 0 ? request.ExchangeRate : 1.0m);

        var quote = new SupplierQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQId = rfq.Id,
            SupplierId = supplier.SupplierId,
            SupplierCode = supplier.SupplierCode,
            SupplierName = supplier.SupplierName,
            QuotationReference = string.IsNullOrWhiteSpace(request.QuotationReference)
                ? $"QT-{supplier.SupplierCode}-{DateTime.UtcNow:yyyyMMdd}"
                : request.QuotationReference.Trim(),
            QuotationDate = quotationDate,
            ValidUntil = request.ValidUntil,
            CurrencyId = currency.CurrencyId,
            CurrencyCode = currency.CurrencyCode,
            ExchangeRate = exchangeRate,
            Status = SupplierQuotationStatus.Submitted,
            Notes = request.Notes?.Trim(),
            IsManualEntry = true
        };

        decimal subTotal = 0m;
        decimal totalDiscount = 0m;
        decimal totalTax = 0m;

        var seenRfqLineIds = new HashSet<Guid>();
        var seenProductIds = new HashSet<Guid>();

        foreach (var lineReq in request.Lines)
        {
            var product = await _dbContext.Products
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == lineReq.ProductId, ct);

            if (product == null)
                throw new InvalidOperationException($"Product with ID {lineReq.ProductId} not found.");

            var rfqLine = lineReq.RFQLineId.HasValue
                ? rfq.Lines.FirstOrDefault(l => l.Id == lineReq.RFQLineId.Value)
                : rfq.Lines.FirstOrDefault(l => l.ProductId == product.ProductId);

            if (rfqLine == null)
                throw new InvalidOperationException($"Product '{product.ProductCode}' is not part of RFQ {rfq.RFQNumber}.");

            if (!seenRfqLineIds.Add(rfqLine.Id) || !seenProductIds.Add(product.ProductId))
                throw new InvalidOperationException($"Product '{product.ProductCode}' is quoted more than once.");

            var validationError = ValidateLineValues(
                lineReq.QuotedQuantity,
                lineReq.UnitPrice,
                lineReq.DiscountPercentage,
                lineReq.TaxRate,
                lineReq.LeadTimeDays);

            if (validationError != null)
                throw new InvalidOperationException($"{product.ProductCode}: {validationError}");

            if (lineReq.QuotedQuantity > rfqLine.Quantity)
                throw new InvalidOperationException($"{product.ProductCode}: quoted quantity cannot exceed the requested quantity of {rfqLine.Quantity}.");

            var lineGross = RoundMoney(lineReq.QuotedQuantity * lineReq.UnitPrice);
            var lineDiscount = RoundMoney(lineGross * (lineReq.DiscountPercentage / 100m));
            var lineNet = lineGross - lineDiscount;
            var lineTax = RoundMoney(lineNet * (lineReq.TaxRate / 100m));
            var lineTotal = RoundMoney(lineNet + lineTax);

            subTotal += lineGross;
            totalDiscount += lineDiscount;
            totalTax += lineTax;

            quote.Lines.Add(new SupplierQuotationLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyId = companyId,
                SupplierQuotationId = quote.Id,
                RFQLineId = rfqLine.Id,
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                QuotedQuantity = lineReq.QuotedQuantity,
                UnitOfMeasureId = rfqLine.UnitOfMeasureId,
                UnitOfMeasureCode = rfqLine.UnitOfMeasureCode,
                UnitPrice = lineReq.UnitPrice,
                DiscountPercentage = lineReq.DiscountPercentage,
                TaxCodeId = lineReq.TaxCodeId,
                TaxRate = lineReq.TaxRate,
                LineTotal = lineTotal,
                LeadTimeDays = lineReq.LeadTimeDays,
                TechnicalRemarks = lineReq.TechnicalRemarks?.Trim()
            });
        }

        quote.SubTotal = RoundMoney(subTotal);
        quote.DiscountAmount = RoundMoney(totalDiscount);
        quote.TaxAmount = RoundMoney(totalTax);
        quote.TotalAmount = RoundMoney(subTotal - totalDiscount + totalTax);
        quote.BaseTotalAmount = RoundMoney(quote.TotalAmount * exchangeRate);

        await _dbContext.SupplierQuotations.AddAsync(quote, ct);
        _dbContext.Entry(quote).Property(x => x.CreatedBy).CurrentValue = userId;

        // Update RFQSupplier record if exists
        var rfqSupplier = await _dbContext.RFQSuppliers
            .FirstOrDefaultAsync(s => s.RFQId == rfq.Id && s.SupplierId == supplier.SupplierId, ct);

        if (rfqSupplier != null)
        {
            rfqSupplier.InvitationStatus = RFQSupplierInvitationStatus.Submitted;
            rfqSupplier.ResponseReceivedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        return MapToQuotationDto(quote);
    }

    public async Task<IReadOnlyList<SupplierQuotationDto>> GetQuotationsForRfqAsync(Guid rfqId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default)
    {
        var rfqExists = await _dbContext.RequestForQuotations
            .AsNoTracking()
            .AnyAsync(r => r.TenantId == tenantId && r.CompanyId == companyId && r.BranchId == branchId && r.Id == rfqId, ct);

        if (!rfqExists)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        var quotes = await _dbContext.SupplierQuotations
            .AsNoTracking()
            .Include(q => q.Lines)
            .Where(q => q.TenantId == tenantId && q.CompanyId == companyId && q.BranchId == branchId && q.RFQId == rfqId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);

        return quotes.Select(MapToQuotationDto).ToList();
    }

    public async Task<SupplierQuotationDto?> GetQuotationByIdAsync(Guid rfqId, Guid quotationId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default)
    {
        var quote = await _dbContext.SupplierQuotations
            .AsNoTracking()
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q =>
                q.TenantId == tenantId &&
                q.CompanyId == companyId &&
                q.BranchId == branchId &&
                q.RFQId == rfqId &&
                q.Id == quotationId, ct);

        return quote == null ? null : MapToQuotationDto(quote);
    }

    public async Task<RfqComparisonMatrixDto> GetComparisonMatrixAsync(Guid rfqId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .AsNoTracking()
            .Include(r => r.Lines)
            .Include(r => r.Quotations)
                .ThenInclude(q => q.Lines)
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        var baseCurrency = await GetDefaultCurrencyAsync(tenantId, rfq.CompanyId, ct);
        var baseCurrencyCode = baseCurrency?.CurrencyCode ?? "LKR";

        var matrix = new RfqComparisonMatrixDto
        {
            RFQId = rfq.Id,
            RFQNumber = rfq.RFQNumber,
            Title = rfq.Title,
            BaseCurrencyCode = baseCurrencyCode,
            Suppliers = rfq.Quotations
                .OrderBy(q => q.BaseTotalAmount)
                .Select(q => new RfqComparisonSupplierBidDto
                {
                    QuotationId = q.Id,
                    SupplierId = q.SupplierId,
                    SupplierCode = q.SupplierCode,
                    SupplierName = q.SupplierName,
                    QuotationReference = q.QuotationReference,
                    QuotationDate = q.QuotationDate,
                    ValidUntil = q.ValidUntil,
                    CurrencyCode = q.CurrencyCode,
                    ExchangeRate = q.ExchangeRate,
                    TotalAmount = q.TotalAmount,
                    BaseTotalAmount = q.BaseTotalAmount,
                    Status = q.Status,
                    IsManualEntry = q.IsManualEntry
                }).ToList()
        };

        foreach (var line in rfq.Lines.OrderBy(l => l.LineNumber))
        {
            var compLine = new RfqComparisonLineDto
            {
                RFQLineId = line.Id,
                LineNumber = line.LineNumber,
                ProductCode = line.ProductCode,
                ProductName = line.ProductName,
                RequestedQuantity = line.Quantity,
                RequestedUomCode = line.UnitOfMeasureCode,
                TargetUnitPrice = line.TargetUnitPrice
            };

            foreach (var quote in rfq.Quotations)
            {
                var quoteLine = quote.Lines.FirstOrDefault(ql => ql.RFQLineId == line.Id || ql.ProductId == line.ProductId);
                if (quoteLine != null)
                {
                    var baseUnitPrice = quoteLine.UnitPrice * quote.ExchangeRate;
                    var baseLineTotal = quoteLine.LineTotal * quote.ExchangeRate;

                    compLine.Bids.Add(new RfqComparisonLineBidDto
                    {
                        QuotationId = quote.Id,
                        SupplierId = quote.SupplierId,
                        QuotedQuantity = quoteLine.QuotedQuantity,
                        QuotedUomCode = quoteLine.UnitOfMeasureCode,
                        UnitPrice = quoteLine.UnitPrice,
                        BaseUnitPrice = baseUnitPrice,
                        DiscountPercentage = quoteLine.DiscountPercentage,
                        LineTotal = quoteLine.LineTotal,
                        BaseLineTotal = baseLineTotal,
                        LeadTimeDays = quoteLine.LeadTimeDays,
                        TechnicalRemarks = quoteLine.TechnicalRemarks
                    });
                }
            }

            matrix.Lines.Add(compLine);
        }

        return matrix;
    }

    public async Task<SupplierQuotationDto> ApproveQuotationAsync(
        Guid rfqId,
        Guid quotationId,
        ApproveQuotationRequest request,
        Guid userId,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Quotations)
                .ThenInclude(q => q.Lines)
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        var quote = rfq.Quotations.FirstOrDefault(q => q.Id == quotationId);
        if (quote == null)
            throw new KeyNotFoundException($"Quotation with ID {quotationId} not found on RFQ {rfq.RFQNumber}.");

        // Rule 1: Quotation Validity Check at Approval Time
        if (DateTime.UtcNow.Date > quote.ValidUntil.Date)
        {
            throw new InvalidOperationException(
                $"Cannot approve quotation '{quote.QuotationReference}'. " +
                $"The quotation expired on {quote.ValidUntil:yyyy-MM-dd}. An expired quotation cannot be approved.");
        }

        // Rule 2: Self-Approval Guard (Creator cannot approve own RFQ)
        if (rfq.CreatedBy == userId)
        {
            throw new InvalidOperationException(
                "Procurement separation of duties violation: The user who created the RFQ cannot approve its quotations.");
        }

        quote.Status = SupplierQuotationStatus.Approved;
        quote.ApprovedAt = DateTime.UtcNow;
        quote.ApprovedByUserId = userId;
        quote.ApprovalRemarks = request.Remarks?.Trim();

        // Transition RFQ status to Awarded
        rfq.Status = RequestForQuotationStatus.Awarded;

        await _dbContext.SaveChangesAsync(ct);
        return MapToQuotationDto(quote);
    }

    public async Task<SupplierQuotationDto> RejectQuotationAsync(
        Guid rfqId,
        Guid quotationId,
        RejectQuotationRequest request,
        Guid userId,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CancellationToken ct = default)
    {
        var quote = await _dbContext.SupplierQuotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q =>
                q.TenantId == tenantId &&
                q.CompanyId == companyId &&
                q.BranchId == branchId &&
                q.RFQId == rfqId &&
                q.Id == quotationId, ct);

        if (quote == null)
            throw new KeyNotFoundException($"Quotation with ID {quotationId} not found.");

        quote.Status = SupplierQuotationStatus.Rejected;
        quote.RejectedAt = DateTime.UtcNow;
        quote.RejectedByUserId = userId;
        quote.RejectionRemarks = request.Remarks?.Trim();

        await _dbContext.SaveChangesAsync(ct);
        return MapToQuotationDto(quote);
    }

    private async Task<Currency?> GetDefaultCurrencyAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        var company = await _dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId, ct);

        if (company?.BaseCurrencyId.HasValue == true)
        {
            var baseCurrency = await _dbContext.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.TenantId == tenantId &&
                    c.CurrencyId == company.BaseCurrencyId.Value &&
                    c.IsActive, ct);

            if (baseCurrency != null)
                return baseCurrency;
        }

        return await _dbContext.Currencies
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.CurrencyCode)
            .FirstOrDefaultAsync(ct);
    }

    private static string? ValidateLineValues(
        decimal quotedQuantity,
        decimal unitPrice,
        decimal discountPercentage,
        decimal taxRate,
        int? leadTimeDays)
    {
        if (quotedQuantity <= 0)
            return "quoted quantity must be greater than zero.";

        if (unitPrice <= 0)
            return "unit price must be greater than zero.";

        if (discountPercentage < 0 || discountPercentage > 100)
            return "discount percentage must be between 0 and 100.";

        if (taxRate < 0 || taxRate > 100)
            return "tax rate must be between 0 and 100.";

        if (leadTimeDays is < 0 or > 365)
            return "lead time must be between 0 and 365 days.";

        return null;
    }

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static SupplierQuotationDto MapToQuotationDto(SupplierQuotation q)
    {
        return new SupplierQuotationDto
        {
            Id = q.Id,
            RFQId = q.RFQId,
            SupplierId = q.SupplierId,
            SupplierCode = q.SupplierCode,
            SupplierName = q.SupplierName,
            QuotationReference = q.QuotationReference,
            QuotationDate = q.QuotationDate,
            ValidUntil = q.ValidUntil,
            CurrencyId = q.CurrencyId,
            CurrencyCode = q.CurrencyCode,
            ExchangeRate = q.ExchangeRate,
            SubTotal = q.SubTotal,
            DiscountAmount = q.DiscountAmount,
            TaxAmount = q.TaxAmount,
            TotalAmount = q.TotalAmount,
            BaseTotalAmount = q.BaseTotalAmount,
            Status = q.Status,
            Notes = q.Notes,
            IsManualEntry = q.IsManualEntry,
            ApprovedAt = q.ApprovedAt,
            ApprovedByUserId = q.ApprovedByUserId,
            ApprovalRemarks = q.ApprovalRemarks,
            RejectedAt = q.RejectedAt,
            RejectedByUserId = q.RejectedByUserId,
            RejectionRemarks = q.RejectionRemarks,
            Lines = q.Lines.Select(ql => new SupplierQuotationLineDto
            {
                Id = ql.Id,
                RFQLineId = ql.RFQLineId,
                ProductId = ql.ProductId,
                ProductCode = ql.ProductCode,
                ProductName = ql.ProductName,
                QuotedQuantity = ql.QuotedQuantity,
                UnitOfMeasureId = ql.UnitOfMeasureId,
                UnitOfMeasureCode = ql.UnitOfMeasureCode,
                UnitPrice = ql.UnitPrice,
                DiscountPercentage = ql.DiscountPercentage,
                TaxCodeId = ql.TaxCodeId,
                TaxRate = ql.TaxRate,
                LineTotal = ql.LineTotal,
                LeadTimeDays = ql.LeadTimeDays,
                TechnicalRemarks = ql.TechnicalRemarks
            }).ToList()
        };
    }
}
