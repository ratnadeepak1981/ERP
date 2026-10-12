namespace ERP.AI.Infrastructure.Core.Entities;

public class AIModel : AIAuditable
{
    public Guid Id { get; private set; }

    public Guid AIProviderId { get; private set; }

    public string ModelCode { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    // Capabilities (nullable where support is unverified)
    public bool? SupportsChat { get; private set; }

    public bool? SupportsStructuredOutput { get; private set; }

    public bool? SupportsToolCalling { get; private set; }

    public bool? SupportsEmbeddings { get; private set; }

    public bool? SupportsTokenReporting { get; private set; }

    // Concurrency Token
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public AIProvider? Provider { get; private set; }

    private AIModel() { }

    public static AIModel Create(
        Guid id,
        Guid providerId,
        string modelCode,
        string displayName,
        bool isActive = true,
        bool? supportsChat = true,
        bool? supportsStructuredOutput = null,
        bool? supportsToolCalling = null,
        bool? supportsEmbeddings = null,
        bool? supportsTokenReporting = null)
    {
        if (providerId == Guid.Empty)
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));

        if (string.IsNullOrWhiteSpace(modelCode))
            throw new ArgumentException("Model code is required.", nameof(modelCode));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        return new AIModel
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            AIProviderId = providerId,
            ModelCode = modelCode.Trim(),
            DisplayName = displayName.Trim(),
            IsActive = isActive,
            SupportsChat = supportsChat,
            SupportsStructuredOutput = supportsStructuredOutput,
            SupportsToolCalling = supportsToolCalling,
            SupportsEmbeddings = supportsEmbeddings,
            SupportsTokenReporting = supportsTokenReporting
        };
    }

    public void Update(
        string displayName,
        bool isActive,
        bool? supportsChat,
        bool? supportsStructuredOutput,
        bool? supportsToolCalling,
        bool? supportsEmbeddings,
        bool? supportsTokenReporting)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        DisplayName = displayName.Trim();
        IsActive = isActive;
        SupportsChat = supportsChat;
        SupportsStructuredOutput = supportsStructuredOutput;
        SupportsToolCalling = supportsToolCalling;
        SupportsEmbeddings = supportsEmbeddings;
        SupportsTokenReporting = supportsTokenReporting;
    }
}
