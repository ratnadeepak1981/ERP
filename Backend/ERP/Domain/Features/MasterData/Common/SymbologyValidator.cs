using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Domain.Features.MasterData.Common;

public static class SymbologyValidator
{
    public static void Validate(IdentifierSymbology symbology, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Identifier value cannot be empty.", nameof(value));

        string trimmed = value.Trim();

        switch (symbology)
        {
            case IdentifierSymbology.EAN13:
                ValidateEan13(trimmed);
                break;

            case IdentifierSymbology.UPCA:
                ValidateUpcA(trimmed);
                break;

            case IdentifierSymbology.Code128:
                ValidateCode128(trimmed);
                break;

            case IdentifierSymbology.QRCode:
                ValidateQrCode(trimmed);
                break;

            case IdentifierSymbology.Code39:
                ValidateCode39(trimmed);
                break;

            case IdentifierSymbology.DataMatrix:
                if (trimmed.Length > 500)
                    throw new ArgumentException("DataMatrix content exceeds maximum length of 500 characters.");
                break;

            default:
                break;
        }
    }

    private static void ValidateEan13(string value)
    {
        if (value.Length != 13 || !value.All(char.IsDigit))
            throw new ArgumentException($"EAN-13 barcode must consist of exactly 13 digits. Provided: '{value}'.");

        int checksum = CalculateModulo10CheckDigit(value.Substring(0, 12), isEan13: true);
        int expected = value[12] - '0';
        if (checksum != expected)
            throw new ArgumentException($"Invalid EAN-13 check digit. Expected '{checksum}', but provided code ends with '{expected}'.");
    }

    private static void ValidateUpcA(string value)
    {
        if (value.Length != 12 || !value.All(char.IsDigit))
            throw new ArgumentException($"UPC-A barcode must consist of exactly 12 digits. Provided: '{value}'.");

        int checksum = CalculateModulo10CheckDigit(value.Substring(0, 11), isEan13: false);
        int expected = value[11] - '0';
        if (checksum != expected)
            throw new ArgumentException($"Invalid UPC-A check digit. Expected '{checksum}', but provided code ends with '{expected}'.");
    }

    private static void ValidateCode128(string value)
    {
        if (value.Length > 80)
            throw new ArgumentException($"Code 128 barcode exceeds maximum length of 80 characters. Provided length: {value.Length}.");

        // Code 128 accepts standard ASCII printable characters (32 to 126)
        if (value.Any(c => c < 32 || c > 126))
            throw new ArgumentException("Code 128 barcode contains invalid non-ASCII characters.");
    }

    private static void ValidateCode39(string value)
    {
        // Standard Code 39 allows uppercase A-Z, 0-9, and - . $ / + % SPACE
        if (!Regex.IsMatch(value, @"^[0-9A-Z\-\.\ \$\/\+\%]+$"))
            throw new ArgumentException("Code 39 barcode contains invalid characters.");
    }

    private static void ValidateQrCode(string value)
    {
        if (value.Length > 500)
            throw new ArgumentException("QR Code content exceeds maximum length of 500 characters.");
    }

    public static int CalculateModulo10CheckDigit(string digits, bool isEan13)
    {
        int sum = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            int digit = digits[i] - '0';
            // For EAN-13 (12 data digits): odd positions (1-indexed) weight 1, even weight 3
            // For UPC-A (11 data digits): odd positions (1-indexed) weight 3, even weight 1
            int weight = isEan13
                ? (i % 2 == 0 ? 1 : 3)
                : (i % 2 == 0 ? 3 : 1);

            sum += digit * weight;
        }

        int remainder = sum % 10;
        return remainder == 0 ? 0 : 10 - remainder;
    }
}
