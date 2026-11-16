using System;
using System.Text.RegularExpressions;

namespace ERP.Application.Common.Email;

/// <summary>
/// Represents an email recipient with a validated email address and optional display name.
/// </summary>
public sealed class EmailRecipient
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Address { get; }
    public string? DisplayName { get; }

    public EmailRecipient(string address, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Email address cannot be empty.", nameof(address));

        var trimmed = address.Trim();
        if (!EmailRegex.IsMatch(trimmed))
            throw new ArgumentException($"Invalid email address format: '{address}'.", nameof(address));

        Address = trimmed;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
    }

    public override string ToString() =>
        string.IsNullOrEmpty(DisplayName) ? Address : $"{DisplayName} <{Address}>";

    public override bool Equals(object? obj) =>
        obj is EmailRecipient other && string.Equals(Address, other.Address, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Address);
}
