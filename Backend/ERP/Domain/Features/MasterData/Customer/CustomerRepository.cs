using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.MasterData.Customer;

public class CustomerRepository : ICustomerRepository
{
    private readonly DomainDbContext _context;

    public CustomerRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<Customer>> GetCustomersAsync(Guid tenantId)
    {
        return await _context.Customers
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<Customer?> GetCustomerByIdAsync(Guid tenantId, Guid customerId)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerId == customerId && x.IsActive);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code)
    {
        return await _context.Customers
            .AnyAsync(x => x.TenantId == tenantId && x.CustomerCode == code);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string name)
    {
        return await _context.Customers
            .AnyAsync(x => x.TenantId == tenantId && x.CustomerName == name);
    }

    public async Task AddCustomerAsync(Customer customer)
    {
        await _context.Customers.AddAsync(customer);
    }

    public async Task<List<CustomerAddress>> GetAddressesAsync(Guid tenantId, Guid customerId)
    {
        return await _context.CustomerAddresses
            .Where(x => x.TenantId == tenantId && x.CustomerId == customerId && x.IsActive)
            .ToListAsync();
    }

    public async Task<CustomerAddress?> GetAddressAssociationAsync(Guid tenantId, Guid customerId, Guid addressId, Guid addressTypeId)
    {
        return await _context.CustomerAddresses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerId == customerId && x.AddressId == addressId && x.AddressTypeId == addressTypeId && x.IsActive);
    }

    public async Task<CustomerAddress?> GetAddressAssociationByIdAsync(Guid tenantId, Guid customerAddressId)
    {
        return await _context.CustomerAddresses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerAddressId == customerAddressId && x.IsActive);
    }

    public async Task AddAddressAssociationAsync(CustomerAddress association)
    {
        await _context.CustomerAddresses.AddAsync(association);
    }

    public async Task<List<CustomerContact>> GetContactsAsync(Guid tenantId, Guid customerId)
    {
        return await _context.CustomerContacts
            .Where(x => x.TenantId == tenantId && x.CustomerId == customerId && x.IsActive)
            .ToListAsync();
    }

    public async Task<CustomerContact?> GetContactAssociationAsync(Guid tenantId, Guid customerId, Guid contactId, Guid contactTypeId)
    {
        return await _context.CustomerContacts
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerId == customerId && x.ContactId == contactId && x.ContactTypeId == contactTypeId && x.IsActive);
    }

    public async Task<CustomerContact?> GetContactAssociationByIdAsync(Guid tenantId, Guid customerContactId)
    {
        return await _context.CustomerContacts
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerContactId == customerContactId && x.IsActive);
    }

    public async Task AddContactAssociationAsync(CustomerContact association)
    {
        await _context.CustomerContacts.AddAsync(association);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
