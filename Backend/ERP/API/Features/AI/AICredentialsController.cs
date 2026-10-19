using System.Security.Claims;
using API.Common;
using API.Security.Authorization;
using ERP.AI.Infrastructure.Core.Entities;
using ERP.AI.Infrastructure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Features.AI;

public record SaveAIProviderCredentialRequest(
    string ProviderCode,
    string KeyReferenceCode,
    string EncryptedSecret,
    string EncryptionAlgorithm = "RSA-4096-OAEP-SHA256",
    int KeyVersion = 1,
    string Environment = "Production"
);

public record AIProviderCredentialResponse(
    Guid Id,
    string ProviderCode,
    string KeyReferenceCode,
    string EncryptedSecret,
    string EncryptionAlgorithm,
    int KeyVersion,
    string Environment,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

[ApiController]
[Route("api/ai/credentials")]
[Authorize]
public class AICredentialsController : BaseApiController
{
    private readonly AIDbContext _aiDbContext;

    public AICredentialsController(AIDbContext aiDbContext)
    {
        _aiDbContext = aiDbContext;
    }

    private Guid GetActorUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (Guid.TryParse(sub, out var userId))
            return userId;

        return Guid.Empty;
    }

    [HttpPost]
    [RequirePermission("AI.READINESS")]
    public async Task<IActionResult> SaveCredential(
        [FromBody] SaveAIProviderCredentialRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequestResponse("Request body cannot be null.");

        if (string.IsNullOrWhiteSpace(request.ProviderCode))
            return BadRequestResponse("Provider code is required.");

        if (string.IsNullOrWhiteSpace(request.KeyReferenceCode))
            return BadRequestResponse("Key reference code is required.");

        if (string.IsNullOrWhiteSpace(request.EncryptedSecret))
            return BadRequestResponse("Encrypted secret cannot be empty.");

        string providerCodeNorm = request.ProviderCode.Trim().ToUpperInvariant();
        string keyRefNorm = request.KeyReferenceCode.Trim();
        string envNorm = string.IsNullOrWhiteSpace(request.Environment) ? "Production" : request.Environment.Trim();

        var provider = await _aiDbContext.AIProviders
            .FirstOrDefaultAsync(p => p.ProviderCode == providerCodeNorm, cancellationToken);

        if (provider == null)
        {
            return NotFoundResponse($"AI Provider '{providerCodeNorm}' was not found in catalog.");
        }

        var existingCredential = await _aiDbContext.AIProviderCredentials
            .FirstOrDefaultAsync(c =>
                c.AIProviderId == provider.Id &&
                c.KeyReferenceCode == keyRefNorm &&
                c.Environment == envNorm, cancellationToken);

        Guid actorId = GetActorUserId();

        if (existingCredential != null)
        {
            existingCredential.UpdateCredential(
                request.EncryptedSecret,
                request.EncryptionAlgorithm,
                request.KeyVersion,
                isActive: true
            );
            existingCredential.MarkUpdated(actorId, DateTime.UtcNow);
        }
        else
        {
            var newCredential = AIProviderCredential.Create(
                Guid.NewGuid(),
                provider.Id,
                keyRefNorm,
                request.EncryptedSecret,
                request.EncryptionAlgorithm,
                request.KeyVersion,
                envNorm,
                isActive: true
            );
            newCredential.MarkCreated(actorId, DateTime.UtcNow);
            await _aiDbContext.AIProviderCredentials.AddAsync(newCredential, cancellationToken);
        }

        await _aiDbContext.SaveChangesAsync(cancellationToken);

        return SuccessResponse(new
        {
            provider = providerCodeNorm,
            keyReferenceCode = keyRefNorm,
            environment = envNorm
        }, $"Credential for provider '{providerCodeNorm}' securely saved in AIDatabase.");
    }

    [HttpGet("{providerCode}")]
    [RequirePermission("AI.READINESS")]
    public async Task<IActionResult> GetCredential(
        [FromRoute] string providerCode,
        [FromQuery] string environment = "Production",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
            return BadRequestResponse("Provider code is required.");

        string providerCodeNorm = providerCode.Trim().ToUpperInvariant();
        string envNorm = string.IsNullOrWhiteSpace(environment) ? "Production" : environment.Trim();

        var provider = await _aiDbContext.AIProviders
            .FirstOrDefaultAsync(p => p.ProviderCode == providerCodeNorm, cancellationToken);

        if (provider == null)
        {
            return NotFoundResponse($"AI Provider '{providerCodeNorm}' was not found in catalog.");
        }

        var credential = await _aiDbContext.AIProviderCredentials
            .Where(c => c.AIProviderId == provider.Id && c.Environment == envNorm && c.IsActive)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (credential == null)
        {
            return NotFoundResponse($"No active credential found for provider '{providerCodeNorm}' in environment '{envNorm}'.");
        }

        return SuccessResponse(new AIProviderCredentialResponse(
            credential.Id,
            providerCodeNorm,
            credential.KeyReferenceCode,
            credential.EncryptedSecret,
            credential.EncryptionAlgorithm,
            credential.KeyVersion,
            credential.Environment,
            credential.IsActive,
            credential.CreatedAtUtc,
            credential.UpdatedAtUtc
        ));
    }
}
