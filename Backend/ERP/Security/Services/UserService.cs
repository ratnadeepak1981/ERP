using SaaS.Application.Interfaces;
using Security.Application.DTOs;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly PasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly ISubscriptionUsageService? _subscriptionUsageService;

    public UserService(
        IUserRepository repository,
        ICurrentUserContext currentUserContext,
        PasswordService passwordService,
        ITokenService tokenService,
        ISubscriptionUsageService? subscriptionUsageService = null)
    {
        _repository = repository;
        _currentUserContext = currentUserContext;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _subscriptionUsageService = subscriptionUsageService;
    }

    public async Task<User> CreateUserAsync(
        string username,
        string email,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException(
                "Username is required.",
                nameof(username));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "Email is required.",
                nameof(email));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException(
                "Password is required.",
                nameof(password));

        Guid? tenantId =
            _currentUserContext.TenantId;

        username = username.Trim().ToLowerInvariant();
        email = email.Trim().ToLowerInvariant();

        var existingUsername =
            await _repository.GetByUsernameAsync(
                tenantId,
                username);

        if (existingUsername != null)
            throw new InvalidOperationException(
                "Username is already registered in this scope.");

        var existingEmail =
            await _repository.GetByEmailAsync(email);

        if (existingEmail != null)
            throw new InvalidOperationException(
                "Email is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,

            // Current ERP rule:
            // Username = Email
            Username = email,
            Email = email,

            PasswordHash =
                _passwordService.HashPassword(password),

            IsActive = true
        };

        if (tenantId.HasValue && _subscriptionUsageService != null)
        {
            return await _subscriptionUsageService.ExecuteWithUsageLimitAsync(
                tenantId.Value,
                "USERS",
                1m,
                async () =>
                {
                    await _repository.AddAsync(user);
                    await _repository.SaveChangesAsync();
                    return user;
                });
        }

        await _repository.AddAsync(user);
        await _repository.SaveChangesAsync();

        return user;
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException(
                "Email is required.",
                nameof(request.Email));

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException(
                "Password is required.",
                nameof(request.Password));

        string email =
            request.Email.Trim().ToLowerInvariant();

        var user =
            await _repository.GetByEmailAsync(email);

        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException(
                "Invalid email or password.");

        bool passwordValid =
            _passwordService.VerifyPassword(
                request.Password,
                user.PasswordHash);

        if (!passwordValid)
            throw new UnauthorizedAccessException(
                "Invalid email or password.");

        var roles =
            await _repository.GetRolesAsync(
                user.Id);

        var permissions =
            await _repository.GetPermissionsAsync(
                user.Id);

        string token =
            _tokenService.GenerateAccessToken(
                user.Id,
                user.Username,
                user.TenantId,
                roles,
                permissions);

        return new LoginResponse
        {
            Status = "PASS",
            Message = "Authentication successful.",
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            TenantId = user.TenantId,
            Roles = roles,
            Permissions = permissions,
            Algorithm = "RS256",
            Issuer = "CSharpAuthServer",
            Audience = "ERP",
            Token = token
        };
    }
}