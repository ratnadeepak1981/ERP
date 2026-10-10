using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Customer;

public interface ICustomerService
{
    Task<List<Customer>> GetCustomersAsync(Guid tenantId);
    Task<Customer?> GetCustomerAsync(Guid tenantId, Guid customerId);
    Task<Customer> CreateCustomerAsync(Guid tenantId, CreateCustomerRequest request);
    Task<Customer> UpdateCustomerAsync(Guid tenantId, Guid customerId, UpdateCustomerRequest request);
    Task DeactivateCustomerAsync(Guid tenantId, Guid customerId);

    Task<List<CustomerAddress>> GetAddressesAsync(Guid tenantId, Guid customerId);
    Task<CustomerAddress> LinkAddressAsync(Guid tenantId, Guid customerId, CustomerAddressLinkRequest request);
    Task SetDefaultAddressAsync(Guid tenantId, Guid customerId, Guid customerAddressId);
    Task RemoveAddressLinkAsync(Guid tenantId, Guid customerId, Guid customerAddressId);

    Task<List<CustomerContact>> GetContactsAsync(Guid tenantId, Guid customerId);
    Task<CustomerContact> LinkContactAsync(Guid tenantId, Guid customerId, CustomerContactLinkRequest request);
    Task SetPrimaryContactAsync(Guid tenantId, Guid customerId, Guid customerContactId);
    Task RemoveContactLinkAsync(Guid tenantId, Guid customerId, Guid customerContactId);
}
