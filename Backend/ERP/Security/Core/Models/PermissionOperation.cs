namespace Security.Core.Models;

/// <summary>
/// Canonical operation names used to describe what a permission allows.
/// The operation is descriptive metadata and is not used for authorization;
/// authorization always matches the exact permission code (plus the legacy
/// .EDIT/.UPDATE aliases handled by the permission policy handler).
/// </summary>
public static class PermissionOperation
{
    public const string View = "VIEW";
    public const string Create = "CREATE";
    public const string Edit = "EDIT";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string SoftDelete = "SOFT_DELETE";
    public const string Activate = "ACTIVATE";
    public const string Deactivate = "DEACTIVATE";
    public const string Submit = "SUBMIT";
    public const string Approve = "APPROVE";
    public const string Confirm = "CONFIRM";
    public const string Post = "POST";
    public const string Cancel = "CANCEL";
    public const string Dispatch = "DISPATCH";
    public const string Deliver = "DELIVER";
    public const string Convert = "CONVERT";
    public const string Qualify = "QUALIFY";
    public const string Assign = "ASSIGN";
    public const string Override = "OVERRIDE";
    public const string Manage = "MANAGE";

    /// <summary>
    /// Derives the operation from the last segment of a permission code.
    /// For example CUSTOMER.PRICE_CARD.ASSIGN becomes ASSIGN.
    /// </summary>
    public static string FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var separatorIndex = code.LastIndexOf('.');
        var operation = separatorIndex >= 0 && separatorIndex < code.Length - 1
            ? code[(separatorIndex + 1)..]
            : code;

        return operation.Trim().ToUpperInvariant();
    }
}
