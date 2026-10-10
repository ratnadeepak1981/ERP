using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Customer;

public interface ICustomerRepository
{
    Task<List<Customer>> GetCustomersAsync(Guid tenantId);
    Task<Customer?> GetCustomerByIdAsync(Guid tenantId, Guid customerId);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code);
    Task<bool> ExistsByNameAsync(Guid tenantId, string name);
    Task AddCustomerAsync(Customer customer);

    Task<List<CustomerAddress>> GetAddressesAsync(Guid tenantId, Guid customerId);
    Task<CustomerAddress?> GetAddressAssociationAsync(Guid tenantId, Guid customerId, Guid addressId, Guid addressTypeId);
    Task<CustomerAddress?> GetAddressAssociationByIdAsync(Guid tenantId, Guid customerAddressId);
    Task AddAddressAssociationAsync(CustomerAddress association);

    Task<List<CustomerContact>> GetContactsAsync(Guid tenantId, Guid customerId);
    Task<CustomerContact?> GetContactAssociationAsync(Guid tenantId, Guid customerId, Guid contactId, Guid contactTypeId);
    Task<CustomerContact?> GetContactAssociationByIdAsync(Guid tenantId, Guid customerContactId);
    Task AddContactAssociationAsync(CustomerContact association);

    Task SaveChangesAsync();
}
