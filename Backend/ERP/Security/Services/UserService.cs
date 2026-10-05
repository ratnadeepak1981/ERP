using Security.Core.Models;
using Security.Interfaces;

namespace Security.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly PasswordService _passwordService;

    public UserService(
        IUserRepository repository,
        ICurrentUserContext currentUserContext,
        PasswordService passwordService)
    {
        _repository = repository;
        _currentUserContext = currentUserContext;
        _passwordService = passwordService;
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

        username = username.Trim();
        email = email.Trim();

        var existingUser =
            await _repository.GetByUsernameAsync(
                tenantId,
                username);

        if (existingUser != null)
            throw new InvalidOperationException(
                "User already exists in this scope.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = username,
            Email = email,
            PasswordHash =
                _passwordService.HashPassword(password),
            IsActive = true
        };

        await _repository.AddAsync(user);
        await _repository.SaveChangesAsync();

        return user;
    }
}