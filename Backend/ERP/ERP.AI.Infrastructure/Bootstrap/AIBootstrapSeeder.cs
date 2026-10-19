using ERP.AI.Infrastructure.Core.Entities;
using ERP.AI.Infrastructure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Bootstrap;

public static class AIBootstrapSeeder
{
    // Deterministic IDs for bootstrap catalog
    public static readonly Guid OpenAiProviderId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid AnthropicProviderId = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid AzureOpenAiProviderId = Guid.Parse("a1000000-0000-0000-0000-000000000003");
    public static readonly Guid GroqProviderId = Guid.Parse("a1000000-0000-0000-0000-000000000004");
    public static readonly Guid GeminiProviderId = Guid.Parse("a1000000-0000-0000-0000-000000000005");

    public static readonly Guid Gpt4oModelId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    public static readonly Guid Gpt4oMiniModelId = Guid.Parse("a2000000-0000-0000-0000-000000000002");
    public static readonly Guid ClaudeSonnetModelId = Guid.Parse("a2000000-0000-0000-0000-000000000003");

    public static void Seed(AIDbContext context)
    {
        SeedProviders(context);
        context.SaveChanges();

        SeedModels(context);
        context.SaveChanges();
    }

    public static async Task SeedAsync(AIDbContext context, CancellationToken cancellationToken = default)
    {
        await SeedProvidersAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await SeedModelsAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void SeedProviders(AIDbContext context)
    {
        SeedProviderIfMissing(context, OpenAiProviderId, "OPENAI", "OpenAI");
        SeedProviderIfMissing(context, AnthropicProviderId, "ANTHROPIC", "Anthropic");
        SeedProviderIfMissing(context, AzureOpenAiProviderId, "AZURE_OPENAI", "Azure OpenAI");
        SeedProviderIfMissing(context, GroqProviderId, "GROQ", "Groq");
        SeedProviderIfMissing(context, GeminiProviderId, "GEMINI", "Google Gemini");
    }

    private static async Task SeedProvidersAsync(AIDbContext context, CancellationToken cancellationToken)
    {
        await SeedProviderIfMissingAsync(context, OpenAiProviderId, "OPENAI", "OpenAI", cancellationToken);
        await SeedProviderIfMissingAsync(context, AnthropicProviderId, "ANTHROPIC", "Anthropic", cancellationToken);
        await SeedProviderIfMissingAsync(context, AzureOpenAiProviderId, "AZURE_OPENAI", "Azure OpenAI", cancellationToken);
        await SeedProviderIfMissingAsync(context, GroqProviderId, "GROQ", "Groq", cancellationToken);
        await SeedProviderIfMissingAsync(context, GeminiProviderId, "GEMINI", "Google Gemini", cancellationToken);
    }

    private static void SeedProviderIfMissing(AIDbContext context, Guid id, string code, string name)
    {
        if (context.AIProviders.Any(p => p.ProviderCode == code || p.Id == id))
            return;

        context.AIProviders.Add(AIProvider.Create(id, code, name, isActive: true));
    }

    private static async Task SeedProviderIfMissingAsync(AIDbContext context, Guid id, string code, string name, CancellationToken cancellationToken)
    {
        if (await context.AIProviders.AnyAsync(p => p.ProviderCode == code || p.Id == id, cancellationToken))
            return;

        await context.AIProviders.AddAsync(AIProvider.Create(id, code, name, isActive: true), cancellationToken);
    }

    private static void SeedModels(AIDbContext context)
    {
        SeedModelIfMissing(
            context,
            Gpt4oModelId,
            OpenAiProviderId,
            "gpt-4o",
            "GPT-4o (Omni)",
            supportsChat: true,
            supportsStructuredOutput: true,
            supportsToolCalling: true,
            supportsEmbeddings: false,
            supportsTokenReporting: true);

        SeedModelIfMissing(
            context,
            Gpt4oMiniModelId,
            OpenAiProviderId,
            "gpt-4o-mini",
            "GPT-4o Mini",
            supportsChat: true,
            supportsStructuredOutput: true,
            supportsToolCalling: true,
            supportsEmbeddings: false,
            supportsTokenReporting: true);

        SeedModelIfMissing(
            context,
            ClaudeSonnetModelId,
            AnthropicProviderId,
            "claude-3-5-sonnet",
            "Claude 3.5 Sonnet",
            supportsChat: true,
            supportsStructuredOutput: true,
            supportsToolCalling: true,
            supportsEmbeddings: false,
            supportsTokenReporting: true);
    }

    private static async Task SeedModelsAsync(AIDbContext context, CancellationToken cancellationToken)
    {
        await SeedModelIfMissingAsync(
            context,
            Gpt4oModelId,
            OpenAiProviderId,
            "gpt-4o",
            "GPT-4o (Omni)",
            supportsChat: true,
            supportsStructuredOutput: true,
            supportsToolCalling: true,
            supportsEmbeddings: false,
            supportsTokenReporting: true,
            cancellationToken);

        await SeedModelIfMissingAsync(
            context,
            Gpt4oMiniModelId,
            OpenAiProviderId,
            "gpt-4o-mini",
            "GPT-4o Mini",
            supportsChat: true,
            supportsStructuredOutput: true,
            supportsToolCalling: true,
            supportsEmbeddings: false,
            supportsTokenReporting: true,
            cancellationToken);

        await SeedModelIfMissingAsync(
            context,
            ClaudeSonnetModelId,
            AnthropicProviderId,
            "claude-3-5-sonnet",
            "Claude 3.5 Sonnet",
            supportsChat: true,
            supportsStructuredOutput: true,
            supportsToolCalling: true,
            supportsEmbeddings: false,
            supportsTokenReporting: true,
            cancellationToken);
    }

    private static void SeedModelIfMissing(
        AIDbContext context,
        Guid id,
        Guid providerId,
        string modelCode,
        string displayName,
        bool? supportsChat,
        bool? supportsStructuredOutput,
        bool? supportsToolCalling,
        bool? supportsEmbeddings,
        bool? supportsTokenReporting)
    {
        if (context.AIModels.Any(m => m.Id == id || (m.AIProviderId == providerId && m.ModelCode == modelCode)))
            return;

        context.AIModels.Add(AIModel.Create(
            id,
            providerId,
            modelCode,
            displayName,
            isActive: true,
            supportsChat: supportsChat,
            supportsStructuredOutput: supportsStructuredOutput,
            supportsToolCalling: supportsToolCalling,
            supportsEmbeddings: supportsEmbeddings,
            supportsTokenReporting: supportsTokenReporting));
    }

    private static async Task SeedModelIfMissingAsync(
        AIDbContext context,
        Guid id,
        Guid providerId,
        string modelCode,
        string displayName,
        bool? supportsChat,
        bool? supportsStructuredOutput,
        bool? supportsToolCalling,
        bool? supportsEmbeddings,
        bool? supportsTokenReporting,
        CancellationToken cancellationToken)
    {
        if (await context.AIModels.AnyAsync(m => m.Id == id || (m.AIProviderId == providerId && m.ModelCode == modelCode), cancellationToken))
            return;

        await context.AIModels.AddAsync(AIModel.Create(
            id,
            providerId,
            modelCode,
            displayName,
            isActive: true,
            supportsChat: supportsChat,
            supportsStructuredOutput: supportsStructuredOutput,
            supportsToolCalling: supportsToolCalling,
            supportsEmbeddings: supportsEmbeddings,
            supportsTokenReporting: supportsTokenReporting), cancellationToken);
    }
}
