using System.Threading;
using System.Threading.Tasks;
using API.Features.Portal.DTOs;

namespace API.Features.Portal.Services;

/// <summary>
/// Service interface governing Customer Company registration and initial contact creation.
/// </summary>
public interface ICustomerRegistrationService
{
    Task<CustomerRegistrationResult> RegisterCompanyCustomerAsync(
        RegisterCompanyCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerRegistrationResult> RegisterIndividualCustomerAsync(
        RegisterIndividualCustomerRequest request,
        CancellationToken cancellationToken = default);
}
