namespace SaaS.Core.Rules;

public static class TenantRules
{
    public static bool IsValidName(string name)
    {
        return !string.IsNullOrWhiteSpace(name);
    }

    public static bool IsValidCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code);
    }

    public static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }
}