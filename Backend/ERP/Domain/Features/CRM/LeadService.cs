using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.CRM.Deduplication;
using ERP.Domain.Features.MasterData.Customer;

namespace ERP.Domain.Features.CRM;

public class LeadService : ILeadService
{
    private readonly ILeadRepository _leadRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerDeduplicationService _deduplicationService;
    private readonly DomainDbContext _context;

    public LeadService(
        ILeadRepository leadRepository,
        ICustomerRepository customerRepository,
        ICustomerDeduplicationService deduplicationService,
        DomainDbContext context)
    {
        _leadRepository = leadRepository ?? throw new ArgumentNullException(nameof(leadRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _deduplicationService = deduplicationService ?? throw new ArgumentNullException(nameof(deduplicationService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<LeadDto>> GetLeadsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? assignedUserId = null,
        LeadStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var list = await _leadRepository.GetLeadsAsync(
            tenantId,
            companyId,
            branchId,
            assignedUserId,
            status,
            cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    public async Task<LeadDto?> GetLeadByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        CancellationToken cancellationToken = default)
    {
        var lead = await _leadRepository.GetByIdAsync(tenantId, companyId, leadId, cancellationToken);
        return lead == null ? null : MapToDto(lead);
    }

    public async Task<LeadDto> CreateLeadAsync(
        Guid tenantId,
        Guid companyId,
        CreateLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.LeadNumber))
            throw new ArgumentException("Lead number is required.", nameof(request.LeadNumber));

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.", nameof(request.Title));

        if (string.IsNullOrWhiteSpace(request.ContactName))
            throw new ArgumentException("Contact name is required.", nameof(request.ContactName));

        if (await _leadRepository.ExistsLeadNumberAsync(tenantId, companyId, request.LeadNumber, cancellationToken))
        {
            throw new InvalidOperationException($"Lead number '{request.LeadNumber}' already exists for this company.");
        }

        var lead = Lead.Create(
            tenantId,
            companyId,
            request.LeadNumber,
            request.Title,
            request.ContactName,
            request.BranchId,
            request.CompanyName,
            request.Email,
            request.Phone,
            request.LeadSource,
            request.AssignedToUserId,
            request.EstimatedValue,
            request.Currency,
            request.CampaignSource,
            request.CampaignMedium,
            request.CampaignCode,
            request.ReferrerUrl,
            request.Notes);

        await _leadRepository.AddAsync(lead, cancellationToken);
        await _leadRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(lead);
    }

    public async Task<LeadDto> UpdateLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        UpdateLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var lead = await _leadRepository.GetByIdAsync(tenantId, companyId, leadId, cancellationToken)
            ?? throw new KeyNotFoundException("Lead not found in authorized company scope.");

        lead.Update(
            request.Title,
            request.ContactName,
            request.CompanyName,
            request.Email,
            request.Phone,
            request.LeadSource,
            request.EstimatedValue,
            request.Currency,
            request.Notes);

        await _leadRepository.SaveChangesAsync(cancellationToken);
        return MapToDto(lead);
    }

    public async Task<LeadDto> AssignLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        AssignLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var lead = await _leadRepository.GetByIdAsync(tenantId, companyId, leadId, cancellationToken)
            ?? throw new KeyNotFoundException("Lead not found in authorized company scope.");

        lead.AssignTo(request.AssignedToUserId, request.BranchId);
        await _leadRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(lead);
    }

    public async Task<LeadDto> QualifyLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        CancellationToken cancellationToken = default)
    {
        var lead = await _leadRepository.GetByIdAsync(tenantId, companyId, leadId, cancellationToken)
            ?? throw new KeyNotFoundException("Lead not found in authorized company scope.");

        lead.Qualify();
        await _leadRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(lead);
    }

    public async Task<LeadDto> DisqualifyLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        DisqualifyLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var lead = await _leadRepository.GetByIdAsync(tenantId, companyId, leadId, cancellationToken)
            ?? throw new KeyNotFoundException("Lead not found in authorized company scope.");

        lead.Disqualify(request.Reason);
        await _leadRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(lead);
    }

    public async Task<ConvertLeadResult> ConvertLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        ConvertLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var lead = await _leadRepository.GetByIdAsync(tenantId, companyId, leadId, cancellationToken)
            ?? throw new KeyNotFoundException("Lead not found in authorized company scope.");

        if (lead.Status == LeadStatus.Converted)
        {
            throw new InvalidOperationException($"Lead '{lead.LeadNumber}' is already converted.");
        }

        // 1. Run Deduplication Check
        var criteria = new CustomerMatchCriteria
        {
            TaxNumber = request.TaxNumber,
            Email = lead.Email,
            Phone = lead.Phone,
            CustomerName = request.CustomerName ?? lead.CompanyName ?? lead.ContactName
        };

        var matchResult = await _deduplicationService.FindDuplicateAsync(tenantId, criteria, cancellationToken);

        // 2. If matching existing customer found and LinkToExistingIfMatched is true -> link without creating duplicate
        if (request.LinkToExistingIfMatched && matchResult.HasPotentialDuplicate && matchResult.MatchedCustomerId.HasValue)
        {
            lead.ConvertToCustomer(matchResult.MatchedCustomerId.Value);
            await _leadRepository.SaveChangesAsync(cancellationToken);

            return new ConvertLeadResult
            {
                LeadId = lead.LeadId,
                CustomerId = matchResult.MatchedCustomerId.Value,
                CustomerCode = matchResult.MatchedCustomerCode ?? string.Empty,
                CustomerName = matchResult.MatchedCustomerName ?? string.Empty,
                WasExistingCustomer = true,
                MatchReason = matchResult.MatchReason ?? "Linked to existing customer match."
            };
        }

        // 3. Create New Customer
        string custCode = !string.IsNullOrWhiteSpace(request.CustomerCode)
            ? request.CustomerCode.Trim()
            : $"CUST-{lead.LeadNumber}";

        string custName = !string.IsNullOrWhiteSpace(request.CustomerName)
            ? request.CustomerName.Trim()
            : lead.CompanyName ?? lead.ContactName;

        if (await _customerRepository.ExistsByCodeAsync(tenantId, custCode))
        {
            custCode = $"CUST-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        }

        var customer = Customer.Create(
            tenantId,
            custCode,
            custName,
            contactPerson: lead.ContactName,
            email: lead.Email,
            phone: lead.Phone,
            addressLine1: request.AddressLine1,
            city: request.City,
            country: request.Country,
            taxNumber: request.TaxNumber);

        await _customerRepository.AddCustomerAsync(customer);

        lead.ConvertToCustomer(customer.CustomerId);

        await _leadRepository.SaveChangesAsync(cancellationToken);

        return new ConvertLeadResult
        {
            LeadId = lead.LeadId,
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            WasExistingCustomer = false,
            MatchReason = "Provisioned new customer master record."
        };
    }

    private static LeadDto MapToDto(Lead x) => new()
    {
        LeadId = x.LeadId,
        TenantId = x.TenantId,
        CompanyId = x.CompanyId,
        BranchId = x.BranchId,
        LeadNumber = x.LeadNumber,
        Title = x.Title,
        CompanyName = x.CompanyName,
        ContactName = x.ContactName,
        Email = x.Email,
        Phone = x.Phone,
        LeadSource = x.LeadSource,
        Status = x.Status,
        AssignedToUserId = x.AssignedToUserId,
        EstimatedValue = x.EstimatedValue,
        Currency = x.Currency,
        CampaignSource = x.CampaignSource,
        CampaignMedium = x.CampaignMedium,
        CampaignCode = x.CampaignCode,
        ReferrerUrl = x.ReferrerUrl,
        QualifiedAt = x.QualifiedAt,
        DisqualifiedAt = x.DisqualifiedAt,
        DisqualificationReason = x.DisqualificationReason,
        ConvertedCustomerId = x.ConvertedCustomerId,
        ConvertedAt = x.ConvertedAt,
        Notes = x.Notes,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt,
        ModifiedAt = x.ModifiedAt
    };
}
