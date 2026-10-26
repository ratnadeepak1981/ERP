using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.PaymentTerm;

public interface IPaymentTermRepository
{
    Task<List<PaymentTerm>> GetByTenantAsync(Guid tenantId);
    Task<PaymentTerm?> GetByIdAsync(Guid tenantId, Guid paymentTermId);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string termCode);
    Task<bool> ExistsByNameAsync(Guid tenantId, string termName);
    Task AddAsync(PaymentTerm paymentTerm);
    Task SaveChangesAsync();
}
