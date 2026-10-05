namespace Security.Application.DTOs;

public class CreatePermissionRequest
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;
}