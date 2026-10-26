using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.PaymentTerm;

public interface IPaymentTermService
{
    Task<List<PaymentTerm>> GetPaymentTermsAsync(Guid tenantId);
    Task<PaymentTerm?> GetPaymentTermAsync(Guid tenantId, Guid paymentTermId);
    Task<PaymentTerm> CreatePaymentTermAsync(Guid tenantId, CreatePaymentTermRequest request);
    Task<PaymentTerm> UpdatePaymentTermAsync(Guid tenantId, Guid paymentTermId, UpdatePaymentTermRequest request);
    Task DeactivatePaymentTermAsync(Guid tenantId, Guid paymentTermId);
    Task<PaymentTerm> ReactivatePaymentTermAsync(Guid tenantId, Guid paymentTermId);
}
