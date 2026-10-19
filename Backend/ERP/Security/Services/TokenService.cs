using Microsoft.IdentityModel.Tokens;
using Security.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Security.Services;

public class TokenService : ITokenService
{
    private readonly RSA _rsa;

    public TokenService(RsaPrivateKeyLoader keyLoader)
    {
        // 1. Load the PRIVATE key needed for signing tokens
        if (!keyLoader.TryLoadPrivateKey())
        {
            throw new InvalidOperationException(
                "RSA private key could not be loaded from SecureVault.");
        }

        // 2. Load the companion public key
        keyLoader.TryLoadPublicKey();

        // 3. Extract the loaded signing engine
        _rsa = keyLoader.GetRsa()
            ?? throw new InvalidOperationException(
                "RSA private key is unavailable.");
    }

    public string GenerateAccessToken(
        Guid userId,
        string username,
        Guid? tenantId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Name, username)
        };

        if (tenantId.HasValue)
        {
            claims.Add(
                new Claim(
                    "tenant_id",
                    tenantId.Value.ToString()));
        }

        claims.AddRange(
            roles.Select(role =>
                new Claim(ClaimTypes.Role, role)));

        claims.AddRange(
            permissions.Select(permission =>
                new Claim("permission", permission)));

        var credentials = new SigningCredentials(
            new RsaSecurityKey(_rsa),
            SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "CSharpAuthServer",
            audience: "ERP",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    // Explicit allowlist for service-to-service machine tokens
    private static readonly HashSet<string> AllowedServiceRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Service Agent"
    };

    private static readonly HashSet<string> AllowedServicePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "AI.READINESS"
    };

    public string GenerateServiceToken(
        string serviceName,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        TimeSpan? lifetime = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name must be provided.", nameof(serviceName));
        }

        // Validate token lifetime bounds
        if (lifetime.HasValue)
        {
            if (lifetime.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime), "Service token lifetime must be strictly greater than zero.");
            }
            if (lifetime.Value > TimeSpan.FromMinutes(30))
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime), "Service token lifetime cannot exceed 30 minutes.");
            }
        }

        // Restrict roles and permissions strictly to approved allowlists
        var roleList = roles?.ToList() ?? new List<string>();
        foreach (var role in roleList)
        {
            if (!AllowedServiceRoles.Contains(role))
            {
                throw new UnauthorizedAccessException($"Service role '{role}' is not permitted for service tokens.");
            }
        }

        var permissionList = permissions?.ToList() ?? new List<string>();
        foreach (var permission in permissionList)
        {
            if (!AllowedServicePermissions.Contains(permission))
            {
                throw new UnauthorizedAccessException($"Service permission '{permission}' is not permitted for service tokens.");
            }
        }

        // Generates deterministic GUID namespace for service subjects to satisfy GUID-parsing consumers
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"service:{serviceName}"));
        var serviceGuid = new Guid(hash);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, serviceGuid.ToString()),
            new(ClaimTypes.Name, $"service:{serviceName}"),
            new("client_id", serviceName),
            new("identity_type", "service")
        };

        claims.AddRange(
            roleList.Select(role =>
                new Claim(ClaimTypes.Role, role)));

        claims.AddRange(
            permissionList.Select(permission =>
                new Claim("permission", permission)));

        var credentials = new SigningCredentials(
            new RsaSecurityKey(_rsa),
            SecurityAlgorithms.RsaSha256);

        var expiry = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5));

        var token = new JwtSecurityToken(
            issuer: "CSharpAuthServer",
            audience: "ERP",
            claims: claims,
            expires: expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));
    }
}