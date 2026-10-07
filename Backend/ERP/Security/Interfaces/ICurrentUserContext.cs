namespace Security.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }

    Guid? TenantId { get; }

    bool IsPlatformUser { get; }

    bool IsTenantUser { get; }

    IReadOnlyCollection<Guid> CompanyIds { get; }

    IReadOnlyCollection<Guid> BranchIds { get; }
}