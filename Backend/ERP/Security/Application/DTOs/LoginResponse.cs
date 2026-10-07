namespace Security.Application.DTOs;

public class LoginResponse
{
    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    public List<string> Roles { get; set; } = new();

    public List<string> Permissions { get; set; } = new();

    public string Algorithm { get; set; } = "RS256";

    public string Issuer { get; set; } = "CSharpAuthServer";

    public string Audience { get; set; } = "ERP";

    public string Token { get; set; } = string.Empty;
}