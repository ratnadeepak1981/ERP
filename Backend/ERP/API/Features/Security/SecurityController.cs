using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using Security.Interfaces;
using Security.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
public class SecurityController : ControllerBase
{
    private readonly SecurityService _securityService;
    private readonly ITokenService _tokenService;
    private readonly RsaPrivateKeyLoader _keyLoader;
    private readonly ITenantService _tenantService;

    public SecurityController(
        SecurityService securityService,
        ITokenService tokenService,
        RsaPrivateKeyLoader keyLoader,
        ITenantService tenantService)
    {
        _securityService = securityService;
        _tokenService = tokenService;
        _keyLoader = keyLoader;
        _tenantService = tenantService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_securityService.GetStatus());
    }

    [HttpGet("diagnostic")]
    public IActionResult RunDiagnostic()
    {
        var results = new List<object>();

        // Test 1 - SecureVault path configuration check
        string privateKeyPath = _keyLoader.GetPrivateKeyPath();
        bool vaultPathAvailable =
            !string.IsNullOrWhiteSpace(privateKeyPath);

        results.Add(new
        {
            step = 1,
            test = "SecureVault path",
            status = vaultPathAvailable ? "PASS" : "FAIL",
            message = vaultPathAvailable
                ? "SecureVault path is configured"
                : "SecureVault path is unavailable"
        });

        if (!vaultPathAvailable)
            return Ok(results);

        // Test 2 - Private key file lookup on disk
        bool privateKeyFound =
            _keyLoader.IsPrivateKeyFound();

        results.Add(new
        {
            step = 2,
            test = "RSA private key existence",
            status = privateKeyFound ? "PASS" : "FAIL",
            message = privateKeyFound
                ? "RSA private key file found"
                : "RSA private key file not found"
        });

        if (!privateKeyFound)
            return Ok(results);

        // Test 3 - Private key loading pass
        bool privateKeyLoaded =
            _keyLoader.TryLoadPrivateKey();

        results.Add(new
        {
            step = 3,
            test = "RSA private key loading",
            status = privateKeyLoaded ? "PASS" : "FAIL",
            message = privateKeyLoaded
                ? "RSA private key loaded successfully"
                : "RSA private key failed to load"
        });

        if (!privateKeyLoaded)
            return Ok(results);

        // Test 4 - Private key parameter structure validation
        bool privateKeyValid =
            _keyLoader.IsPrivateKeyValid();

        var rsaPrivate =
            _keyLoader.GetRsa();

        results.Add(new
        {
            step = 4,
            test = "RSA private key parameters",
            status = privateKeyValid ? "PASS" : "FAIL",
            message = privateKeyValid
                ? $"RSA {rsaPrivate!.KeySize}-bit private key structure is valid"
                : "RSA private key structure is invalid"
        });

        if (!privateKeyValid)
            return Ok(results);

        // Test 5 - Public key file lookup on disk
        bool publicKeyFound =
            _keyLoader.IsPublicKeyFound();

        results.Add(new
        {
            step = 5,
            test = "RSA public key existence",
            status = publicKeyFound ? "PASS" : "FAIL",
            message = publicKeyFound
                ? "RSA public key file found"
                : "RSA public key file not found"
        });

        if (!publicKeyFound)
            return Ok(results);

        // Test 6 - Public key loading pass
        bool publicKeyLoaded =
            _keyLoader.TryLoadPublicKey();

        results.Add(new
        {
            step = 6,
            test = "RSA public key loading",
            status = publicKeyLoaded ? "PASS" : "FAIL",
            message = publicKeyLoaded
                ? "RSA public key loaded successfully"
                : "RSA public key failed to load"
        });

        if (!publicKeyLoaded)
            return Ok(results);

        // Test 7 - Public key parameter checks
        bool publicKeyValid =
            _keyLoader.IsPublicKeyValid();

        results.Add(new
        {
            step = 7,
            test = "RSA public key parameters",
            status = publicKeyValid ? "PASS" : "FAIL",
            message = publicKeyValid
                ? "RSA public key structure validation passed"
                : "RSA public key structure is invalid"
        });

        if (!publicKeyValid)
            return Ok(results);

        // Test 8 - Asymmetric challenge verification
        bool keyPairValid =
            _keyLoader.VerifyPrivatePublicPair();

        results.Add(new
        {
            step = 8,
            test = "Cryptographic signature validation challenge",
            status = keyPairValid ? "PASS" : "FAIL",
            message = keyPairValid
                ? "Private key signature matched public key verification rules safely"
                : "Asymmetric cryptographic key pair mismatch detected"
        });

        if (!keyPairValid)
            return Ok(results);

        // Test 9 - Functional RS256 JWT Generation
        try
        {
            string token =
                _tokenService.GenerateAccessToken(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    new[] { "DiagnosticUser" },
                    new[] { "Security.Diagnostic" });

            bool tokenGenerated =
                !string.IsNullOrWhiteSpace(token);

            results.Add(new
            {
                step = 9,
                test = "RS256 JWT generation framework check",
                status = tokenGenerated ? "PASS" : "FAIL",
                message = tokenGenerated
                    ? "RS256 isolation token signed and generated successfully"
                    : "Token generation returned an empty payload stream"
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                step = 9,
                test = "RS256 JWT generation framework check",
                status = "FAIL",
                message =
                    $"JWT generation engine threw a critical error: {ex.Message}"
            });
        }

        return Ok(results);
    }

    [HttpGet("jwt-diagnostic")]
    public IActionResult JwtDiagnostic()
    {
        try
        {
            Guid userId = Guid.NewGuid();
            Guid tenantId = Guid.NewGuid();

            var roles = new[]
            {
                "TenantAdmin"
            };

            var permissions = new[]
            {
                "Product.Read",
                "Product.Write"
            };

            string token =
                _tokenService.GenerateAccessToken(
                    userId,
                    tenantId,
                    roles,
                    permissions);

            return Ok(new
            {
                status = "PASS",
                message = "Real RS256 JWT generated successfully",
                algorithm = "RS256",
                issuer = "CSharpAuthServer",
                audience = "ERP",
                userId,
                tenantId,
                roles,
                permissions,
                token
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
    }

    [HttpPost("login-test")]
    public IActionResult LoginTest(
        [FromQuery] string username,
        [FromQuery] string password)
    {
        if (username != "admin" || password != "123")
        {
            return Unauthorized(new
            {
                status = "FAIL",
                message = "Invalid test credentials"
            });
        }

        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        var roles = new[]
        {
            "TenantAdmin"
        };

        var permissions = new[]
        {
            "Product.Read",
            "Product.Write"
        };

        string token =
            _tokenService.GenerateAccessToken(
                userId,
                tenantId,
                roles,
                permissions);

        return Ok(new
        {
            status = "PASS",
            message = "Test authentication successful",
            username,
            algorithm = "RS256",
            issuer = "CSharpAuthServer",
            audience = "ERP",
            token
        });
    }

    [HttpPost("jwt-wrong-issuer-test")]
    public IActionResult JwtWrongIssuerTest()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, "TenantAdmin"),
            new("permission", "Product.Read")
        };

        var rsa =
            _keyLoader.GetRsa()
            ?? throw new InvalidOperationException(
                "RSA private key is unavailable.");

        var credentials =
            new SigningCredentials(
                new RsaSecurityKey(rsa),
                SecurityAlgorithms.RsaSha256);

        var token =
            new JwtSecurityToken(
                issuer: "WRONG-ISSUER",
                audience: "ERP",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credentials);

        return Ok(new
        {
            status = "PASS",
            test = "Wrong issuer",
            issuer = "WRONG-ISSUER",
            audience = "ERP",
            algorithm = "RS256",
            token
        });
    }

    [HttpPost("tenant-registration-test")]
    public IActionResult TenantRegistrationTest(
        [FromBody] CreateTenantRequest request)
    {
        TenantRegistrationResult result =
            _tenantService.RegisterTenant(request);

        return Ok(new
        {
            status = "PASS",
            message = "Tenant registration use case completed",
            tenant = result.Tenant,
            subscription = result.Subscription
        });
    }
}