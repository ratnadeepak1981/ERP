namespace ERP.AI.Infrastructure.Core.Entities;

public class TenantAIConfiguration : AIAuditable
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AIProviderId { get; private set; }

    public Guid AIModelId { get; private set; }

    public Guid? AIProviderKeyReferenceId { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    // Concurrency Token
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigations
    public AIProvider? Provider { get; private set; }

    public AIModel? Model { get; private set; }

    public AIProviderKeyReference? KeyReference { get; private set; }

    private TenantAIConfiguration() { }

    public static TenantAIConfiguration Create(
        Guid id,
        Guid tenantId,
        Guid providerId,
        Guid modelId,
        Guid? keyReferenceId = null,
        bool isEnabled = true)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));

        if (providerId == Guid.Empty)
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));

        if (modelId == Guid.Empty)
            throw new ArgumentException("Model ID cannot be empty.", nameof(modelId));

        return new TenantAIConfiguration
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            TenantId = tenantId,
            AIProviderId = providerId,
            AIModelId = modelId,
            AIProviderKeyReferenceId = keyReferenceId,
            IsEnabled = isEnabled
        };
    }

    public void Update(
        Guid providerId,
        Guid modelId,
        Guid? keyReferenceId,
        bool isEnabled)
    {
        if (providerId == Guid.Empty)
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));

        if (modelId == Guid.Empty)
            throw new ArgumentException("Model ID cannot be empty.", nameof(modelId));

        AIProviderId = providerId;
        AIModelId = modelId;
        AIProviderKeyReferenceId = keyReferenceId;
        IsEnabled = isEnabled;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
