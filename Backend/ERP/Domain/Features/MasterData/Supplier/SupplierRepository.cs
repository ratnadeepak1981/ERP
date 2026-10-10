using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.MasterData.Supplier;

public class SupplierRepository : ISupplierRepository
{
    private readonly DomainDbContext _context;

    public SupplierRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<Supplier>> GetSuppliersAsync(Guid tenantId)
    {
        return await _context.Suppliers
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<Supplier?> GetSupplierByIdAsync(Guid tenantId, Guid supplierId)
    {
        return await _context.Suppliers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SupplierId == supplierId && x.IsActive);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code)
    {
        return await _context.Suppliers
            .AnyAsync(x => x.TenantId == tenantId && x.SupplierCode == code);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string name)
    {
        return await _context.Suppliers
            .AnyAsync(x => x.TenantId == tenantId && x.SupplierName == name);
    }

    public async Task AddSupplierAsync(Supplier supplier)
    {
        await _context.Suppliers.AddAsync(supplier);
    }

    public async Task<List<SupplierAddress>> GetAddressesAsync(Guid tenantId, Guid supplierId)
    {
        return await _context.SupplierAddresses
            .Where(x => x.TenantId == tenantId && x.SupplierId == supplierId && x.IsActive)
            .ToListAsync();
    }

    public async Task<SupplierAddress?> GetAddressAssociationAsync(Guid tenantId, Guid supplierId, Guid addressId, Guid addressTypeId)
    {
        return await _context.SupplierAddresses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SupplierId == supplierId && x.AddressId == addressId && x.AddressTypeId == addressTypeId && x.IsActive);
    }

    public async Task<SupplierAddress?> GetAddressAssociationByIdAsync(Guid tenantId, Guid supplierAddressId)
    {
        return await _context.SupplierAddresses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SupplierAddressId == supplierAddressId && x.IsActive);
    }

    public async Task AddAddressAssociationAsync(SupplierAddress association)
    {
        await _context.SupplierAddresses.AddAsync(association);
    }

    public async Task<List<SupplierContact>> GetContactsAsync(Guid tenantId, Guid supplierId)
    {
        return await _context.SupplierContacts
            .Where(x => x.TenantId == tenantId && x.SupplierId == supplierId && x.IsActive)
            .ToListAsync();
    }

    public async Task<SupplierContact?> GetContactAssociationAsync(Guid tenantId, Guid supplierId, Guid contactId, Guid contactTypeId)
    {
        return await _context.SupplierContacts
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SupplierId == supplierId && x.ContactId == contactId && x.ContactTypeId == contactTypeId && x.IsActive);
    }

    public async Task<SupplierContact?> GetContactAssociationByIdAsync(Guid tenantId, Guid supplierContactId)
    {
        return await _context.SupplierContacts
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SupplierContactId == supplierContactId && x.IsActive);
    }

    public async Task AddContactAssociationAsync(SupplierContact association)
    {
        await _context.SupplierContacts.AddAsync(association);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
