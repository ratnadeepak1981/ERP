using Security.Core.Models;

namespace Security.Interfaces;

public interface IPermissionService
{
    Task<Permission> CreatePermissionAsync(
        string code,
        string name,
        string description,
        string module);
}