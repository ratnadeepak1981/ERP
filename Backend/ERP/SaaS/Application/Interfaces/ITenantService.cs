using SaaS.Application.DTOs;
using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ITenantService
{
    Tenant CreateTenant(string name, string code);

    TenantRegistrationResult RegisterTenant(
        CreateTenantRequest request);
}