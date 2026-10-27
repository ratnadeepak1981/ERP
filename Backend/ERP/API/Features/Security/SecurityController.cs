using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using Security.Interfaces;
using Security.Services;
using SecurityLoginRequest = Security.Application.DTOs.LoginRequest;
using SecurityRefreshTokenRequest = Security.Application.DTOs.RefreshTokenRequest;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
public class SecurityController : BaseApiController
{
    private readonly SecurityService _securityService;
    private readonly ITokenService _tokenService;
    private readonly RsaPrivateKeyLoader _keyLoader;
    private readonly IUserService _userService;
    private readonly ISessionService _sessionService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWebHostEnvironment _env;

    private const string RefreshTokenCookieName = "apex_erp_refresh_token";
    private const string CookiePath = "/api";

    public SecurityController(
        SecurityService securityService,
        ITokenService tokenService,
        RsaPrivateKeyLoader keyLoader,
        IUserService userService,
        ISessionService sessionService,
        ICurrentUserContext currentUserContext,
        IWebHostEnvironment env)
    {
        _securityService = securityService;
        _tokenService = tokenService;
        _keyLoader = keyLoader;
        _userService = userService;
        _sessionService = sessionService;
        _currentUserContext = currentUserContext;
        _env = env;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] SecurityLoginRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequestResponse("Login request is null.");
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequestResponse("Email is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequestResponse("Password is required.");
            }

            var result = await _userService.LoginAsync(request);
            if (!string.IsNullOrWhiteSpace(result.RefreshToken))
            {
                SetRefreshTokenCookie(result.RefreshToken, result.AccessTokenExpiresAt?.AddDays(7));
            }

            return SuccessResponse(result, "Login successful.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return UnauthorizedResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] SecurityRefreshTokenRequest? request)
    {
        try
        {
            var (rawRefreshToken, isCookieTransport) = ExtractRefreshToken(request);

            if (string.IsNullOrWhiteSpace(rawRefreshToken))
            {
                return UnauthorizedResponse("Refresh token is required.");
            }

            if (isCookieTransport && !ValidateCsrfOrigin())
            {
                return ForbiddenResponse("Potential cross-site request forgery detected on cookie refresh.");
            }

            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            string? userAgent = Request.Headers.UserAgent.ToString();

            var result = await _sessionService.RefreshTokenAsync(
                rawRefreshToken,
                ipAddress,
                userAgent,
                HttpContext.RequestAborted);

            if (!result.Succeeded)
            {
                if (isCookieTransport)
                {
                    ClearRefreshTokenCookie();
                }
                return UnauthorizedResponse(result.ErrorMessage ?? "Invalid refresh token.");
            }

            if (isCookieTransport && !string.IsNullOrWhiteSpace(result.NewRefreshToken))
            {
                SetRefreshTokenCookie(
                    result.NewRefreshToken,
                    result.RefreshTokenExpiresAt.HasValue
                        ? new DateTimeOffset(result.RefreshTokenExpiresAt.Value)
                        : DateTimeOffset.UtcNow.AddDays(7));
            }

            return SuccessResponse(new
            {
                accessToken = result.AccessToken,
                token = result.AccessToken,
                accessTokenExpiresAt = result.AccessTokenExpiresAt,
                refreshToken = isCookieTransport ? null : result.NewRefreshToken,
                refreshTokenExpiresAt = result.RefreshTokenExpiresAt,
                userId = result.UserId,
                username = result.Username,
                email = result.Email,
                tenantId = result.TenantId,
                roles = result.Roles,
                permissions = result.Permissions,
                sessionId = result.SessionId
            }, "Token refreshed successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] SecurityRefreshTokenRequest? request)
    {
        try
        {
            var (rawRefreshToken, isCookieTransport) = ExtractRefreshToken(request);

            if (!string.IsNullOrWhiteSpace(rawRefreshToken))
            {
                await _sessionService.RevokeSessionByTokenAsync(
                    rawRefreshToken,
                    "UserLogout",
                    HttpContext.RequestAborted);
            }

            if (isCookieTransport || Request.Cookies.ContainsKey(RefreshTokenCookieName))
            {
                ClearRefreshTokenCookie();
            }

            return SuccessResponse("Logout successful.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        try
        {
            Guid currentUserId = _currentUserContext.UserId;

            await _sessionService.RevokeAllSessionsForUserAsync(
                currentUserId,
                "LogoutAll",
                HttpContext.RequestAborted);

            ClearRefreshTokenCookie();
            return SuccessResponse("All user sessions revoked successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> GetActiveSessions()
    {
        try
        {
            Guid currentUserId = _currentUserContext.UserId;

            var sessions = await _sessionService.GetActiveSessionsForUserAsync(
                currentUserId,
                null,
                HttpContext.RequestAborted);

            return SuccessResponse(sessions);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        try
        {
            Guid currentUserId = _currentUserContext.UserId;
            bool isPlatformAdmin = User.IsInRole("Platform Admin") || User.IsInRole("Security Manager");

            bool revoked = await _sessionService.RevokeSessionByIdAsync(
                sessionId,
                currentUserId,
                isPlatformAdmin,
                "AdminRevocation",
                HttpContext.RequestAborted);

            if (!revoked)
            {
                return NotFoundResponse("Session not found.");
            }

            return SuccessResponse("Session revoked successfully.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbiddenResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    private void SetRefreshTokenCookie(string token, DateTimeOffset? expiresAt)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps || !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = expiresAt ?? DateTimeOffset.UtcNow.AddDays(7)
        };

        Response.Cookies.Append(RefreshTokenCookieName, token, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps || !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };

        Response.Cookies.Delete(RefreshTokenCookieName, cookieOptions);
    }

    private (string? Token, bool IsCookieTransport) ExtractRefreshToken(SecurityRefreshTokenRequest? request)
    {
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            return (request.RefreshToken, false);
        }

        if (Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            return (cookieToken, true);
        }

        return (null, false);
    }

    private bool ValidateCsrfOrigin()
    {
        string? origin = Request.Headers.Origin.FirstOrDefault() ?? Request.Headers.Referer.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            bool isAllowed = false;
            if (_env.IsDevelopment() && (uri.Host == "localhost" || uri.Host == "127.0.0.1"))
            {
                isAllowed = true;
            }
            else if (string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                isAllowed = true;
            }

            if (!isAllowed)
            {
                return false;
            }
        }

        bool hasCustomHeader = Request.Headers.ContainsKey("X-Requested-With") || Request.Headers.ContainsKey("X-Apex-Origin");
        bool hasOrigin = !string.IsNullOrWhiteSpace(origin);

        return hasCustomHeader || hasOrigin;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return SuccessResponse(_securityService.GetStatus());
    }

    [HttpGet("diagnostic")]
    [Authorize(Roles = "Platform Admin, Security Manager")]
    public IActionResult RunDiagnostic()
    {
        var results = new List<object>();

        string privateKeyPath = _keyLoader.GetPrivateKeyPath();
        bool vaultPathAvailable = !string.IsNullOrWhiteSpace(privateKeyPath);

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
            return SuccessResponse(results);

        bool privateKeyFound = _keyLoader.IsPrivateKeyFound();

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
            return SuccessResponse(results);

        bool privateKeyLoaded = _keyLoader.TryLoadPrivateKey();

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
            return SuccessResponse(results);

        bool privateKeyValid = _keyLoader.IsPrivateKeyValid();
        var rsaPrivate = _keyLoader.GetRsa();

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
            return SuccessResponse(results);

        bool publicKeyFound = _keyLoader.IsPublicKeyFound();

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
            return SuccessResponse(results);

        bool publicKeyLoaded = _keyLoader.TryLoadPublicKey();

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
            return SuccessResponse(results);

        bool publicKeyValid = _keyLoader.IsPublicKeyValid();

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
            return SuccessResponse(results);

        bool keyPairValid = _keyLoader.VerifyPrivatePublicPair();

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
            return SuccessResponse(results);

        try
        {
            string token = _tokenService.GenerateAccessToken(
                Guid.NewGuid(),
                "DiagnosticUser",
                Guid.NewGuid(),
                new[] { "DiagnosticUser" },
                new[] { "Security.Diagnostic" });

            bool tokenGenerated = !string.IsNullOrWhiteSpace(token);

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
                message = $"JWT generation engine threw a critical error: {ex.Message}"
            });
        }

        return SuccessResponse(results);
    }

    [HttpGet("jwt-diagnostic")]
    public IActionResult JwtDiagnostic()
    {
        if (!_env.IsDevelopment())
        {
            return NotFoundResponse("Diagnostic endpoint only available in Development environment.");
        }

        try
        {
            Guid userId = Guid.NewGuid();
            Guid tenantId = Guid.NewGuid();

            var roles = new[] { "TenantAdmin" };
            var permissions = new[] { "Product.Read", "Product.Write" };

            string token = _tokenService.GenerateAccessToken(
                userId,
                "DiagnosticUser",
                tenantId,
                roles,
                permissions);

            return SuccessResponse(new
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
            }, "Real RS256 JWT generated successfully");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("login-test")]
    public IActionResult LoginTest([FromQuery] string username, [FromQuery] string password)
    {
        if (!_env.IsDevelopment())
        {
            return NotFoundResponse("Login test endpoint only available in Development environment.");
        }

        if (username != "admin" || password != "123")
        {
            return UnauthorizedResponse("Invalid test credentials");
        }

        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        var roles = new[] { "TenantAdmin" };
        var permissions = new[] { "Product.Read", "Product.Write" };

        string token = _tokenService.GenerateAccessToken(
            userId,
            username,
            tenantId,
            roles,
            permissions);

        return SuccessResponse(new
        {
            status = "PASS",
            message = "Test authentication successful",
            username,
            algorithm = "RS256",
            issuer = "CSharpAuthServer",
            audience = "ERP",
            token
        }, "Test authentication successful");
    }

    [HttpPost("jwt-wrong-issuer-test")]
    public IActionResult JwtWrongIssuerTest()
    {
        if (!_env.IsDevelopment())
        {
            return NotFoundResponse("Test endpoint only available in Development environment.");
        }

        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, "TenantAdmin"),
            new("permission", "Product.Read")
        };

        var rsa = _keyLoader.GetRsa()
            ?? throw new InvalidOperationException("RSA private key is unavailable.");

        var credentials = new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "WRONG-ISSUER",
            audience: "ERP",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return SuccessResponse(new
        {
            status = "PASS",
            test = "Wrong issuer",
            issuer = "WRONG-ISSUER",
            audience = "ERP",
            algorithm = "RS256",
            token
        });
    }

    [Authorize]
    [HttpGet("protected-test")]
    public IActionResult ProtectedTest()
    {
        return SuccessResponse(new
        {
            status = "PASS",
            message = "JWT authentication successful.",
            userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            username = User.FindFirst(ClaimTypes.Name)?.Value,
            roles = User.Claims
                .Where(x => x.Type == ClaimTypes.Role)
                .Select(x => x.Value)
                .ToList(),
            tenantId = User.FindFirst("tenant_id")?.Value
        }, "JWT authentication successful.");
    }

    [HttpGet("subscription-view-test")]
    [RequirePermission("SUBSCRIPTION.VIEW")]
    public IActionResult SubscriptionViewTest()
    {
        return SuccessResponse(new
        {
            status = "PASS",
            message = "SUBSCRIPTION_VIEW authorization successful."
        }, "SUBSCRIPTION_VIEW authorization successful.");
    }

    [HttpGet("platform-admin-test")]
    [Authorize(Roles = "Platform Admin")]
    public IActionResult PlatformAdminTest()
    {
        return SuccessResponse(new
        {
            status = "PASS",
            message = "Platform Admin role authorization successful."
        }, "Platform Admin role authorization successful.");
    }

    [HttpGet("error-simulation-test")]
    public IActionResult ErrorSimulationTest()
    {
        if (!_env.IsDevelopment())
        {
            return NotFoundResponse("Diagnostic endpoint only available in Development environment.");
        }

        throw new InvalidOperationException("Simulated unexpected internal database failure for error traceability verification.");
    }
}