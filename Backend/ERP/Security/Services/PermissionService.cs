using Security.Core.Models;
using Security.Interfaces;

namespace Security.Services;

public class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _repository;

    public PermissionService(
        IPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Permission> CreatePermissionAsync(
        string code,
        string name,
        string description,
        string module)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "Permission code is required.",
                nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Permission name is required.",
                nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "Permission description is required.",
                nameof(description));

        if (string.IsNullOrWhiteSpace(module))
            throw new ArgumentException(
                "Permission module is required.",
                nameof(module));

        code = code.Trim();

        var existingPermission =
            await _repository.GetByCodeAsync(code);

        if (existingPermission != null)
            throw new InvalidOperationException(
                "Permission already exists.");

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name.Trim(),
            Description = description.Trim(),
            Module = module.Trim(),
            IsActive = true
        };

        await _repository.AddAsync(permission);

        await _repository.SaveChangesAsync();

        return permission;
    }
}