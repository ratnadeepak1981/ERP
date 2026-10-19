namespace ERP.AI.Infrastructure.Core.Entities;

public class AIProviderKeyReference : AIAuditable
{
    public Guid Id { get; private set; }

    public Guid AIProviderId { get; private set; }

    public string KeyReferenceCode { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string Environment { get; private set; } = "Production";

    public bool IsActive { get; private set; } = true;

    // Concurrency Token
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public AIProvider? Provider { get; private set; }

    private AIProviderKeyReference() { }

    public static AIProviderKeyReference Create(
        Guid id,
        Guid providerId,
        string keyReferenceCode,
        string displayName,
        string environment = "Production",
        bool isActive = true)
    {
        if (providerId == Guid.Empty)
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));

        if (string.IsNullOrWhiteSpace(keyReferenceCode))
            throw new ArgumentException("Key reference code is required.", nameof(keyReferenceCode));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(environment))
            throw new ArgumentException("Environment is required.", nameof(environment));

        return new AIProviderKeyReference
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            AIProviderId = providerId,
            KeyReferenceCode = keyReferenceCode.Trim(),
            DisplayName = displayName.Trim(),
            Environment = environment.Trim(),
            IsActive = isActive
        };
    }

    public void Update(string displayName, string environment, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(environment))
            throw new ArgumentException("Environment is required.", nameof(environment));

        DisplayName = displayName.Trim();
        Environment = environment.Trim();
        IsActive = isActive;
    }
}
