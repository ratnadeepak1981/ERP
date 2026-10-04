using Microsoft.IdentityModel.Tokens;
using Security.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;

namespace Security.Services;

public class TokenService : ITokenService
{
    private readonly RSA _rsa;

    public TokenService(RsaPrivateKeyLoader keyLoader)
    {
        // 1. Fix the method call to load the PRIVATE key needed for signing tokens
        if (!keyLoader.TryLoadPrivateKey())
        {
            throw new InvalidOperationException(
                "RSA private key could not be loaded from SecureVault.");
        }

        // 2. Load the companion public key to ensure the loader state is synchronized
        keyLoader.TryLoadPublicKey();

        // 3. Extract the loaded signing engine reference safely
        _rsa = keyLoader.GetRsa()
            ?? throw new InvalidOperationException(
                "RSA private key is unavailable.");
    }


    public string GenerateAccessToken(
        Guid userId,
        Guid? tenantId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString())
        };

        if (tenantId.HasValue)
        {
            claims.Add(
                new Claim("tenant_id", tenantId.Value.ToString()));
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

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));
    }
}
