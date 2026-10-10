using System;
using System.Collections.Generic;
using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Contact;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.Supplier;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.PurchaseOrder;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.Domain.Features.MasterData.Warehouse;

namespace ERP.Domain.Common.Scope;

public static class EntityScopePolicy
{
    private static readonly Dictionary<Type, DataScope> DefinedScopes = new()
    {
        [typeof(Product)] = DataScope.Tenant,
        [typeof(Company)] = DataScope.Tenant,
        [typeof(Branch)] = DataScope.Company,
        [typeof(Category)] = DataScope.Tenant,
        [typeof(Supplier)] = DataScope.Tenant,
        [typeof(Customer)] = DataScope.Tenant,
        [typeof(Warehouse)] = DataScope.Tenant,
        [typeof(WarehouseZone)] = DataScope.Tenant,
        [typeof(WarehouseLocation)] = DataScope.Tenant,
        [typeof(UnitOfMeasure)] = DataScope.Tenant,
        [typeof(UnitOfMeasureConversion)] = DataScope.Tenant,
        [typeof(SupplierProductPrice)] = DataScope.Tenant,
        [typeof(Address)] = DataScope.Tenant,
        [typeof(Contact)] = DataScope.Tenant,
        [typeof(CustomerAddress)] = DataScope.Tenant,
        [typeof(CustomerContact)] = DataScope.Tenant,
        [typeof(SupplierAddress)] = DataScope.Tenant,
        [typeof(SupplierContact)] = DataScope.Tenant,
        [typeof(CompanyAddress)] = DataScope.Tenant,
        [typeof(BranchAddress)] = DataScope.Tenant,
        [typeof(WarehouseAddress)] = DataScope.Tenant,
        [typeof(PurchaseOrder)] = DataScope.Branch,
        [typeof(PurchaseOrderItem)] = DataScope.ParentTransaction
    };

    public static DataScope GetScope<T>()
    {
        return GetScope(typeof(T));
    }

    public static DataScope GetScope(Type entityType)
    {
        if (!DefinedScopes.TryGetValue(entityType, out var scope))
        {
            // Safeguard 1: Fail-closed. Never assume tenant or unrestricted scope.
            throw new InvalidOperationException(
                $"Data scope is undefined for entity '{entityType.Name}'. Access is rejected under fail-closed security policy.");
        }

        return scope;
    }

    public static bool IsScopeDefined(Type entityType)
    {
        return DefinedScopes.ContainsKey(entityType);
    }
}
