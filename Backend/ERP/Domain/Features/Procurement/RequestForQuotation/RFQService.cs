using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domain.Features.Procurement.RequestForQuotation;

public class RFQService : IRFQService
{
    private readonly DomainDbContext _dbContext;
    private readonly IRfqTokenService _tokenService;
    private readonly IRfqEmailNotificationService? _emailService;
    private readonly ILogger<RFQService>? _logger;

    public RFQService(
        DomainDbContext dbContext,
        IRfqTokenService tokenService,
        IRfqEmailNotificationService? emailService = null,
        ILogger<RFQService>? logger = null)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<RfqDetailsDto> CreateRfqAsync(
        CreateRfqRequest request,
        Guid userId,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("RFQ Title is required.", nameof(request.Title));

        if (request.ClosingDate <= DateTime.UtcNow)
            throw new ArgumentException("Closing date must be in the future.", nameof(request.ClosingDate));

        var rfqNumber = await GenerateRfqNumberAsync(tenantId, companyId, ct);

        var rfq = new RequestForQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RFQNumber = rfqNumber,
            Title = request.Title.Trim(),
            Status = RequestForQuotationStatus.Draft,
            ClosingDate = request.ClosingDate,
            DeliveryRequiredDate = request.DeliveryRequiredDate,
            DeliveryTerms = request.DeliveryTerms?.Trim(),
            Instructions = request.Instructions?.Trim()
        };

        // Add Lines
        if (request.Lines != null && request.Lines.Count > 0)
        {
            int lineNumber = 1;
            foreach (var lineReq in request.Lines)
            {
                if (lineReq.Quantity <= 0)
                    throw new ArgumentException($"Line {lineNumber}: Quantity must be greater than zero.");

                var product = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == lineReq.ProductId, ct);

                if (product == null)
                    throw new InvalidOperationException($"Product with ID {lineReq.ProductId} not found.");

                var uom = await _dbContext.UnitsOfMeasure
                    .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == lineReq.UnitOfMeasureId, ct);

                var uomCode = uom?.Code ?? product.UnitOfMeasure ?? "EA";

                rfq.Lines.Add(new RequestForQuotationLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQId = rfq.Id,
                    LineNumber = lineNumber++,
                    ProductId = product.ProductId,
                    ProductCode = product.ProductCode,
                    ProductName = product.ProductName,
                    Quantity = lineReq.Quantity,
                    UnitOfMeasureId = lineReq.UnitOfMeasureId,
                    UnitOfMeasureCode = uomCode,
                    TargetUnitPrice = lineReq.TargetUnitPrice,
                    RequiredDate = lineReq.RequiredDate ?? request.DeliveryRequiredDate,
                    TechnicalSpecifications = lineReq.TechnicalSpecifications?.Trim(),
                    Notes = lineReq.Notes?.Trim()
                });
            }
        }

        // Add Invited Suppliers
        if (request.SupplierIds != null && request.SupplierIds.Count > 0)
        {
            var distinctIds = request.SupplierIds.Distinct().ToList();
            var suppliers = await _dbContext.Suppliers
                .Where(s => s.TenantId == tenantId && distinctIds.Contains(s.SupplierId))
                .ToListAsync(ct);

            foreach (var supplier in suppliers)
            {
                rfq.InvitedSuppliers.Add(new RFQSupplier
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = companyId,
                    RFQId = rfq.Id,
                    SupplierId = supplier.SupplierId,
                    SupplierCode = supplier.SupplierCode,
                    SupplierName = supplier.SupplierName,
                    ContactEmail = supplier.Email ?? string.Empty,
                    InvitationStatus = RFQSupplierInvitationStatus.Pending
                });
            }
        }

        await _dbContext.RequestForQuotations.AddAsync(rfq, ct);
        _dbContext.Entry(rfq).Property(x => x.CreatedBy).CurrentValue = userId;
        await _dbContext.SaveChangesAsync(ct);

        return MapToDetailsDto(rfq);
    }

    public async Task<RfqDetailsDto> UpdateRfqAsync(
        Guid id,
        UpdateRfqRequest request,
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .Include(r => r.InvitedSuppliers)
            .Include(r => r.Quotations)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {id} not found.");

        if (rfq.Status != RequestForQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot edit RFQ in '{rfq.Status}' status. Only Draft RFQs can be modified.");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("RFQ Title is required.", nameof(request.Title));

        rfq.Title = request.Title.Trim();
        rfq.ClosingDate = request.ClosingDate;
        rfq.DeliveryRequiredDate = request.DeliveryRequiredDate;
        rfq.DeliveryTerms = request.DeliveryTerms?.Trim();
        rfq.Instructions = request.Instructions?.Trim();

        await _dbContext.SaveChangesAsync(ct);

        return MapToDetailsDto(rfq);
    }

    public async Task<RfqDetailsDto?> GetRfqByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .Include(r => r.InvitedSuppliers)
            .Include(r => r.Quotations)
                .ThenInclude(q => q.Lines)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

        if (rfq == null) return null;

        var dto = MapToDetailsDto(rfq);

        // Populate award notification audit information for approved quotations and winning supplier
        var approvedQuote = rfq.Quotations.FirstOrDefault(q => q.Status == SupplierQuotationStatus.Approved);
        if (approvedQuote != null)
        {
            var audit = await _dbContext.DomainAuditRecords
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.EntityName == "RfqAwardNotification" && a.EntityId == approvedQuote.Id.ToString())
                .OrderByDescending(a => a.Timestamp)
                .FirstOrDefaultAsync(ct);

            if (audit != null)
            {
                var outcome = ParseAwardAudit(audit);
                var quoteDto = dto.Quotations.FirstOrDefault(q => q.Id == approvedQuote.Id);
                if (quoteDto != null)
                {
                    quoteDto.AwardNotificationStatus = outcome.Status;
                    quoteDto.AwardNotificationSentAt = outcome.SentAt;
                    quoteDto.AwardNotificationFailureReason = outcome.FailureReason;
                    quoteDto.AwardNotificationRecipientEmail = outcome.RecipientEmail;
                }

                var winningSupDto = dto.InvitedSuppliers.FirstOrDefault(s => s.SupplierId == approvedQuote.SupplierId);
                if (winningSupDto != null)
                {
                    winningSupDto.AwardNotificationStatus = outcome.Status;
                    winningSupDto.AwardNotificationSentAt = outcome.SentAt;
                    winningSupDto.AwardNotificationFailureReason = outcome.FailureReason;
                }
            }
        }

        return dto;
    }

    public async Task<IReadOnlyList<RfqHeaderDto>> GetRfqsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        RfqFilterRequest? filter = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.RequestForQuotations
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId);

        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(r => r.BranchId == branchId.Value);
        }

        if (filter != null)
        {
            if (filter.Status.HasValue)
            {
                query = query.Where(r => r.Status == filter.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim().ToLower();
                query = query.Where(r => r.RFQNumber.ToLower().Contains(term) || r.Title.ToLower().Contains(term));
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(r => r.ClosingDate >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(r => r.ClosingDate <= filter.ToDate.Value);
            }
        }

        var rfqs = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RfqHeaderDto
            {
                Id = r.Id,
                TenantId = r.TenantId,
                CompanyId = r.CompanyId,
                BranchId = r.BranchId,
                RFQNumber = r.RFQNumber,
                Title = r.Title,
                Status = r.Status,
                ClosingDate = r.ClosingDate,
                DeliveryRequiredDate = r.DeliveryRequiredDate,
                DeliveryTerms = r.DeliveryTerms,
                Instructions = r.Instructions,
                LineCount = r.Lines.Count,
                InvitedSupplierCount = r.InvitedSuppliers.Count,
                ReceivedQuotationCount = r.Quotations.Count,
                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy
            })
            .ToListAsync(ct);

        return rfqs;
    }

    public async Task<RfqDetailsDto> AddLineAsync(
        Guid rfqId,
        CreateRfqLineRequest request,
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .Include(r => r.InvitedSuppliers)
            .Include(r => r.Quotations)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        if (rfq.Status != RequestForQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot add lines to RFQ in '{rfq.Status}' status.");

        if (request.Quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(request.Quantity));

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == request.ProductId, ct);

        if (product == null)
            throw new InvalidOperationException($"Product with ID {request.ProductId} not found.");

        var uom = await _dbContext.UnitsOfMeasure
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == request.UnitOfMeasureId, ct);

        var nextLineNumber = rfq.Lines.Any() ? rfq.Lines.Max(l => l.LineNumber) + 1 : 1;

        rfq.Lines.Add(new RequestForQuotationLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = rfq.CompanyId,
            RFQId = rfq.Id,
            LineNumber = nextLineNumber,
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            Quantity = request.Quantity,
            UnitOfMeasureId = request.UnitOfMeasureId,
            UnitOfMeasureCode = uom?.Code ?? product.UnitOfMeasure ?? "EA",
            TargetUnitPrice = request.TargetUnitPrice,
            RequiredDate = request.RequiredDate ?? rfq.DeliveryRequiredDate,
            TechnicalSpecifications = request.TechnicalSpecifications?.Trim(),
            Notes = request.Notes?.Trim()
        });

        await _dbContext.SaveChangesAsync(ct);
        return MapToDetailsDto(rfq);
    }

    public async Task<RfqDetailsDto> RemoveLineAsync(
        Guid rfqId,
        Guid lineId,
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .Include(r => r.InvitedSuppliers)
            .Include(r => r.Quotations)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        if (rfq.Status != RequestForQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot remove lines from RFQ in '{rfq.Status}' status.");

        var line = rfq.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line != null)
        {
            _dbContext.RequestForQuotationLines.Remove(line);
            rfq.Lines.Remove(line);

            // Renumber lines
            int num = 1;
            foreach (var remaining in rfq.Lines.OrderBy(l => l.LineNumber))
            {
                remaining.LineNumber = num++;
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        return MapToDetailsDto(rfq);
    }

    public async Task<RfqDetailsDto> AddSupplierAsync(
        Guid rfqId,
        AddRfqSupplierRequest request,
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .Include(r => r.InvitedSuppliers)
            .Include(r => r.Quotations)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        if (rfq.Status != RequestForQuotationStatus.Draft && rfq.Status != RequestForQuotationStatus.Issued)
            throw new InvalidOperationException($"Cannot add suppliers to RFQ in '{rfq.Status}' status.");

        if (rfq.InvitedSuppliers.Any(s => s.SupplierId == request.SupplierId))
            return MapToDetailsDto(rfq); // Already invited

        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SupplierId == request.SupplierId, ct);

        if (supplier == null)
            throw new InvalidOperationException($"Supplier with ID {request.SupplierId} not found.");

        var newSupplier = new RFQSupplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = rfq.CompanyId,
            RFQId = rfq.Id,
            SupplierId = supplier.SupplierId,
            SupplierCode = supplier.SupplierCode,
            SupplierName = supplier.SupplierName,
            ContactEmail = supplier.Email ?? string.Empty,
            InvitationStatus = RFQSupplierInvitationStatus.Pending
        };

        await _dbContext.RFQSuppliers.AddAsync(newSupplier, ct);
        _dbContext.Entry(newSupplier).Property(x => x.CreatedBy).CurrentValue = userId;

        await _dbContext.SaveChangesAsync(ct);
        return MapToDetailsDto(rfq);
    }

    public async Task<RfqDetailsDto> RemoveSupplierAsync(
        Guid rfqId,
        Guid supplierId,
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .Include(r => r.InvitedSuppliers)
            .Include(r => r.Quotations)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        if (rfq.Status != RequestForQuotationStatus.Draft)
            throw new InvalidOperationException($"Cannot remove suppliers from RFQ in '{rfq.Status}' status.");

        var supplier = rfq.InvitedSuppliers.FirstOrDefault(s => s.SupplierId == supplierId);
        if (supplier != null)
        {
            _dbContext.RFQSuppliers.Remove(supplier);
            rfq.InvitedSuppliers.Remove(supplier);

            await _dbContext.SaveChangesAsync(ct);
        }

        return MapToDetailsDto(rfq);
    }

    public async Task<RfqInvitationResultDto> SendInvitationsAsync(
        Guid rfqId,
        SendRfqInvitationsRequest request,
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .Include(r => r.InvitedSuppliers)
            .Include(r => r.Quotations)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        if (rfq.Status == RequestForQuotationStatus.Cancelled || rfq.Status == RequestForQuotationStatus.Closed)
            throw new InvalidOperationException($"Cannot send invitations for RFQ in '{rfq.Status}' status.");

        if (rfq.Status == RequestForQuotationStatus.Awarded)
        {
            return await RetryAwardNotificationInternalAsync(rfq, request, userId, tenantId, ct);
        }

        if (!rfq.Lines.Any())
            throw new InvalidOperationException("Cannot send invitations for an RFQ with no product lines.");

        if (request.SupplierIds is { Count: 0 })
            throw new InvalidOperationException("Select at least one supplier for invitation dispatch.");

        var targets = rfq.InvitedSuppliers.AsEnumerable();
        if (request.SupplierIds != null)
        {
            var selectedSupplierIds = request.SupplierIds.ToHashSet();
            targets = targets.Where(s => selectedSupplierIds.Contains(s.SupplierId));
        }

        var targetList = targets
            .Where(s => s.InvitationStatus is RFQSupplierInvitationStatus.Pending or RFQSupplierInvitationStatus.Failed)
            .ToList();
        if (!targetList.Any())
        {
            return new RfqInvitationResultDto
            {
                Messages = { "No pending or failed selected invitations are eligible for dispatch." }
            };
        }

        var result = new RfqInvitationResultDto();
        var company = await _dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == rfq.CompanyId, ct);
        var companyName = company?.Name ?? string.Empty;

        foreach (var supplier in targetList)
        {
            if (_emailService == null || string.IsNullOrWhiteSpace(supplier.ContactEmail))
            {
                supplier.InvitationStatus = RFQSupplierInvitationStatus.Failed;
                result.FailedCount++;
                result.Messages.Add(string.IsNullOrWhiteSpace(supplier.ContactEmail)
                    ? $"Invitation could not be dispatched to '{supplier.SupplierName}': supplier contact email is missing."
                    : $"Invitation could not be dispatched to '{supplier.SupplierName}': email service is unavailable.");
                continue;
            }

            string plaintextToken;
            try
            {
                var generatedToken = _tokenService.GenerateToken();
                plaintextToken = generatedToken.PlaintextToken;
                supplier.TokenHash = generatedToken.TokenHash;
                supplier.TokenExpiresAt = rfq.ClosingDate.AddDays(2); // Grace period
                supplier.InvitationStatus = RFQSupplierInvitationStatus.Sending;
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                supplier.InvitationStatus = RFQSupplierInvitationStatus.Failed;
                supplier.TokenHash = null;
                supplier.TokenExpiresAt = null;
                result.FailedCount++;
                _logger?.LogWarning(ex, "Could not prepare RFQ invitation for supplier {SupplierId}.", supplier.SupplierId);
                result.Messages.Add($"Invitation could not be dispatched to '{supplier.SupplierName}'. Correct the supplier email or mail configuration and retry.");
                await _dbContext.SaveChangesAsync(ct);
                continue;
            }

            RfqEmailDispatchResult dispatch;
            try
            {
                var emailContext = BuildInvitationEmailContext(
                    supplier,
                    rfq,
                    companyName,
                    plaintextToken,
                    request);

                dispatch = await _emailService.SendInvitationAsync(emailContext, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                supplier.InvitationStatus = RFQSupplierInvitationStatus.Failed;
                supplier.TokenHash = null;
                supplier.TokenExpiresAt = null;
                result.FailedCount++;
                _logger?.LogWarning(ex, "RFQ invitation dispatch failed for supplier {SupplierId}.", supplier.SupplierId);
                result.Messages.Add($"Invitation could not be dispatched to '{supplier.SupplierName}': {ex.Message}");
                await _dbContext.SaveChangesAsync(ct);
                continue;
            }

            if (dispatch.IsSuccess)
            {
                supplier.InvitationStatus = RFQSupplierInvitationStatus.Sent;
                supplier.InvitedAt = DateTime.UtcNow;
                result.SentCount++;
                result.Messages.Add($"Invitation email for '{supplier.SupplierName}' was accepted by the SMTP transport.");
            }
            else
            {
                supplier.InvitationStatus = RFQSupplierInvitationStatus.Failed;
                supplier.TokenHash = null;
                supplier.TokenExpiresAt = null;
                result.FailedCount++;
                _logger?.LogWarning(
                    "RFQ invitation dispatch failed for supplier {SupplierId}: {FailureReason}",
                    supplier.SupplierId,
                    dispatch.FailureReason);
                var detail = !string.IsNullOrWhiteSpace(dispatch.FailureReason)
                    ? $": {dispatch.FailureReason}"
                    : ". Correct the supplier email or mail configuration and retry.";
                result.Messages.Add($"Invitation could not be dispatched to '{supplier.SupplierName}'{detail}");
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        // Transition status to Issued if currently Draft
        if (rfq.Status == RequestForQuotationStatus.Draft && result.SentCount > 0)
        {
            rfq.Status = RequestForQuotationStatus.Issued;
        }

        await _dbContext.SaveChangesAsync(ct);
        return result;
    }

    public async Task<RfqDetailsDto> CloseRfqAsync(Guid rfqId, Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .Include(r => r.InvitedSuppliers)
            .Include(r => r.Quotations)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        rfq.Status = RequestForQuotationStatus.Closed;

        await _dbContext.SaveChangesAsync(ct);
        return MapToDetailsDto(rfq);
    }

    public async Task<RfqDetailsDto> CancelRfqAsync(Guid rfqId, string? reason, Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var rfq = await _dbContext.RequestForQuotations
            .Include(r => r.Lines)
            .Include(r => r.InvitedSuppliers)
            .Include(r => r.Quotations)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rfqId, ct);

        if (rfq == null)
            throw new KeyNotFoundException($"RFQ with ID {rfqId} not found.");

        rfq.Status = RequestForQuotationStatus.Cancelled;
        rfq.Instructions = (rfq.Instructions + $"\n[Cancelled: {reason}]").Trim();

        await _dbContext.SaveChangesAsync(ct);
        return MapToDetailsDto(rfq);
    }

    private async Task<string> GenerateRfqNumberAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"RFQ-{year}-";

        var latestRfq = await _dbContext.RequestForQuotations
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId && r.RFQNumber.StartsWith(prefix))
            .OrderByDescending(r => r.RFQNumber)
            .Select(r => r.RFQNumber)
            .FirstOrDefaultAsync(ct);

        int nextSeq = 1;
        if (!string.IsNullOrEmpty(latestRfq) && latestRfq.Length > prefix.Length)
        {
            var suffix = latestRfq.Substring(prefix.Length);
            if (int.TryParse(suffix, out int parsed))
            {
                nextSeq = parsed + 1;
            }
        }

        return $"{prefix}{nextSeq:D4}";
    }

    private static RfqDetailsDto MapToDetailsDto(RequestForQuotation rfq)
    {
        return new RfqDetailsDto
        {
            Id = rfq.Id,
            TenantId = rfq.TenantId,
            CompanyId = rfq.CompanyId,
            BranchId = rfq.BranchId,
            RFQNumber = rfq.RFQNumber,
            Title = rfq.Title,
            Status = rfq.Status,
            ClosingDate = rfq.ClosingDate,
            DeliveryRequiredDate = rfq.DeliveryRequiredDate,
            DeliveryTerms = rfq.DeliveryTerms,
            Instructions = rfq.Instructions,
            LineCount = rfq.Lines.Count,
            InvitedSupplierCount = rfq.InvitedSuppliers.Count,
            ReceivedQuotationCount = rfq.Quotations.Count,
            CreatedAt = rfq.CreatedAt,
            CreatedBy = rfq.CreatedBy,
            Lines = rfq.Lines.OrderBy(l => l.LineNumber).Select(l => new RfqLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                ProductId = l.ProductId,
                ProductCode = l.ProductCode,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                UnitOfMeasureId = l.UnitOfMeasureId,
                UnitOfMeasureCode = l.UnitOfMeasureCode,
                TargetUnitPrice = l.TargetUnitPrice,
                RequiredDate = l.RequiredDate,
                TechnicalSpecifications = l.TechnicalSpecifications,
                Notes = l.Notes
            }).ToList(),
            InvitedSuppliers = rfq.InvitedSuppliers.Select(s => new RfqSupplierDto
            {
                Id = s.Id,
                SupplierId = s.SupplierId,
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName,
                ContactEmail = s.ContactEmail,
                InvitationStatus = s.InvitationStatus,
                InvitedAt = s.InvitedAt,
                HasToken = !string.IsNullOrEmpty(s.TokenHash),
                TokenExpiresAt = s.TokenExpiresAt,
                ResponseReceivedAt = s.ResponseReceivedAt
            }).ToList(),
            Quotations = rfq.Quotations.Select(q => new SupplierQuotationDto
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
            }).ToList()
        };
    }

    private static RfqInvitationEmailContext BuildInvitationEmailContext(
        RFQSupplier supplier,
        RequestForQuotation rfq,
        string companyName,
        string plaintextToken,
        SendRfqInvitationsRequest request)
    {
        var rawPortalUrl = !string.IsNullOrWhiteSpace(request.PortalBaseUrl)
            ? request.PortalBaseUrl.Trim()
            : "http://localhost:4200";

        if (!Uri.TryCreate(rawPortalUrl, UriKind.Absolute, out var portalUri))
        {
            portalUri = new Uri("http://localhost:4200");
        }

        var scheme = portalUri.Scheme;
        var portPart = portalUri.IsDefaultPort ? string.Empty : $":{portalUri.Port}";
        var originalHost = portalUri.Host.ToLowerInvariant();

        // 1. Resolve candidate tenant subdomain
        var subdomain = !string.IsNullOrWhiteSpace(request.TenantSubdomain)
            ? request.TenantSubdomain.Trim().ToLowerInvariant()
            : null;

        if (string.IsNullOrWhiteSpace(subdomain) && originalHost.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            var parts = originalHost.Split('.');
            if (parts.Length == 2 && parts[0].Length >= 3)
            {
                subdomain = parts[0];
            }
        }

        // Option 1: Standard localhost
        var standardBase = $"{scheme}://localhost{portPart}";
        var standardUrl = $"{standardBase}/public/rfq-submit?token={plaintextToken}";

        // Option 2: Localhost tenant subdomain
        string? tenantLocalhostUrl = null;
        string? tenantLocalhostUnavailableReason = null;
        if (!string.IsNullOrWhiteSpace(subdomain))
        {
            tenantLocalhostUrl = $"{scheme}://{subdomain}.localhost{portPart}/public/rfq-submit?token={plaintextToken}";
        }
        else
        {
            tenantLocalhostUnavailableReason = "Tenant subdomain is not registered or assigned for this organization.";
        }

        // Option 3: External tenant access
        string? externalTenantUrl = null;
        string? externalTenantUnavailableReason = null;

        var externalHost = !string.IsNullOrWhiteSpace(request.ExternalBaseUrl)
            ? request.ExternalBaseUrl.Trim()
            : null;

        if (!string.IsNullOrWhiteSpace(externalHost))
        {
            if (externalHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                externalHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(externalHost, UriKind.Absolute, out var extUri))
                {
                    externalHost = extUri.Host;
                }
            }
            else
            {
                externalHost = externalHost.TrimEnd('/');
            }
        }

        if (string.IsNullOrWhiteSpace(externalHost) || externalHost.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            externalHost = "apexerp.com";
        }

        bool isIpAddress = System.Net.IPAddress.TryParse(externalHost, out _);
        if (isIpAddress)
        {
            // IP addresses cannot serve conventional DNS subdomains (e.g. tenant.192.168.1.1 is invalid DNS)
            externalTenantUrl = $"http://{externalHost}{portPart}/public/rfq-submit?token={plaintextToken}";
        }
        else if (!string.IsNullOrWhiteSpace(subdomain))
        {
            externalTenantUrl = $"https://{subdomain}.{externalHost}/public/rfq-submit?token={plaintextToken}";
        }
        else
        {
            externalTenantUnavailableReason = "Tenant subdomain is required for external hosted domain access.";
        }

        return new RfqInvitationEmailContext
        {
            RecipientEmail = supplier.ContactEmail,
            RecipientName = supplier.SupplierName,
            RfqNumber = rfq.RFQNumber,
            RfqTitle = rfq.Title,
            BuyingCompanyName = companyName,
            ResponseDeadline = rfq.ClosingDate,
            CustomNote = request.CustomEmailNote,
            StandardLocalhostUrl = standardUrl,
            TenantLocalhostUrl = tenantLocalhostUrl,
            TenantLocalhostUnavailableReason = tenantLocalhostUnavailableReason,
            ExternalTenantUrl = externalTenantUrl,
            ExternalTenantUnavailableReason = externalTenantUnavailableReason
        };
    }

    private static readonly ConcurrentDictionary<Guid, DateTime> _recentlySentAwards = new();
    private static readonly Regex EmailFormatRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    private async Task<RfqInvitationResultDto> RetryAwardNotificationInternalAsync(
        RequestForQuotation rfq,
        SendRfqInvitationsRequest request,
        Guid userId,
        Guid tenantId,
        CancellationToken ct)
    {
        var winningQuote = rfq.Quotations.FirstOrDefault(q => q.Status == SupplierQuotationStatus.Approved);
        if (winningQuote == null)
        {
            winningQuote = await _dbContext.SupplierQuotations
                .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.RFQId == rfq.Id && q.Status == SupplierQuotationStatus.Approved, ct);
        }

        if (winningQuote == null)
        {
            return new RfqInvitationResultDto
            {
                FailedCount = 1,
                Messages = { "Cannot dispatch award notification: no approved quotation was found on this awarded RFQ." }
            };
        }

        if (request.SupplierIds is { Count: > 0 } && !request.SupplierIds.Contains(winningQuote.SupplierId))
        {
            return new RfqInvitationResultDto
            {
                FailedCount = 1,
                Messages = { "This RFQ has already been awarded. Only the winning supplier is eligible for award notification dispatch or retry." }
            };
        }

        // 1. Check in-memory fast cache (suppresses duplicates even if DB write was transiently delayed)
        if (_recentlySentAwards.ContainsKey(winningQuote.Id))
        {
            return new RfqInvitationResultDto
            {
                SentCount = 0,
                FailedCount = 0,
                Messages = { $"Award notification has already been successfully sent to winning supplier '{winningQuote.SupplierName}'. Duplicate notifications are prevented." }
            };
        }

        // 2. Check DB audit records
        var latestAudit = await _dbContext.DomainAuditRecords
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EntityName == "RfqAwardNotification" && a.EntityId == winningQuote.Id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync(ct);

        if (latestAudit != null && latestAudit.Action == "AwardNotificationSent")
        {
            _recentlySentAwards.TryAdd(winningQuote.Id, latestAudit.Timestamp);
            return new RfqInvitationResultDto
            {
                SentCount = 0,
                FailedCount = 0,
                Messages = { $"Award notification has already been successfully sent to winning supplier '{winningQuote.SupplierName}'. Duplicate notifications are prevented." }
            };
        }

        // 3. Check in-flight dispatch (< 30 seconds old)
        if (latestAudit != null && latestAudit.Action == "AwardNotificationSending" &&
            DateTime.UtcNow - latestAudit.Timestamp < TimeSpan.FromSeconds(30))
        {
            return new RfqInvitationResultDto
            {
                SentCount = 0,
                FailedCount = 0,
                Messages = { $"Award notification dispatch for '{winningQuote.SupplierName}' is currently in progress. Please wait." }
            };
        }

        // 4. Resolve and validate supplier registered email
        var (isValidEmail, recipientEmail, emailValidationError) = await ResolveAndValidateSupplierEmailAsync(tenantId, rfq.Id, winningQuote.SupplierId, ct);
        if (!isValidEmail)
        {
            await RecordAwardNotificationAuditAsync(tenantId, winningQuote.Id, userId, "AwardNotificationFailed", "Failed", recipientEmail, emailValidationError, ct);

            return new RfqInvitationResultDto
            {
                FailedCount = 1,
                Messages = { $"Award notification could not be dispatched to '{winningQuote.SupplierName}': {emailValidationError}" }
            };
        }

        if (_emailService == null)
        {
            const string failReason = "Email notification service is unavailable.";
            await RecordAwardNotificationAuditAsync(tenantId, winningQuote.Id, userId, "AwardNotificationFailed", "Failed", recipientEmail, failReason, ct);

            return new RfqInvitationResultDto
            {
                FailedCount = 1,
                Messages = { $"Award notification could not be dispatched to '{winningQuote.SupplierName}': {failReason}" }
            };
        }

        // 5. Record in-flight intent before calling SMTP transport (mirrors RFQSupplierInvitationStatus.Sending)
        await RecordAwardNotificationAuditAsync(tenantId, winningQuote.Id, userId, "AwardNotificationSending", "Sending", recipientEmail, null, ct);

        var company = await _dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == rfq.CompanyId, ct);
        var companyName = !string.IsNullOrWhiteSpace(company?.Name) ? company.Name : "ApexERP Procurement";

        var awardEmailContext = new RfqAwardEmailContext
        {
            RecipientEmail = recipientEmail!,
            WinningSupplierName = winningQuote.SupplierName,
            RfqNumber = rfq.RFQNumber,
            RfqTitle = rfq.Title,
            QuotationReference = winningQuote.QuotationReference,
            BuyerCompanyName = companyName,
            AwardDate = winningQuote.ApprovedAt ?? DateTime.UtcNow,
            CustomRemarks = winningQuote.ApprovalRemarks,
            BuyerContactName = companyName,
            BuyerContactEmail = null,
            BuyerContactPhone = null
        };

        RfqEmailDispatchResult dispatch;
        try
        {
            dispatch = await _emailService.SendAwardNotificationAsync(awardEmailContext, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "RFQ award notification retry failed for supplier {SupplierId}.", winningQuote.SupplierId);
            dispatch = new RfqEmailDispatchResult(false, ex.Message);
        }

        if (dispatch.IsSuccess)
        {
            var sentAt = DateTime.UtcNow;
            _recentlySentAwards.TryAdd(winningQuote.Id, sentAt);
            await RecordAwardNotificationAuditAsync(tenantId, winningQuote.Id, userId, "AwardNotificationSent", "Sent", recipientEmail, null, ct, sentAt);
            return new RfqInvitationResultDto
            {
                SentCount = 1,
                FailedCount = 0,
                Messages = { $"Award notification email for '{winningQuote.SupplierName}' was accepted by the SMTP transport." }
            };
        }
        else
        {
            var failReason = !string.IsNullOrWhiteSpace(dispatch.FailureReason)
                ? dispatch.FailureReason
                : "SMTP transport rejected message.";
            await RecordAwardNotificationAuditAsync(tenantId, winningQuote.Id, userId, "AwardNotificationFailed", "Failed", recipientEmail, failReason, ct);
            return new RfqInvitationResultDto
            {
                SentCount = 0,
                FailedCount = 1,
                Messages = { $"Award notification could not be dispatched to '{winningQuote.SupplierName}': {failReason}" }
            };
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
                _logger?.LogWarning(ex, "Attempt {Attempt} to record award notification audit failed, retrying...", attempt);
                try { await Task.Delay(50 * attempt, ct); } catch { /* ignore */ }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogError(ex, "Could not record award notification audit record for quotation {QuoteId} after {Attempts} attempts.", quoteId, attempt);
            }
        }
    }

    private static (string Status, DateTime? SentAt, string? FailureReason, string? RecipientEmail) ParseAwardAudit(DomainAuditRecord record)
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
                return (status ?? "Sent", sentAt, failureReason, recipient);
            }
        }
        catch
        {
            // fallback
        }
        return (record.Action == "AwardNotificationSent" ? "Sent" : "Failed", record.Timestamp, null, null);
    }
}
