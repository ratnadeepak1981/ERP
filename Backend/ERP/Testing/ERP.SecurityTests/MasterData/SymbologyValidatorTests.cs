using System;
using Domain.Features.MasterData.Common;
using Xunit;

namespace ERP.SecurityTests.MasterData;

public class SymbologyValidatorTests
{
    // ==========================================
    // Dataset A: EAN-13 & UPC-A
    // ==========================================

    [Theory]
    [InlineData("4006381333931")] // Staedtler pen
    [InlineData("9780201379624")] // Book ISBN-13
    [InlineData("0012345678905")] // General GTIN-13
    public void Validate_Ean13_ValidCode_Passes(string value)
    {
        var ex = Record.Exception(() => SymbologyValidator.Validate(IdentifierSymbology.EAN13, value));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("4006381333932")] // Checksum mismatch (ends with 2 instead of 1)
    [InlineData("9780201379625")] // Checksum mismatch (ends with 5 instead of 4)
    [InlineData("0012345678900")] // Checksum mismatch (ends with 0 instead of 5)
    public void Validate_Ean13_InvalidChecksum_ThrowsArgumentException(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.EAN13, value));
        Assert.Contains("Invalid EAN-13 check digit", ex.Message);
    }

    [Theory]
    [InlineData("400638133393")]   // 12 digits (too short)
    [InlineData("40063813339312")] // 14 digits (too long)
    [InlineData("123")]            // 3 digits
    public void Validate_Ean13_InvalidLength_ThrowsArgumentException(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.EAN13, value));
        Assert.Contains("must consist of exactly 13 digits", ex.Message);
    }

    [Theory]
    [InlineData("400638133393A")]
    [InlineData("4006381-33393")]
    [InlineData("4006381 33393")]
    public void Validate_Ean13_NonDigits_ThrowsArgumentException(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.EAN13, value));
        Assert.Contains("must consist of exactly 13 digits", ex.Message);
    }

    [Theory]
    [InlineData("036000291452")] // Valid Kleenex UPC-A
    [InlineData("012345678905")] // Valid UPC-A
    [InlineData("042100005264")] // Valid UPC-A
    public void Validate_UpcA_ValidCode_Passes(string value)
    {
        var ex = Record.Exception(() => SymbologyValidator.Validate(IdentifierSymbology.UPCA, value));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("036000291453")] // Checksum mismatch (ends with 3 instead of 2)
    [InlineData("012345678904")] // Checksum mismatch (ends with 4 instead of 5)
    public void Validate_UpcA_InvalidChecksum_ThrowsArgumentException(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.UPCA, value));
        Assert.Contains("Invalid UPC-A check digit", ex.Message);
    }

    [Theory]
    [InlineData("03600029145")]   // 11 digits (too short)
    [InlineData("0360002914521")] // 13 digits (too long)
    [InlineData("12345")]
    public void Validate_UpcA_InvalidLength_ThrowsArgumentException(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.UPCA, value));
        Assert.Contains("must consist of exactly 12 digits", ex.Message);
    }

    [Theory]
    [InlineData("03600029145X")]
    [InlineData("036000 91452")]
    public void Validate_UpcA_NonDigits_ThrowsArgumentException(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.UPCA, value));
        Assert.Contains("must consist of exactly 12 digits", ex.Message);
    }

    // ==========================================
    // Dataset B: Code 128
    // ==========================================

    [Theory]
    [InlineData("ABC123")]
    [InlineData("ERP-PO-2026-001")]
    [InlineData("SKU/WH/0001")]
    [InlineData("Item #45 with spaces and :;<> symbols!")]
    public void Validate_Code128_ValidInput_Passes(string value)
    {
        var ex = Record.Exception(() => SymbologyValidator.Validate(IdentifierSymbology.Code128, value));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Code128_Exact80Chars_Passes()
    {
        string exact80 = new string('A', 80);
        var ex = Record.Exception(() => SymbologyValidator.Validate(IdentifierSymbology.Code128, exact80));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Code128_Exceeds80Chars_ThrowsArgumentException()
    {
        string length81 = new string('A', 81);
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.Code128, length81));
        Assert.Contains("exceeds maximum length of 80 characters", ex.Message);
    }

    [Theory]
    [InlineData("Item\t1")]      // Tab (char 9)
    [InlineData("Item\n2")]      // Newline (char 10)
    [InlineData("Item\r3")]      // Return (char 13)
    [InlineData("Café-123")]     // Non-ASCII (é)
    [InlineData("Item-€")]       // Non-ASCII (€)
    public void Validate_Code128_InvalidNonAsciiOrControlCharacters_ThrowsArgumentException(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.Code128, value));
        Assert.Contains("invalid non-ASCII characters", ex.Message);
    }

    // ==========================================
    // Dataset C: Code 39
    // ==========================================

    [Theory]
    [InlineData("ABC123")]
    [InlineData("SKU-001")]
    [InlineData("ERP/WH.01")]
    [InlineData("PROD+TEST")]
    [InlineData("ITEM%10")]
    [InlineData("$100")]
    [InlineData("CODE 39")]
    public void Validate_Code39_ValidInput_Passes(string value)
    {
        var ex = Record.Exception(() => SymbologyValidator.Validate(IdentifierSymbology.Code39, value));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("sku-001")]      // Lowercase not allowed
    [InlineData("SKU@001")]      // @ not in character set
    [InlineData("ITEM#1")]       // # not in character set
    [InlineData("PRD!")]         // ! not in character set
    [InlineData("A*B")]          // * reserved for start/stop framing
    [InlineData("A=B")]          // = not in character set
    public void Validate_Code39_InvalidCharacters_ThrowsArgumentException(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.Code39, value));
        Assert.Contains("contains invalid characters", ex.Message);
    }

    // ==========================================
    // Dataset D: QR Code & DataMatrix
    // ==========================================

    [Theory]
    [InlineData("ERP-PRODUCT-0001")]
    [InlineData("LOC-WH01-BIN001")]
    [InlineData("https://erp.local/product/12345")]
    public void Validate_QrCode_ValidInput_Passes(string value)
    {
        var ex = Record.Exception(() => SymbologyValidator.Validate(IdentifierSymbology.QRCode, value));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_QrCode_Exact500Chars_Passes()
    {
        string exact500 = new string('Q', 500);
        var ex = Record.Exception(() => SymbologyValidator.Validate(IdentifierSymbology.QRCode, exact500));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_QrCode_Exceeds500Chars_ThrowsArgumentException()
    {
        string length501 = new string('Q', 501);
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.QRCode, length501));
        Assert.Contains("exceeds maximum length of 500 characters", ex.Message);
    }

    [Theory]
    [InlineData("ERP-PRODUCT-0001")]
    [InlineData("LOC-WH01-BIN001")]
    public void Validate_DataMatrix_ValidInput_Passes(string value)
    {
        var ex = Record.Exception(() => SymbologyValidator.Validate(IdentifierSymbology.DataMatrix, value));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DataMatrix_Exact500Chars_Passes()
    {
        string exact500 = new string('D', 500);
        var ex = Record.Exception(() => SymbologyValidator.Validate(IdentifierSymbology.DataMatrix, exact500));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DataMatrix_Exceeds500Chars_ThrowsArgumentException()
    {
        string length501 = new string('D', 501);
        var ex = Assert.Throws<ArgumentException>(() =>
            SymbologyValidator.Validate(IdentifierSymbology.DataMatrix, length501));
        Assert.Contains("exceeds maximum length of 500 characters", ex.Message);
    }

    // ==========================================
    // Common: Null, Empty, Whitespace Validation
    // ==========================================

    [Theory]
    [InlineData(IdentifierSymbology.EAN13)]
    [InlineData(IdentifierSymbology.UPCA)]
    [InlineData(IdentifierSymbology.Code128)]
    [InlineData(IdentifierSymbology.Code39)]
    [InlineData(IdentifierSymbology.QRCode)]
    [InlineData(IdentifierSymbology.DataMatrix)]
    public void Validate_NullOrWhitespace_ThrowsArgumentException(IdentifierSymbology symbology)
    {
        Assert.Throws<ArgumentException>(() => SymbologyValidator.Validate(symbology, null!));
        Assert.Throws<ArgumentException>(() => SymbologyValidator.Validate(symbology, ""));
        Assert.Throws<ArgumentException>(() => SymbologyValidator.Validate(symbology, "   "));
    }

    // ==========================================
    // Direct Helper: CalculateModulo10CheckDigit
    // ==========================================

    [Fact]
    public void CalculateModulo10CheckDigit_Ean13_ReturnsCorrectDigit()
    {
        // 400638133393 -> 1
        int checkDigit = SymbologyValidator.CalculateModulo10CheckDigit("400638133393", isEan13: true);
        Assert.Equal(1, checkDigit);

        // Remainder zero case: 000000000000 -> 0
        int zeroCheck = SymbologyValidator.CalculateModulo10CheckDigit("000000000000", isEan13: true);
        Assert.Equal(0, zeroCheck);
    }

    [Fact]
    public void CalculateModulo10CheckDigit_UpcA_ReturnsCorrectDigit()
    {
        // 03600029145 -> 2
        int checkDigit = SymbologyValidator.CalculateModulo10CheckDigit("03600029145", isEan13: false);
        Assert.Equal(2, checkDigit);

        // Remainder zero case: 00000000000 -> 0
        int zeroCheck = SymbologyValidator.CalculateModulo10CheckDigit("00000000000", isEan13: false);
        Assert.Equal(0, zeroCheck);
    }
}
