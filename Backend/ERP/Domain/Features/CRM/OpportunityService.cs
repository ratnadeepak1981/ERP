using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP.Domain.Features.MasterData.Customer;

namespace ERP.Domain.Features.CRM;

public class OpportunityService : IOpportunityService
{
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly ICustomerRepository _customerRepository;

    public OpportunityService(
        IOpportunityRepository opportunityRepository,
        ICustomerRepository customerRepository)
    {
        _opportunityRepository = opportunityRepository ?? throw new ArgumentNullException(nameof(opportunityRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    }

    public async Task<List<OpportunityDto>> GetOpportunitiesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? customerId = null,
        OpportunityStage? stage = null,
        CancellationToken cancellationToken = default)
    {
        var list = await _opportunityRepository.GetOpportunitiesAsync(
            tenantId,
            companyId,
            branchId,
            customerId,
            stage,
            cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    public async Task<OpportunityDto?> GetOpportunityByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid opportunityId,
        CancellationToken cancellationToken = default)
    {
        var opp = await _opportunityRepository.GetByIdAsync(tenantId, companyId, opportunityId, cancellationToken);
        return opp == null ? null : MapToDto(opp);
    }

    public async Task<OpportunityDto> CreateOpportunityAsync(
        Guid tenantId,
        Guid companyId,
        CreateOpportunityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.OpportunityNumber))
            throw new ArgumentException("Opportunity number is required.", nameof(request.OpportunityNumber));

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.", nameof(request.Title));

        if (request.CustomerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(request.CustomerId));

        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, request.CustomerId)
            ?? throw new KeyNotFoundException("Customer not found or does not belong to authorized tenant.");

        if (!customer.IsActive)
            throw new InvalidOperationException($"Cannot create opportunity for inactive customer '{customer.CustomerCode}'.");

        if (await _opportunityRepository.ExistsOpportunityNumberAsync(tenantId, companyId, request.OpportunityNumber, cancellationToken))
        {
            throw new InvalidOperationException($"Opportunity number '{request.OpportunityNumber}' already exists for this company.");
        }

        var opp = Opportunity.Create(
            tenantId,
            companyId,
            request.CustomerId,
            request.OpportunityNumber,
            request.Title,
            request.EstimatedValue,
            request.BranchId,
            request.LeadId,
            request.Stage,
            request.ProbabilityPercent,
            request.Currency,
            request.ExpectedCloseDate,
            request.AssignedToUserId,
            request.Description,
            request.CampaignSource,
            request.CampaignMedium,
            request.CampaignCode);

        await _opportunityRepository.AddAsync(opp, cancellationToken);
        await _opportunityRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(opp);
    }

    public async Task<OpportunityDto> UpdateOpportunityAsync(
        Guid tenantId,
        Guid companyId,
        Guid opportunityId,
        UpdateOpportunityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var opp = await _opportunityRepository.GetByIdAsync(tenantId, companyId, opportunityId, cancellationToken)
            ?? throw new KeyNotFoundException("Opportunity not found in authorized company scope.");

        opp.Update(
            request.Title,
            request.EstimatedValue,
            request.Currency,
            request.ExpectedCloseDate,
            request.Description);

        await _opportunityRepository.SaveChangesAsync(cancellationToken);
        return MapToDto(opp);
    }

    public async Task<OpportunityDto> UpdateOpportunityStageAsync(
        Guid tenantId,
        Guid companyId,
        Guid opportunityId,
        UpdateOpportunityStageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var opp = await _opportunityRepository.GetByIdAsync(tenantId, companyId, opportunityId, cancellationToken)
            ?? throw new KeyNotFoundException("Opportunity not found in authorized company scope.");

        if (request.Stage == OpportunityStage.ClosedWon)
        {
            opp.CloseWon();
        }
        else if (request.Stage == OpportunityStage.ClosedLost)
        {
            opp.CloseLost(request.LossReason ?? "Unspecified loss reason");
        }
        else
        {
            opp.UpdateStage(request.Stage, request.ProbabilityPercent);
        }

        await _opportunityRepository.SaveChangesAsync(cancellationToken);
        return MapToDto(opp);
    }

    private static OpportunityDto MapToDto(Opportunity x) => new()
    {
        OpportunityId = x.OpportunityId,
        TenantId = x.TenantId,
        CompanyId = x.CompanyId,
        BranchId = x.BranchId,
        OpportunityNumber = x.OpportunityNumber,
        Title = x.Title,
        CustomerId = x.CustomerId,
        LeadId = x.LeadId,
        Stage = x.Stage,
        ProbabilityPercent = x.ProbabilityPercent,
        EstimatedValue = x.EstimatedValue,
        Currency = x.Currency,
        ExpectedCloseDate = x.ExpectedCloseDate,
        ActualCloseDate = x.ActualCloseDate,
        AssignedToUserId = x.AssignedToUserId,
        LossReason = x.LossReason,
        Description = x.Description,
        CampaignSource = x.CampaignSource,
        CampaignMedium = x.CampaignMedium,
        CampaignCode = x.CampaignCode,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt,
        ModifiedAt = x.ModifiedAt
    };
}
