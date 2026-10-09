using System;
using System.Collections.Generic;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.PurchaseOrder;

namespace ERP.Domain.Common.Scope;

public static class EntityScopePolicy
{
    private static readonly Dictionary<Type, DataScope> DefinedScopes = new()
    {
        [typeof(Product)] = DataScope.Tenant,
        [typeof(Company)] = DataScope.Tenant,
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
