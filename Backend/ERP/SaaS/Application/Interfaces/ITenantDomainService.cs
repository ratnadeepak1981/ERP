using System.Threading;
using System.Threading.Tasks;
using SaaS.Application.DTOs;

namespace SaaS.Application.Interfaces;

public interface ITenantDomainService
{
    Task<TenantResolutionResult> ResolveTenantByHostnameAsync(
        string hostname,
        CancellationToken cancellationToken = default);
}
