using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using Domain.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domain.Features.Procurement.RequestForQuotation;

public class SupplierQuotationService : ISupplierQuotationService
{
    private readonly DomainDbContext _dbContext;
    private readonly IRfqTokenService _tokenService;
    private readonly IExchangeRateService? _exchangeRateService;
    private readonly IRfqEmailNotificationService? _emailNotificationService;
    private readonly ILogger<SupplierQuotationService>? _logger;

    public SupplierQuotationService(
        DomainDbContext dbContext,
        IRfqTokenService tokenService,
        IExchangeRateService? exchangeRateService = null,
        IRfqEmailNotificationService? emailNotificationService = null,
        ILogger<SupplierQuotationService>? logger = null)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _exchangeRateService = exchangeRateService;
        _emailNotificationService = emailNotificationService;
        _logger = logger;
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
        var company = await _dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == rfq.TenantId && c.Id == rfq.CompanyId, ct)
            ?? await _dbContext.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == rfq.TenantId, ct)
            ?? await _dbContext.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == rfq.CompanyId, ct)
            ?? await _dbContext.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IsActive, ct);

        var companyName = !string.IsNullOrWhiteSpace(company?.Name)
            ? company.Name
            : "ApexERP Purchasing Organization";

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
            CompanyName = companyName,
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

        var approvedQuoteIds = quotes.Where(q => q.Status == SupplierQuotationStatus.Approved).Select(q => q.Id.ToString()).ToList();
        var audits = approvedQuoteIds.Count > 0
            ? await _dbContext.DomainAuditRecords
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.EntityName == "RfqAwardNotification" && approvedQuoteIds.Contains(a.EntityId))
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync(ct)
            : new List<DomainAuditRecord>();

        return quotes.Select(q =>
        {
            var audit = audits.FirstOrDefault(a => a.EntityId == q.Id.ToString());
            var outcome = audit != null ? ParseAwardAudit(audit) : null;
            return MapToQuotationDto(q, outcome?.Status, outcome?.SentAt, outcome?.FailureReason, outcome?.RecipientEmail);
        }).ToList();
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

        if (quote == null) return null;

        var audit = quote.Status == SupplierQuotationStatus.Approved
            ? await _dbContext.DomainAuditRecords
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.EntityName == "RfqAwardNotification" && a.EntityId == quote.Id.ToString())
                .OrderByDescending(a => a.Timestamp)
                .FirstOrDefaultAsync(ct)
            : null;

        var outcome = audit != null ? ParseAwardAudit(audit) : null;
        return MapToQuotationDto(quote, outcome?.Status, outcome?.SentAt, outcome?.FailureReason, outcome?.RecipientEmail);
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
        var baseCurrencyId = baseCurrency?.CurrencyId ?? Guid.Empty;

        // Exclude Draft, Withdrawn, and Rejected quotations from active competing bids.
        var eligibleQuotations = rfq.Quotations
            .Where(q => q.Status is SupplierQuotationStatus.Submitted
                     or SupplierQuotationStatus.UnderReview
                     or SupplierQuotationStatus.Approved)
            .ToList();

        // Resolve exchange rates for each eligible quotation
        var supplierBidDtos = new List<RfqComparisonSupplierBidDto>();
        var quotationRates = new Dictionary<Guid, (decimal Rate, bool HasValidRate)>();

        foreach (var q in eligibleQuotations)
        {
            var isSameCurrency = (baseCurrencyId != Guid.Empty && q.CurrencyId == baseCurrencyId)
                || string.Equals(q.CurrencyCode, baseCurrencyCode, StringComparison.OrdinalIgnoreCase);

            decimal rate = 1.0m;
            bool hasValidRate = true;
            string? conversionError = null;

            if (isSameCurrency)
            {
                rate = 1.0m;
                hasValidRate = true;
            }
            else
            {
                if (q.ExchangeRate > 0 && q.ExchangeRate != 1.0m)
                {
                    rate = q.ExchangeRate;
                    hasValidRate = true;
                }
                else if (_exchangeRateService != null && baseCurrencyId != Guid.Empty && q.CurrencyId != Guid.Empty)
                {
                    try
                    {
                        var resolved = await _exchangeRateService.ResolveAsync(
                            tenantId,
                            q.CurrencyId,
                            baseCurrencyId,
                            q.QuotationDate);
                        if (resolved.Rate > 0)
                        {
                            rate = resolved.Rate;
                            hasValidRate = true;
                        }
                        else
                        {
                            hasValidRate = false;
                            conversionError = $"Exchange rate for {q.CurrencyCode} to {baseCurrencyCode} is not available.";
                            rate = 0m;
                        }
                    }
                    catch
                    {
                        hasValidRate = false;
                        conversionError = $"Exchange rate for {q.CurrencyCode} to {baseCurrencyCode} could not be resolved.";
                        rate = 0m;
                    }
                }
                else if (q.ExchangeRate > 0)
                {
                    // Rate is explicitly recorded on quotation
                    rate = q.ExchangeRate;
                    hasValidRate = true;
                }
                else
                {
                    hasValidRate = false;
                    conversionError = $"Missing exchange rate for {q.CurrencyCode} to {baseCurrencyCode}.";
                    rate = 0m;
                }
            }

            quotationRates[q.Id] = (rate, hasValidRate);

            decimal baseTotal = hasValidRate ? RoundMoney(q.TotalAmount * rate) : 0m;
            decimal baseSubTotal = hasValidRate ? RoundMoney(q.SubTotal * rate) : 0m;
            decimal baseDiscount = hasValidRate ? RoundMoney(q.DiscountAmount * rate) : 0m;
            decimal baseTax = hasValidRate ? RoundMoney(q.TaxAmount * rate) : 0m;

            var lineLeadTimes = q.Lines
                .Where(l => l.LeadTimeDays.HasValue)
                .Select(l => l.LeadTimeDays!.Value)
                .ToList();
            int? maxLeadTime = lineLeadTimes.Count > 0 ? lineLeadTimes.Max() : null;
            int? minLeadTime = lineLeadTimes.Count > 0 ? lineLeadTimes.Min() : null;

            supplierBidDtos.Add(new RfqComparisonSupplierBidDto
            {
                QuotationId = q.Id,
                SupplierId = q.SupplierId,
                SupplierCode = q.SupplierCode,
                SupplierName = q.SupplierName,
                QuotationReference = q.QuotationReference,
                QuotationDate = q.QuotationDate,
                SubmissionDate = q.CreatedAt,
                ValidUntil = q.ValidUntil,
                CurrencyCode = q.CurrencyCode,
                ExchangeRate = rate,
                HasValidExchangeRate = hasValidRate,
                CurrencyConversionError = conversionError,
                SubTotal = q.SubTotal,
                BaseSubTotal = baseSubTotal,
                DiscountAmount = q.DiscountAmount,
                BaseDiscountAmount = baseDiscount,
                TaxAmount = q.TaxAmount,
                BaseTaxAmount = baseTax,
                TotalAmount = q.TotalAmount,
                BaseTotalAmount = baseTotal,
                Notes = q.Notes,
                MaxLeadTimeDays = maxLeadTime,
                MinLeadTimeDays = minLeadTime,
                Status = q.Status,
                IsManualEntry = q.IsManualEntry
            });
        }

        var matrix = new RfqComparisonMatrixDto
        {
            RFQId = rfq.Id,
            RFQNumber = rfq.RFQNumber,
            Title = rfq.Title,
            BaseCurrencyCode = baseCurrencyCode,
            ClosingDate = rfq.ClosingDate,
            DeliveryRequiredDate = rfq.DeliveryRequiredDate,
            DeliveryTerms = rfq.DeliveryTerms,
            Instructions = rfq.Instructions,
            Suppliers = supplierBidDtos
                .OrderBy(s => s.BaseTotalAmount > 0 ? s.BaseTotalAmount : decimal.MaxValue)
                .ToList()
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

            foreach (var supplier in matrix.Suppliers)
            {
                var quote = eligibleQuotations.FirstOrDefault(q => q.Id == supplier.QuotationId);
                if (quote == null) continue;

                var quoteLine = quote.Lines.FirstOrDefault(ql => ql.RFQLineId == line.Id || ql.ProductId == line.ProductId);
                if (quoteLine != null)
                {
                    var (rate, hasValidRate) = quotationRates.TryGetValue(quote.Id, out var rInfo)
                        ? rInfo
                        : (1.0m, true);

                    var lineGross = RoundMoney(quoteLine.QuotedQuantity * quoteLine.UnitPrice);
                    var lineDiscount = RoundMoney(lineGross * (quoteLine.DiscountPercentage / 100m));
                    var lineNet = lineGross - lineDiscount;
                    var lineTax = RoundMoney(lineNet * (quoteLine.TaxRate / 100m));

                    var baseUnitPrice = hasValidRate ? RoundMoney(quoteLine.UnitPrice * rate) : 0m;
                    var baseLineTotal = hasValidRate ? RoundMoney(quoteLine.LineTotal * rate) : 0m;
                    var baseDiscountAmount = hasValidRate ? RoundMoney(lineDiscount * rate) : 0m;
                    var baseTaxAmount = hasValidRate ? RoundMoney(lineTax * rate) : 0m;

                    compLine.Bids.Add(new RfqComparisonLineBidDto
                    {
                        QuotationId = quote.Id,
                        SupplierId = quote.SupplierId,
                        QuotedQuantity = quoteLine.QuotedQuantity,
                        QuotedUomCode = quoteLine.UnitOfMeasureCode,
                        UnitPrice = quoteLine.UnitPrice,
                        BaseUnitPrice = baseUnitPrice,
                        DiscountPercentage = quoteLine.DiscountPercentage,
                        DiscountAmount = lineDiscount,
                        BaseDiscountAmount = baseDiscountAmount,
                        TaxRate = quoteLine.TaxRate,
                        TaxAmount = lineTax,
                        BaseTaxAmount = baseTaxAmount,
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

        // Guard 1: RFQ Status Check
        if (rfq.Status is RequestForQuotationStatus.Cancelled or RequestForQuotationStatus.Closed)
        {
            throw new InvalidOperationException($"Cannot award quotation on an RFQ in '{rfq.Status}' status.");
        }

        var quote = rfq.Quotations.FirstOrDefault(q => q.Id == quotationId);
        if (quote == null)
            throw new KeyNotFoundException($"Quotation with ID {quotationId} not found on RFQ {rfq.RFQNumber}.");

        // Guard 2: Quotation Status Check (cannot award rejected or draft)
        if (quote.Status == SupplierQuotationStatus.Rejected)
        {
            throw new InvalidOperationException($"Cannot award quotation '{quote.QuotationReference}' because it has been rejected.");
        }

        if (quote.Status == SupplierQuotationStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot award quotation '{quote.QuotationReference}' because it is in Draft status.");
        }

        // Guard 3: Idempotent Handling for Already Approved Quotation
        if (quote.Status == SupplierQuotationStatus.Approved)
        {
            var existingAudit = await _dbContext.DomainAuditRecords
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.EntityName == "RfqAwardNotification" && a.EntityId == quote.Id.ToString())
                .OrderByDescending(a => a.Timestamp)
                .FirstOrDefaultAsync(ct);
            var outcome = existingAudit != null ? ParseAwardAudit(existingAudit) : null;
            return MapToQuotationDto(quote, outcome?.Status, outcome?.SentAt, outcome?.FailureReason, outcome?.RecipientEmail);
        }

        // Guard 4: Quotation Validity Check at Approval Time
        if (DateTime.UtcNow.Date > quote.ValidUntil.Date)
        {
            throw new InvalidOperationException(
                $"Cannot approve quotation '{quote.QuotationReference}'. " +
                $"The quotation expired on {quote.ValidUntil:yyyy-MM-dd}. An expired quotation cannot be approved.");
        }

        // Guard 5: Self-Approval Guard (Creator cannot approve own RFQ)
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

        // Dispatch award notification email to the winning supplier
        var notificationOutcome = await DispatchAwardNotificationInternalAsync(rfq, quote, request, userId, ct);
        return MapToQuotationDto(quote, notificationOutcome.Status, notificationOutcome.SentAt, notificationOutcome.FailureReason, notificationOutcome.RecipientEmail);
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
        var rfq = await _dbContext.RequestForQuotations
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        var quote = await _dbContext.SupplierQuotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q =>
                q.TenantId == tenantId &&
                q.CompanyId == companyId &&
                q.BranchId == branchId &&
                q.RFQId == rfqId &&
                q.Id == quotationId, ct);

        if (quote == null)
            throw new KeyNotFoundException($"Quotation with ID {quotationId} not found on RFQ {rfq.RFQNumber}.");

        if (quote.Status == SupplierQuotationStatus.Approved)
        {
            throw new InvalidOperationException($"Cannot reject quotation '{quote.QuotationReference}' because it has already been approved.");
        }

        quote.Status = SupplierQuotationStatus.Rejected;
        quote.RejectedAt = DateTime.UtcNow;
        quote.RejectedByUserId = userId;
        quote.RejectionRemarks = request.Remarks?.Trim();

        await _dbContext.SaveChangesAsync(ct);
        return MapToQuotationDto(quote);
    }

    public async Task<SupplierQuotationDto> RetryAwardNotificationAsync(
        Guid rfqId,
        Guid quotationId,
        Guid userId,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Quotations)
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.CompanyId == companyId &&
                r.BranchId == branchId &&
                r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        if (rfq.Status != RequestForQuotationStatus.Awarded)
            throw new InvalidOperationException($"Cannot retry award notification for an RFQ in '{rfq.Status}' status. RFQ must be Awarded.");

        var quote = rfq.Quotations.FirstOrDefault(q => q.Id == quotationId);
        if (quote == null)
            throw new KeyNotFoundException($"Quotation with ID {quotationId} not found on RFQ {rfq.RFQNumber}.");

        if (quote.Status != SupplierQuotationStatus.Approved)
            throw new InvalidOperationException($"Cannot send award notification for quotation '{quote.QuotationReference}' because it is in '{quote.Status}' status. Only Approved quotations are eligible.");

        // Check if already sent (prevent accidental duplicate notifications)
        if (_recentlySentAwards.ContainsKey(quote.Id))
        {
            throw new InvalidOperationException($"Award notification has already been successfully sent to '{quote.SupplierName}'. Duplicate notifications are prevented.");
        }

        var latestAudit = await _dbContext.DomainAuditRecords
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EntityName == "RfqAwardNotification" && a.EntityId == quote.Id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync(ct);

        if (latestAudit != null && latestAudit.Action == "AwardNotificationSent")
        {
            _recentlySentAwards.TryAdd(quote.Id, latestAudit.Timestamp);
            throw new InvalidOperationException($"Award notification has already been successfully sent to '{quote.SupplierName}'. Duplicate notifications are prevented.");
        }

        if (latestAudit != null && latestAudit.Action == "AwardNotificationSending" &&
            DateTime.UtcNow - latestAudit.Timestamp < TimeSpan.FromSeconds(30))
        {
            throw new InvalidOperationException("Award notification dispatch is currently in progress. Please wait.");
        }

        var request = new ApproveQuotationRequest
        {
            Remarks = quote.ApprovalRemarks
        };

        var outcome = await DispatchAwardNotificationInternalAsync(rfq, quote, request, userId, ct);
        return MapToQuotationDto(quote, outcome.Status, outcome.SentAt, outcome.FailureReason, outcome.RecipientEmail);
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

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _awardLocks = new();
    private static readonly ConcurrentDictionary<Guid, DateTime> _recentlySentAwards = new();
    private static readonly Regex EmailFormatRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private sealed record AwardNotificationOutcome(string Status, DateTime? SentAt, string? FailureReason, string? RecipientEmail);

    private async Task<(bool IsValid, string? Email, string? ErrorMessage)> ResolveAndValidateSupplierEmailAsync(
        Guid tenantId,
        Guid rfqId,
        Guid supplierId,
        CancellationToken ct)
    {
        // 1. Authoritative registered email from Supplier master record
        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SupplierId == supplierId, ct);

        var email = supplier?.Email?.Trim();

        // 2. Fall back to RFQSupplier.ContactEmail if supplier master email is blank
        if (string.IsNullOrWhiteSpace(email))
        {
            var rfqSupplier = await _dbContext.RFQSuppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.RFQId == rfqId && s.SupplierId == supplierId, ct);
            email = rfqSupplier?.ContactEmail?.Trim();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, null, "Winning supplier has no registered contact email address in the supplier record.");
        }

        if (!EmailFormatRegex.IsMatch(email))
        {
            return (false, email, $"Winning supplier contact email '{email}' has an invalid format.");
        }

        return (true, email, null);
    }

    private async Task<AwardNotificationOutcome> DispatchAwardNotificationInternalAsync(
        RequestForQuotation rfq,
        SupplierQuotation quote,
        ApproveQuotationRequest request,
        Guid userId,
        CancellationToken ct)
    {
        var sem = _awardLocks.GetOrAdd(quote.Id, _ => new SemaphoreSlim(1, 1));
        if (!await sem.WaitAsync(TimeSpan.FromSeconds(5), ct))
        {
            return new AwardNotificationOutcome("Sending", null, "An award notification dispatch is already in progress for this quotation.", null);
        }

        try
        {
            // 1. Check in-memory fast cache first (suppresses duplicate sends even if DB write was delayed)
            if (_recentlySentAwards.TryGetValue(quote.Id, out var cachedSentAt))
            {
                _logger?.LogInformation("Award notification was already dispatched in-memory for quotation {QuotationId}", quote.Id);
                return new AwardNotificationOutcome("Sent", cachedSentAt, null, null);
            }

            // 2. Check DB audit records
            var existingAudit = await _dbContext.DomainAuditRecords
                .AsNoTracking()
                .Where(a => a.TenantId == rfq.TenantId && a.EntityName == "RfqAwardNotification" && a.EntityId == quote.Id.ToString())
                .OrderByDescending(a => a.Timestamp)
                .FirstOrDefaultAsync(ct);

            if (existingAudit != null && existingAudit.Action == "AwardNotificationSent")
            {
                var outcome = ParseAwardAudit(existingAudit);
                if (outcome.SentAt.HasValue)
                {
                    _recentlySentAwards.TryAdd(quote.Id, outcome.SentAt.Value);
                }
                _logger?.LogInformation("Award notification has already been dispatched for quotation {QuotationId}", quote.Id);
                return outcome;
            }

            // 3. Check in-flight dispatch (< 30 seconds old)
            if (existingAudit != null && existingAudit.Action == "AwardNotificationSending" &&
                DateTime.UtcNow - existingAudit.Timestamp < TimeSpan.FromSeconds(30))
            {
                return new AwardNotificationOutcome("Sending", null, "Award notification dispatch is currently in progress.", null);
            }

            // 4. Resolve and validate supplier registered email
            var (isValidEmail, recipientEmail, emailValidationError) = await ResolveAndValidateSupplierEmailAsync(rfq.TenantId, rfq.Id, quote.SupplierId, ct);
            if (!isValidEmail)
            {
                await RecordAwardNotificationAuditAsync(rfq.TenantId, quote.Id, userId, "AwardNotificationFailed", "Failed", recipientEmail, emailValidationError, ct);
                return new AwardNotificationOutcome("Failed", null, emailValidationError, recipientEmail);
            }

            if (_emailNotificationService == null)
            {
                const string failReason = "Email notification service is not configured or unavailable.";
                await RecordAwardNotificationAuditAsync(rfq.TenantId, quote.Id, userId, "AwardNotificationFailed", "Failed", recipientEmail, failReason, ct);
                return new AwardNotificationOutcome("Failed", null, failReason, recipientEmail);
            }

            // 5. Record in-flight intent before calling SMTP transport (mirrors RFQSupplierInvitationStatus.Sending)
            await RecordAwardNotificationAuditAsync(rfq.TenantId, quote.Id, userId, "AwardNotificationSending", "Sending", recipientEmail, null, ct);

            // 6. Resolve buyer company details
            var company = await _dbContext.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == rfq.TenantId && c.Id == rfq.CompanyId, ct);
            var companyName = !string.IsNullOrWhiteSpace(company?.Name) ? company.Name : "ApexERP Procurement";

            var emailContext = new RfqAwardEmailContext
            {
                RecipientEmail = recipientEmail!,
                WinningSupplierName = quote.SupplierName,
                RfqNumber = rfq.RFQNumber,
                RfqTitle = rfq.Title,
                QuotationReference = quote.QuotationReference,
                BuyerCompanyName = companyName,
                AwardDate = quote.ApprovedAt ?? DateTime.UtcNow,
                CustomRemarks = quote.ApprovalRemarks,
                BuyerContactName = companyName,
                BuyerContactEmail = null,
                BuyerContactPhone = null
            };

            RfqEmailDispatchResult dispatch;
            try
            {
                dispatch = await _emailNotificationService.SendAwardNotificationAsync(emailContext, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Failed to send award notification email for RFQ {RfqNumber} to {RecipientEmail}", rfq.RFQNumber, recipientEmail);
                dispatch = new RfqEmailDispatchResult(false, ex.Message);
            }

            if (dispatch.IsSuccess)
            {
                var sentAt = DateTime.UtcNow;
                _recentlySentAwards.TryAdd(quote.Id, sentAt);
                await RecordAwardNotificationAuditAsync(rfq.TenantId, quote.Id, userId, "AwardNotificationSent", "Sent", recipientEmail, null, ct, sentAt);
                return new AwardNotificationOutcome("Sent", sentAt, null, recipientEmail);
            }
            else
            {
                var failReason = !string.IsNullOrWhiteSpace(dispatch.FailureReason)
                    ? dispatch.FailureReason
                    : "SMTP transport rejected message.";
                await RecordAwardNotificationAuditAsync(rfq.TenantId, quote.Id, userId, "AwardNotificationFailed", "Failed", recipientEmail, failReason, ct);
                return new AwardNotificationOutcome("Failed", null, failReason, recipientEmail);
            }
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task RecordAwardNotificationAuditAsync(
        Guid tenantId,
        Guid quoteId,
        Guid userId,
        string action,
        string status,
        string? recipient,
        string? failureReason,
        CancellationToken ct,
        DateTime? sentAt = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            Status = status,
            Recipient = recipient,
            FailureReason = failureReason,
            SentAt = sentAt ?? (status == "Sent" ? DateTime.UtcNow : (DateTime?)null),
            AttemptedAt = DateTime.UtcNow
        });

        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var audit = new DomainAuditRecord
                {
                    AuditId = Guid.NewGuid(),
                    TenantId = tenantId,
                    EntityName = "RfqAwardNotification",
                    EntityId = quoteId.ToString(),
                    Action = action,
                    UserId = userId,
                    Timestamp = DateTime.UtcNow,
                    NewValues = payload
                };

                await _dbContext.DomainAuditRecords.AddAsync(audit, ct);
                await _dbContext.SaveChangesAsync(ct);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Attempt {Attempt} to save award notification audit failed. Retrying...", attempt);
                try { await Task.Delay(50 * attempt, ct); } catch { /* ignore */ }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogError(ex, "Could not record award notification audit record for quotation {QuoteId} after {Attempts} attempts.", quoteId, attempt);
            }
        }
    }

    private static AwardNotificationOutcome ParseAwardAudit(DomainAuditRecord record)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(record.NewValues))
            {
                using var doc = JsonDocument.Parse(record.NewValues);
                var root = doc.RootElement;
                var status = root.TryGetProperty("Status", out var sp) ? sp.GetString() : (record.Action == "AwardNotificationSent" ? "Sent" : "Failed");
                var recipient = root.TryGetProperty("Recipient", out var rp) ? rp.GetString() : null;
                var failureReason = root.TryGetProperty("FailureReason", out var fp) ? fp.GetString() : null;
                DateTime? sentAt = null;
                if (root.TryGetProperty("SentAt", out var stp) && stp.TryGetDateTime(out var dt))
                {
                    sentAt = dt;
                }
                else if (status == "Sent")
                {
                    sentAt = record.Timestamp;
                }
                return new AwardNotificationOutcome(status ?? "Sent", sentAt, failureReason, recipient);
            }
        }
        catch
        {
            // fallback
        }
        return new AwardNotificationOutcome(record.Action == "AwardNotificationSent" ? "Sent" : "Failed", record.Timestamp, null, null);
    }

    private static SupplierQuotationDto MapToQuotationDto(
        SupplierQuotation q,
        string? awardStatus = null,
        DateTime? awardSentAt = null,
        string? awardFailureReason = null,
        string? awardRecipientEmail = null)
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
            AwardNotificationStatus = awardStatus,
            AwardNotificationSentAt = awardSentAt,
            AwardNotificationFailureReason = awardFailureReason,
            AwardNotificationRecipientEmail = awardRecipientEmail,
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
