using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;

using Security.Infrastructure.Persistence;
using Security.Interfaces;
using Security.Services;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

using SecurityLoginRequest = Security.Application.DTOs.LoginRequest;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
public class SecurityController : ControllerBase
{
    private readonly SecurityService _securityService;
    private readonly ITokenService _tokenService;
    private readonly RsaPrivateKeyLoader _keyLoader;
    private readonly ITenantService _tenantService;
    private readonly SecurityDbContext _securityDb;
    private readonly PasswordService _passwordService;

    public SecurityController(
        SecurityService securityService,
        ITokenService tokenService,
        RsaPrivateKeyLoader keyLoader,
        ITenantService tenantService,
        SecurityDbContext securityDb,
        PasswordService passwordService)
    {
        _securityService = securityService;
        _tokenService = tokenService;
        _keyLoader = keyLoader;
        _tenantService = tenantService;
        _securityDb = securityDb;
        _passwordService = passwordService;
    }

    // ============================================================
    // REAL LOGIN
    // ============================================================

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] SecurityLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                status = "FAIL",
                message = "Username and password are required."
            });
        }

        // --------------------------------------------------------
        // Find active user
        // --------------------------------------------------------

        var user = await _securityDb.Users
            .FirstOrDefaultAsync(x =>
                x.Username == request.Username &&
                x.IsActive);

        if (user == null)
        {
            return Unauthorized(new
            {
                status = "FAIL",
                message = "Invalid username or password."
            });
        }

        // --------------------------------------------------------
        // Verify password
        // --------------------------------------------------------

        bool passwordValid =
            _passwordService.VerifyPassword(
                request.Password,
                user.PasswordHash);

        if (!passwordValid)
        {
            return Unauthorized(new
            {
                status = "FAIL",
                message = "Invalid username or password."
            });
        }

        // --------------------------------------------------------
        // Load active user roles
        // --------------------------------------------------------

        var roles = await _securityDb.UserRoles
            .Where(x => x.UserId == user.Id)
            .Include(x => x.Role)
            .Where(x => x.Role.IsActive)
            .Select(x => x.Role.Name)
            .Distinct()
            .ToListAsync();

        // --------------------------------------------------------
        // Load active permissions through roles
        // --------------------------------------------------------

        var permissions = await _securityDb.UserRoles
            .Where(x => x.UserId == user.Id)
            .Include(x => x.Role)
                .ThenInclude(x => x.RolePermissions)
                    .ThenInclude(x => x.Permission)
            .SelectMany(x => x.Role.RolePermissions)
            .Where(x => x.Permission.IsActive)
            .Select(x => x.Permission.Code)
            .Distinct()
            .ToListAsync();

        // --------------------------------------------------------
        // Generate real RSA JWT
        // --------------------------------------------------------

        string token =
            _tokenService.GenerateAccessToken(
                user.Id,
                user.TenantId,
                roles,
                permissions);

        return Ok(new
        {
            status = "PASS",
            message = "Authentication successful.",
            userId = user.Id,
            username = user.Username,
            tenantId = user.TenantId,
            roles,
            permissions,
            algorithm = "RS256",
            issuer = "CSharpAuthServer",
            audience = "ERP",
            token
        });
    }

    // ============================================================
    // SECURITY STATUS
    // ============================================================

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_securityService.GetStatus());
    }

    // ============================================================
    // SECURITY / RSA DIAGNOSTIC
    // ============================================================

    [HttpGet("diagnostic")]
    public IActionResult RunDiagnostic()
    {
        var results = new List<object>();

        // Test 1 - SecureVault path configuration check
        string privateKeyPath =
            _keyLoader.GetPrivateKeyPath();

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

        // Test 2 - Private key file lookup
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

        // Test 3 - Private key loading
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

        // Test 4 - Private key validation
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

        // Test 5 - Public key file lookup
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

        // Test 6 - Public key loading
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

        // Test 7 - Public key validation
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

        // Test 8 - Private/public key pair verification
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

        // Test 9 - RS256 JWT generation
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

    // ============================================================
    // JWT DIAGNOSTIC
    // ============================================================

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

    // ============================================================
    // LOGIN TEST - LEGACY DIAGNOSTIC
    // ============================================================

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

    // ============================================================
    // WRONG ISSUER TEST
    // ============================================================

    [HttpPost("jwt-wrong-issuer-test")]
    public IActionResult JwtWrongIssuerTest()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                userId.ToString()),

            new(
                "tenant_id",
                tenantId.ToString()),

            new(
                ClaimTypes.Role,
                "TenantAdmin"),

            new(
                "permission",
                "Product.Read")
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

    // ============================================================
    // TENANT REGISTRATION TEST
    // ============================================================

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

    [Authorize]
    [HttpGet("protected-test")]
    public IActionResult ProtectedTest()
    {
        return Ok(new
        {
            status = "PASS",
            message = "JWT authentication successful.",
            userId = User.FindFirst(
                JwtRegisteredClaimNames.Sub)?.Value,
            username = User.Identity?.Name,
            roles = User.Claims
                .Where(x => x.Type == ClaimTypes.Role)
                .Select(x => x.Value)
                .ToList(),
            tenantId = User.FindFirst("tenant_id")?.Value
        });
    }

    [HttpGet("subscription-view-test")]
    [Authorize(Policy = "SUBSCRIPTION_VIEW")]
    public IActionResult SubscriptionViewTest()
    {
        return Ok(new
        {
            status = "PASS",
            message = "SUBSCRIPTION_VIEW authorization successful."
        });
    }

    [HttpGet("platform-admin-test")]
    [Authorize(Roles = "Platform Admin")]
    public IActionResult PlatformAdminTest()
    {
        return Ok(new
        {
            status = "PASS",
            message = "Platform Admin role authorization successful."
        });
    }
}