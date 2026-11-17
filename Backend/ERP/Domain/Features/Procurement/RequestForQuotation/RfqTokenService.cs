using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Procurement.RequestForQuotation;

public interface IRfqTokenService
{
    (string PlaintextToken, string TokenHash) GenerateToken();
    string ComputeHash(string plaintextToken);
    Task<RFQSupplier?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}

public class RfqTokenService : IRfqTokenService
{
    private readonly DomainDbContext _dbContext;

    public RfqTokenService(DomainDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public (string PlaintextToken, string TokenHash) GenerateToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        // URL-safe base64 string
        var plaintextToken = Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var tokenHash = ComputeHash(plaintextToken);

        return (plaintextToken, tokenHash);
    }

    public string ComputeHash(string plaintextToken)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken))
            throw new ArgumentException("Plaintext token cannot be empty.", nameof(plaintextToken));

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<RFQSupplier?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = ComputeHash(token);

        var rfqSupplier = await _dbContext.RFQSuppliers
            .Include(s => s.RequestForQuotation)
                .ThenInclude(r => r!.Lines)
            .FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        if (rfqSupplier == null)
            return null;

        // Check expiration
        if (rfqSupplier.TokenExpiresAt.HasValue && rfqSupplier.TokenExpiresAt.Value < DateTime.UtcNow)
            return null;

        return rfqSupplier;
    }
}
