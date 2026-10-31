using System;

namespace Security.Application.DTOs;

public class UpdateUserRequest
{
    public string? Username { get; set; }

    public string? Email { get; set; }

    public Guid? RoleId { get; set; }

    public bool? IsActive { get; set; }
}
