using System;
using System.Collections.Generic;

namespace Security.Application.DTOs;

public class AssignRolePermissionsBatchRequest
{
    public Guid RoleId { get; set; }

    public List<string>? PermissionIds { get; set; }

    public List<string>? PermissionCodes { get; set; }
}
