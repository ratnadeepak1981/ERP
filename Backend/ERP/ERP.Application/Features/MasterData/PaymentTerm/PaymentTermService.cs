using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.PaymentTerm;

namespace ERP.Application.Features.MasterData.PaymentTerm;

public class PaymentTermService : IPaymentTermService
{
    private readonly IPaymentTermRepository _repository;

    public PaymentTermService(IPaymentTermRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<global::Domain.Features.MasterData.PaymentTerm.PaymentTerm>> GetPaymentTermsAsync(Guid tenantId)
    {
        return await _repository.GetByTenantAsync(tenantId);
    }

    public async Task<global::Domain.Features.MasterData.PaymentTerm.PaymentTerm?> GetPaymentTermAsync(Guid tenantId, Guid paymentTermId)
    {
        return await _repository.GetByIdAsync(tenantId, paymentTermId);
    }

    public async Task<global::Domain.Features.MasterData.PaymentTerm.PaymentTerm> CreatePaymentTermAsync(Guid tenantId, CreatePaymentTermRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.TermCode))
            throw new ArgumentException("Term code is required.", nameof(request.TermCode));

        if (string.IsNullOrWhiteSpace(request.TermName))
            throw new ArgumentException("Term name is required.", nameof(request.TermName));

        if (request.Days < 0)
            throw new ArgumentException("Days cannot be negative.", nameof(request.Days));

        string code = request.TermCode.Trim().ToUpperInvariant();
        string name = request.TermName.Trim();

        if (await _repository.ExistsByCodeAsync(tenantId, code))
            throw new InvalidOperationException($"Payment term code '{code}' already exists for this tenant.");

        if (await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Payment term name '{name}' already exists for this tenant.");

        var term = global::Domain.Features.MasterData.PaymentTerm.PaymentTerm.Create(tenantId, code, name, request.Days, request.Description?.Trim());
        await _repository.AddAsync(term);
        await _repository.SaveChangesAsync();
        return term;
    }

    public async Task<global::Domain.Features.MasterData.PaymentTerm.PaymentTerm> UpdatePaymentTermAsync(Guid tenantId, Guid paymentTermId, UpdatePaymentTermRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.TermName))
            throw new ArgumentException("Term name is required.", nameof(request.TermName));

        if (request.Days < 0)
            throw new ArgumentException("Days cannot be negative.", nameof(request.Days));

        var term = await _repository.GetByIdAsync(tenantId, paymentTermId)
            ?? throw new KeyNotFoundException("Payment term not found.");

        string name = request.TermName.Trim();
        if (term.TermName != name && await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Payment term name '{name}' already exists for this tenant.");

        term.Update(name, request.Days, request.Description?.Trim());
        await _repository.SaveChangesAsync();
        return term;
    }

    public async Task DeactivatePaymentTermAsync(Guid tenantId, Guid paymentTermId)
    {
        var term = await _repository.GetByIdAsync(tenantId, paymentTermId)
            ?? throw new KeyNotFoundException("Payment term not found.");

        term.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<global::Domain.Features.MasterData.PaymentTerm.PaymentTerm> ReactivatePaymentTermAsync(Guid tenantId, Guid paymentTermId)
    {
        var term = await _repository.GetByIdAsync(tenantId, paymentTermId)
            ?? throw new KeyNotFoundException("Payment term not found.");

        if (term.IsActive)
            return term;

        term.Activate();
        await _repository.SaveChangesAsync();
        return term;
    }
}
