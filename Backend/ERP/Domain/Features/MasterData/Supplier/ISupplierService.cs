using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Supplier;

public interface ISupplierService
{
    Task<List<Supplier>> GetSuppliersAsync(Guid tenantId);
    Task<Supplier?> GetSupplierAsync(Guid tenantId, Guid supplierId);
    Task<Supplier> CreateSupplierAsync(Guid tenantId, CreateSupplierRequest request);
    Task<Supplier> UpdateSupplierAsync(Guid tenantId, Guid supplierId, UpdateSupplierRequest request);
    Task DeactivateSupplierAsync(Guid tenantId, Guid supplierId);

    Task<List<SupplierAddress>> GetAddressesAsync(Guid tenantId, Guid supplierId);
    Task<SupplierAddress> LinkAddressAsync(Guid tenantId, Guid supplierId, SupplierAddressLinkRequest request);
    Task SetDefaultAddressAsync(Guid tenantId, Guid supplierId, Guid supplierAddressId);
    Task RemoveAddressLinkAsync(Guid tenantId, Guid supplierId, Guid supplierAddressId);

    Task<List<SupplierContact>> GetContactsAsync(Guid tenantId, Guid supplierId);
    Task<SupplierContact> LinkContactAsync(Guid tenantId, Guid supplierId, SupplierContactLinkRequest request);
    Task SetPrimaryContactAsync(Guid tenantId, Guid supplierId, Guid supplierContactId);
    Task RemoveContactLinkAsync(Guid tenantId, Guid supplierId, Guid supplierContactId);
}
