namespace Security.Interfaces;

public interface IUserAccessService
{
    Task<bool> CanAccessCompanyAsync(
        Guid userId,
        Guid tenantId,
        Guid companyId);

    Task<bool> CanAccessBranchAsync(
        Guid userId,
        Guid tenantId,
        Guid companyId,
        Guid branchId);
}