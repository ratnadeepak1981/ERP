namespace ERP.AI.Infrastructure.Core.Entities;

public class AIProvider : AIAuditable
{
    public Guid Id { get; private set; }

    public string ProviderCode { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    // Optimistic Concurrency Token
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation collections
    public ICollection<AIModel> Models { get; private set; } = new List<AIModel>();

    public ICollection<AIProviderKeyReference> KeyReferences { get; private set; } = new List<AIProviderKeyReference>();

    private AIProvider() { }

    public static AIProvider Create(
        Guid id,
        string providerCode,
        string displayName,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
            throw new ArgumentException("Provider code is required.", nameof(providerCode));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        return new AIProvider
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            ProviderCode = providerCode.Trim().ToUpperInvariant(),
            DisplayName = displayName.Trim(),
            IsActive = isActive
        };
    }

    public void Update(string displayName, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        DisplayName = displayName.Trim();
        IsActive = isActive;
    }
}
