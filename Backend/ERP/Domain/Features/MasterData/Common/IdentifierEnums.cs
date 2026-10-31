namespace Domain.Features.MasterData.Common;

public enum IdentifierType
{
    LinearBarcode = 1,
    QRCode = 2,
    DataMatrix = 3
}

public enum IdentifierSymbology
{
    Code128 = 1,
    EAN13 = 2,
    UPCA = 3,
    Code39 = 4,
    QRCode = 5,
    DataMatrix = 6
}

public enum IdentifierSource
{
    Manufacturer = 1,
    Supplier = 2,
    Internal = 3,
    Customer = 4
}
