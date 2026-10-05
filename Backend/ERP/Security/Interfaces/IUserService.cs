using Security.Core.Models;

namespace Security.Interfaces;

public interface IUserService
{
    Task<User> CreateUserAsync(
        string username,
        string email,
        string password);
}