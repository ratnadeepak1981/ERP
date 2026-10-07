namespace Security.Interfaces;

public interface IUserScopeService
{
    Task<IReadOnlyCollection<Guid>> GetCompanyIdsAsync(Guid userId);

    Task<IReadOnlyCollection<Guid>> GetBranchIdsAsync(Guid userId);
}