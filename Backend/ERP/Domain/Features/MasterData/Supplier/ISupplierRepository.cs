using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Supplier;

public interface ISupplierRepository
{
    Task<List<Supplier>> GetSuppliersAsync(Guid tenantId);
    Task<Supplier?> GetSupplierByIdAsync(Guid tenantId, Guid supplierId);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code);
    Task<bool> ExistsByNameAsync(Guid tenantId, string name);
    Task AddSupplierAsync(Supplier supplier);

    Task<List<SupplierAddress>> GetAddressesAsync(Guid tenantId, Guid supplierId);
    Task<SupplierAddress?> GetAddressAssociationAsync(Guid tenantId, Guid supplierId, Guid addressId, Guid addressTypeId);
    Task<SupplierAddress?> GetAddressAssociationByIdAsync(Guid tenantId, Guid supplierAddressId);
    Task AddAddressAssociationAsync(SupplierAddress association);

    Task<List<SupplierContact>> GetContactsAsync(Guid tenantId, Guid supplierId);
    Task<SupplierContact?> GetContactAssociationAsync(Guid tenantId, Guid supplierId, Guid contactId, Guid contactTypeId);
    Task<SupplierContact?> GetContactAssociationByIdAsync(Guid tenantId, Guid supplierContactId);
    Task AddContactAssociationAsync(SupplierContact association);

    Task SaveChangesAsync();
}
