using Security.Core.Models;
using Security.Interfaces;

namespace Security.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _repository;
    private readonly ICurrentUserContext _currentUserContext;

    public RoleService(
        IRoleRepository repository,
        ICurrentUserContext currentUserContext)
    {
        _repository = repository;
        _currentUserContext = currentUserContext;
    }

    public async Task<Role> CreateRoleAsync(
        string name,
        string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Role name is required.",
                nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "Role description is required.",
                nameof(description));

        Guid? tenantId =
            _currentUserContext.TenantId;

        var existingRole =
            await _repository.GetByNameAsync(
                tenantId,
                name.Trim());

        if (existingRole != null)
            throw new InvalidOperationException(
                "Role already exists in this scope.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description.Trim(),

            // Custom role creation is never a system role.
            IsSystemRole = false,

            IsActive = true
        };

        await _repository.AddAsync(role);
        await _repository.SaveChangesAsync();

        return role;
    }
}