using System;

namespace ERP.Domain.Common.Scope;

public interface ITenantScopedEntity
{
    Guid TenantId { get; }
}

public interface ICompanyScopedEntity
{
    Guid CompanyId { get; }
}

public interface IBranchScopedEntity
{
    Guid BranchId { get; }
}
