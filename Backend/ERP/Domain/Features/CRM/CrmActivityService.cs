using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Domain.Features.CRM;

public class CrmActivityService : ICrmActivityService
{
    private readonly ICrmActivityRepository _activityRepository;

    public CrmActivityService(ICrmActivityRepository activityRepository)
    {
        _activityRepository = activityRepository ?? throw new ArgumentNullException(nameof(activityRepository));
    }

    public async Task<List<CrmActivityDto>> GetActivitiesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? leadId = null,
        Guid? opportunityId = null,
        Guid? customerId = null,
        bool? isCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var list = await _activityRepository.GetActivitiesAsync(
            tenantId,
            companyId,
            branchId,
            leadId,
            opportunityId,
            customerId,
            isCompleted,
            cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    public async Task<CrmActivityDto?> GetActivityByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid activityId,
        CancellationToken cancellationToken = default)
    {
        var act = await _activityRepository.GetByIdAsync(tenantId, companyId, activityId, cancellationToken);
        return act == null ? null : MapToDto(act);
    }

    public async Task<CrmActivityDto> CreateActivityAsync(
        Guid tenantId,
        Guid companyId,
        CreateCrmActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("Subject is required.", nameof(request.Subject));

        var act = CrmActivity.Create(
            tenantId,
            companyId,
            request.ActivityType,
            request.Subject,
            request.BranchId,
            request.Description,
            request.DueDate,
            request.LeadId,
            request.OpportunityId,
            request.CustomerId,
            request.AssignedToUserId);

        await _activityRepository.AddAsync(act, cancellationToken);
        await _activityRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(act);
    }

    public async Task<CrmActivityDto> UpdateActivityAsync(
        Guid tenantId,
        Guid companyId,
        Guid activityId,
        UpdateCrmActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var act = await _activityRepository.GetByIdAsync(tenantId, companyId, activityId, cancellationToken)
            ?? throw new KeyNotFoundException("CRM Activity not found in authorized company scope.");

        act.Update(
            request.ActivityType,
            request.Subject,
            request.Description,
            request.DueDate);

        await _activityRepository.SaveChangesAsync(cancellationToken);
        return MapToDto(act);
    }

    public async Task<CrmActivityDto> CompleteActivityAsync(
        Guid tenantId,
        Guid companyId,
        Guid activityId,
        CompleteCrmActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var act = await _activityRepository.GetByIdAsync(tenantId, companyId, activityId, cancellationToken)
            ?? throw new KeyNotFoundException("CRM Activity not found in authorized company scope.");

        act.MarkCompleted(request.CompletionNotes, request.CompletedAt);
        await _activityRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(act);
    }

    private static CrmActivityDto MapToDto(CrmActivity x) => new()
    {
        CrmActivityId = x.CrmActivityId,
        TenantId = x.TenantId,
        CompanyId = x.CompanyId,
        BranchId = x.BranchId,
        ActivityType = x.ActivityType,
        Subject = x.Subject,
        Description = x.Description,
        DueDate = x.DueDate,
        CompletedAt = x.CompletedAt,
        IsCompleted = x.IsCompleted,
        CompletionNotes = x.CompletionNotes,
        LeadId = x.LeadId,
        OpportunityId = x.OpportunityId,
        CustomerId = x.CustomerId,
        AssignedToUserId = x.AssignedToUserId,
        IsActive = x.IsActive,
        CreatedAt = x.CreatedAt,
        ModifiedAt = x.ModifiedAt
    };
}
