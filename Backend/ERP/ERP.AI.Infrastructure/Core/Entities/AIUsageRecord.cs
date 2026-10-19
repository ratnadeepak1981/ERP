namespace ERP.AI.Infrastructure.Core.Entities;

public class AIUsageRecord
{
    public Guid Id { get; private set; }

    public Guid? TenantId { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public Guid AIProviderId { get; private set; }

    public Guid AIModelId { get; private set; }

    public Guid? AIProviderKeyReferenceId { get; private set; }

    public string? AIApplicationCode { get; private set; }

    public string? AIFeatureCode { get; private set; }

    public string? PromptVersion { get; private set; }

    // Token telemetry reported by provider or local estimator
    public int? InputTokens { get; private set; }

    public int? OutputTokens { get; private set; }

    public int? TotalTokens { get; private set; }

    public int? CachedInputTokens { get; private set; }

    public int? ReasoningTokens { get; private set; }

    // Source: "ProviderReported", "LocalEstimated", "Unknown"
    public string UsageSource { get; private set; } = "ProviderReported";

    // Status: "Success", "Failed", "Timeout", "RateLimited"
    public string Status { get; private set; } = "Success";

    public long? LatencyMilliseconds { get; private set; }

    public decimal? EstimatedCost { get; private set; }

    public string? CurrencyCode { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    // Navigations
    public AIProvider? Provider { get; private set; }

    public AIModel? Model { get; private set; }

    public AIProviderKeyReference? KeyReference { get; private set; }

    private AIUsageRecord() { }

    public static AIUsageRecord Create(
        Guid id,
        Guid? tenantId,
        string correlationId,
        Guid providerId,
        Guid modelId,
        Guid? keyReferenceId,
        string? applicationCode,
        string? featureCode,
        string? promptVersion,
        int? inputTokens,
        int? outputTokens,
        int? totalTokens,
        int? cachedInputTokens,
        int? reasoningTokens,
        string usageSource,
        string status,
        long? latencyMilliseconds,
        decimal? estimatedCost,
        string? currencyCode,
        DateTime createdAtUtc,
        DateTime? completedAtUtc = null)
    {
        if (providerId == Guid.Empty)
            throw new ArgumentException("Provider ID cannot be empty.", nameof(providerId));

        if (modelId == Guid.Empty)
            throw new ArgumentException("Model ID cannot be empty.", nameof(modelId));

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation ID is required.", nameof(correlationId));

        return new AIUsageRecord
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            TenantId = tenantId,
            CorrelationId = correlationId.Trim(),
            AIProviderId = providerId,
            AIModelId = modelId,
            AIProviderKeyReferenceId = keyReferenceId,
            AIApplicationCode = applicationCode?.Trim(),
            AIFeatureCode = featureCode?.Trim(),
            PromptVersion = promptVersion?.Trim(),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens ?? (inputTokens.HasValue && outputTokens.HasValue ? inputTokens.Value + outputTokens.Value : null),
            CachedInputTokens = cachedInputTokens,
            ReasoningTokens = reasoningTokens,
            UsageSource = string.IsNullOrWhiteSpace(usageSource) ? "ProviderReported" : usageSource.Trim(),
            Status = string.IsNullOrWhiteSpace(status) ? "Success" : status.Trim(),
            LatencyMilliseconds = latencyMilliseconds,
            EstimatedCost = estimatedCost,
            CurrencyCode = currencyCode?.Trim().ToUpperInvariant(),
            CreatedAtUtc = createdAtUtc == default ? DateTime.UtcNow : createdAtUtc,
            CompletedAtUtc = completedAtUtc
        };
    }
}
