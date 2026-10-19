using System;
using System.Linq;
using System.Threading.Tasks;
using ERP.AI.Infrastructure.Bootstrap;
using ERP.AI.Infrastructure.Core.Entities;
using ERP.AI.Infrastructure.Infrastructure.Persistence;
using ERP.AI.Infrastructure.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests;

public class AIPersistenceUnitTests
{
    private static AIDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AIDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AIDbContext(options);
    }

    [Fact]
    public void AIProvider_CreateWithValidData_SetsPropertiesAndUpperCode()
    {
        var provider = AIProvider.Create(Guid.NewGuid(), "openai", "OpenAI");

        Assert.Equal("OPENAI", provider.ProviderCode);
        Assert.Equal("OpenAI", provider.DisplayName);
        Assert.True(provider.IsActive);
    }

    [Fact]
    public void AIProvider_CreateWithMissingCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AIProvider.Create(Guid.NewGuid(), "", "OpenAI"));
    }

    [Fact]
    public void AIModel_CreateWithValidData_SetsCapabilitiesCorrectly()
    {
        var providerId = Guid.NewGuid();
        var model = AIModel.Create(
            Guid.NewGuid(),
            providerId,
            "gpt-4o",
            "GPT-4o",
            isActive: true,
            supportsChat: true,
            supportsStructuredOutput: true,
            supportsToolCalling: true,
            supportsEmbeddings: false,
            supportsTokenReporting: true);

        Assert.Equal("gpt-4o", model.ModelCode);
        Assert.Equal(providerId, model.AIProviderId);
        Assert.True(model.SupportsChat);
        Assert.True(model.SupportsStructuredOutput);
        Assert.True(model.SupportsToolCalling);
        Assert.False(model.SupportsEmbeddings);
        Assert.True(model.SupportsTokenReporting);
    }

    [Fact]
    public void AIProviderKeyReference_DoesNotStoreRawSecrets_StoresMetadataOnly()
    {
        var providerId = Guid.NewGuid();
        var keyRef = AIProviderKeyReference.Create(
            Guid.NewGuid(),
            providerId,
            "OPENAI_PROD_VAULT_REF",
            "Production Vault Key Reference",
            "Production");

        Assert.Equal("OPENAI_PROD_VAULT_REF", keyRef.KeyReferenceCode);
        Assert.Equal("Production", keyRef.Environment);
        Assert.True(keyRef.IsActive);
    }

    [Fact]
    public void TenantAIConfiguration_ModelBelongingToDifferentProvider_ThrowsOnSaveChanges()
    {
        using var context = CreateInMemoryDbContext("TenantAIConfig_ConsistencyTest");

        var providerA = AIProvider.Create(Guid.NewGuid(), "PROVIDER_A", "Provider A");
        var providerB = AIProvider.Create(Guid.NewGuid(), "PROVIDER_B", "Provider B");
        var modelA = AIModel.Create(Guid.NewGuid(), providerA.Id, "model-a", "Model A");

        context.AIProviders.AddRange(providerA, providerB);
        context.AIModels.Add(modelA);
        context.SaveChanges();

        // Mismatched configuration: specifies provider B, but model A belongs to provider A
        var invalidConfig = TenantAIConfiguration.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            providerB.Id,
            modelA.Id);

        context.TenantAIConfigurations.Add(invalidConfig);

        var ex = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        Assert.Contains("AI model consistency violation", ex.Message);
    }

    [Fact]
    public void AIUsageRecord_CalculatesTotalTokensFromInputAndOutput_WhenNotSupplied()
    {
        var providerId = Guid.NewGuid();
        var modelId = Guid.NewGuid();

        var record = AIUsageRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "corr-123",
            providerId,
            modelId,
            null,
            "INVENTORY",
            "STOCK_PREDICT",
            "v1.0",
            inputTokens: 150,
            outputTokens: 50,
            totalTokens: null,
            cachedInputTokens: 20,
            reasoningTokens: 0,
            usageSource: "ProviderReported",
            status: "Success",
            latencyMilliseconds: 320,
            estimatedCost: 0.0012m,
            currencyCode: "USD",
            createdAtUtc: DateTime.UtcNow);

        Assert.Equal(200, record.TotalTokens);
        Assert.Equal(150, record.InputTokens);
        Assert.Equal(50, record.OutputTokens);
        Assert.Equal(20, record.CachedInputTokens);
        Assert.Equal("ProviderReported", record.UsageSource);
    }

    [Fact]
    public async Task AIBootstrapSeeder_IsIdempotent_CanRunMultipleTimesWithoutDuplicates()
    {
        using var context = CreateInMemoryDbContext("AIBootstrapSeeder_IdempotencyTest");

        // Run seed first time
        await AIBootstrapSeeder.SeedAsync(context);
        int initialProviderCount = await context.AIProviders.CountAsync();
        int initialModelCount = await context.AIModels.CountAsync();

        Assert.True(initialProviderCount >= 3);
        Assert.True(initialModelCount >= 3);

        // Run seed second time
        await AIBootstrapSeeder.SeedAsync(context);
        int secondProviderCount = await context.AIProviders.CountAsync();
        int secondModelCount = await context.AIModels.CountAsync();

        Assert.Equal(initialProviderCount, secondProviderCount);
        Assert.Equal(initialModelCount, secondModelCount);
    }

    [Fact]
    public void AIAuditSaveChangesInterceptor_AutomaticallyGeneratesAuditRecordForAIAuditableEntities()
    {
        var interceptor = new AIAuditSaveChangesInterceptor();
        var options = new DbContextOptionsBuilder<AIDbContext>()
            .UseInMemoryDatabase(databaseName: "AIAuditInterceptor_Test")
            .AddInterceptors(interceptor)
            .Options;

        using var context = new AIDbContext(options);

        var provider = AIProvider.Create(Guid.NewGuid(), "NEW_PROVIDER", "New Provider");
        context.AIProviders.Add(provider);
        context.SaveChanges();

        var auditRecord = context.AIAuditRecords.FirstOrDefault(a => a.EntityType == nameof(AIProvider));
        Assert.NotNull(auditRecord);
        Assert.Equal("EntityCreated", auditRecord.EventType);
        Assert.Equal(provider.Id.ToString(), auditRecord.EntityId);
        Assert.Equal("Success", auditRecord.Outcome);
        Assert.NotNull(auditRecord.SafeMetadataJson);
    }

    [Fact]
    public void PythonServiceOptions_MissingUrl_ThrowsInvalidOperationException()
    {
        var options = new ERP.AI.Infrastructure.Configuration.PythonServiceOptions
        {
            BaseUrl = ""
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate(isDevelopment: true));
        Assert.Contains("BaseUrl is required", ex.Message);
    }

    [Fact]
    public void PythonServiceOptions_DevelopmentAllowsLoopbackHttp()
    {
        var options = new ERP.AI.Infrastructure.Configuration.PythonServiceOptions
        {
            BaseUrl = "http://127.0.0.1:8000",
            TimeoutSeconds = 10
        };

        // Should not throw in Development
        options.Validate(isDevelopment: true);
    }

    [Fact]
    public void PythonServiceOptions_ProductionRejectsHttpAndLoopback()
    {
        var loopbackOptions = new ERP.AI.Infrastructure.Configuration.PythonServiceOptions
        {
            BaseUrl = "http://127.0.0.1:8000",
            TimeoutSeconds = 10
        };

        var ex1 = Assert.Throws<InvalidOperationException>(() => loopbackOptions.Validate(isDevelopment: false));
        Assert.Contains("strictly use HTTPS", ex1.Message);

        var loopbackHttps = new ERP.AI.Infrastructure.Configuration.PythonServiceOptions
        {
            BaseUrl = "https://127.0.0.1:8000",
            TimeoutSeconds = 10
        };

        var ex2 = Assert.Throws<InvalidOperationException>(() => loopbackHttps.Validate(isDevelopment: false));
        Assert.Contains("cannot target localhost or loopback", ex2.Message);
    }

    [Fact]
    public void PythonServiceOptions_ProductionAcceptsValidExternalHttps()
    {
        var options = new ERP.AI.Infrastructure.Configuration.PythonServiceOptions
        {
            BaseUrl = "https://ai-service.internal.erp.com",
            TimeoutSeconds = 15
        };

        // Should not throw in production
        options.Validate(isDevelopment: false);
    }

    [Fact]
    public void AddAIInfrastructure_WithoutAIDatabaseConnection_ThrowsClearErrorWhenDbContextRequested()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                ["PythonService:BaseUrl"] = "http://127.0.0.1:8000"
            })
            .Build();

        ERP.AI.Infrastructure.DependencyInjection.AIInfrastructureServiceCollectionExtensions.AddAIInfrastructure(services, config);
        var provider = services.BuildServiceProvider();

        Action resolve = () => { var ctx = provider.GetRequiredService<AIDbContext>(); };
        var ex = Assert.Throws<InvalidOperationException>(resolve);
        Assert.Contains("ConnectionStrings:AIDatabase", ex.Message);
    }

    [Fact]
    public async Task AIDatabase_Test_SchemaVerification_AppliedSuccessfully()
    {
        // Verifies the actual physical AIDatabase_Test migrated schema
        var options = new DbContextOptionsBuilder<AIDbContext>()
            .UseSqlServer(TestConstants.AIDbTest)
            .Options;

        using var context = new AIDbContext(options);
        
        // Assert we can query the applied migration tables
        var migrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20261012063639_InitialAICatalogAndAudit", migrations);

        // Run idempotent seed to verify table write capabilities
        await AIBootstrapSeeder.SeedAsync(context);

        var providers = await context.AIProviders.ToListAsync();
        Assert.True(providers.Count >= 3);
        Assert.Contains(providers, p => p.ProviderCode == "OPENAI");
    }

    [Fact]
    public void AIProviderCredential_CreateWithValidData_SetsPropertiesAndActiveStatus()
    {
        var providerId = Guid.NewGuid();
        var credential = AIProviderCredential.Create(
            Guid.NewGuid(),
            providerId,
            "GROQ_API_KEY",
            "encrypted_ciphertext_b64_payload",
            "RSA-4096-OAEP-SHA256",
            keyVersion: 1,
            environment: "Production",
            isActive: true);

        Assert.Equal(providerId, credential.AIProviderId);
        Assert.Equal("GROQ_API_KEY", credential.KeyReferenceCode);
        Assert.Equal("encrypted_ciphertext_b64_payload", credential.EncryptedSecret);
        Assert.Equal("RSA-4096-OAEP-SHA256", credential.EncryptionAlgorithm);
        Assert.Equal(1, credential.KeyVersion);
        Assert.Equal("Production", credential.Environment);
        Assert.True(credential.IsActive);
    }

    [Fact]
    public void AIProviderCredential_CreateWithEmptySecretOrKeyRef_ThrowsArgumentException()
    {
        var providerId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            AIProviderCredential.Create(Guid.NewGuid(), providerId, "", "valid_secret"));

        Assert.Throws<ArgumentException>(() =>
            AIProviderCredential.Create(Guid.NewGuid(), providerId, "VALID_REF", ""));
    }

    [Fact]
    public void AIProviderCredential_UpdateCredential_UpdatesValuesProperly()
    {
        var providerId = Guid.NewGuid();
        var credential = AIProviderCredential.Create(
            Guid.NewGuid(),
            providerId,
            "GEMINI_API_KEY",
            "original_ciphertext",
            "RSA-4096-OAEP-SHA256",
            keyVersion: 1);

        credential.UpdateCredential("new_rotated_ciphertext", "RSA-4096-OAEP-SHA256", 2, isActive: true);

        Assert.Equal("new_rotated_ciphertext", credential.EncryptedSecret);
        Assert.Equal(2, credential.KeyVersion);
    }

    [Fact]
    public void AIProviderCredential_InMemoryDb_PersistsAndRetrievesSuccessfully()
    {
        using var context = CreateInMemoryDbContext("AIProviderCredential_PersistenceTest");

        var provider = AIProvider.Create(Guid.NewGuid(), "GROQ", "Groq");
        context.AIProviders.Add(provider);
        context.SaveChanges();

        var credential = AIProviderCredential.Create(
            Guid.NewGuid(),
            provider.Id,
            "GROQ_API_KEY",
            "encrypted_secret_data_12345",
            "RSA-4096-OAEP-SHA256",
            1,
            "Production");

        context.AIProviderCredentials.Add(credential);
        context.SaveChanges();

        var retrieved = context.AIProviderCredentials.FirstOrDefault(c => c.AIProviderId == provider.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("encrypted_secret_data_12345", retrieved.EncryptedSecret);
        Assert.Equal("GROQ_API_KEY", retrieved.KeyReferenceCode);
    }
}


