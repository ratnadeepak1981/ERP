using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Features.Portal.DTOs;

namespace API.Features.Portal.Services;

public interface ICustomerAuthService
{
    Task<CustomerLoginResponse> LoginAsync(CustomerLoginRequest request, CancellationToken ct = default);
    Task<CustomerProfileDto> GetProfileAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<SellingCompanyDto>> GetAvailableSellingCompaniesAsync(Guid tenantId, CancellationToken ct = default);
    Task<SellingCompanyDto> ValidateSellingCompanyContextAsync(Guid customerId, Guid tenantId, Guid sellingCompanyId, CancellationToken ct = default);
}
