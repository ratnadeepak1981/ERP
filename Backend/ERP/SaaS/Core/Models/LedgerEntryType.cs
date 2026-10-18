namespace SaaS.Core.Models;

public enum LedgerEntryType
{
    InvoiceCharge = 1,     // Debit (Increases receivable / tenant balance owed)
    PaymentReceived = 2,   // Credit (Reduces receivable / tenant balance owed)
    CreditNoteIssued = 3,  // Credit (Reduces receivable / tenant balance owed)
    RefundIssued = 4,      // Debit (Reverses payment credit / increases receivable)
    ManualAdjustment = 5   // Debit or Credit
}

public enum LedgerDirection
{
    Debit = 1,
    Credit = 2
}
