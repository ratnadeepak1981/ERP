namespace Domain.Features.Sales.SalesQuotation;

/// <summary>
/// Status lifecycle of a Sales Quotation in accordance with Sales-Quotation-Workflow.md Section 11.
/// </summary>
public enum SalesQuotationStatus
{
    /// <summary>
    /// Quotation under active authoring; lines, quantities, and prices editable.
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Placed in conditional internal review; author edits locked.
    /// </summary>
    Submitted = 2,

    /// <summary>
    /// Internally approved by sales management; ready for customer issuance.
    /// </summary>
    Confirmed = 3,

    /// <summary>
    /// Rejected during internal review with mandatory remarks.
    /// </summary>
    RejectedInternal = 4,

    /// <summary>
    /// Formally issued to the customer via email or portal; proposal active and customer acceptance eligible.
    /// </summary>
    Sent = 5,

    /// <summary>
    /// Customer accepted the valid issued proposal; eligible for Sales Order conversion.
    /// </summary>
    Accepted = 6,

    /// <summary>
    /// Customer formally declined with mandatory reason.
    /// </summary>
    DeclinedByCustomer = 7,

    /// <summary>
    /// Atomically converted into official SalesOrder (Draft). Immutable terminal state.
    /// </summary>
    Converted = 8,

    /// <summary>
    /// Newer revision issued (e.g. SQ-2026-0001-R1). Immutable historical record.
    /// </summary>
    Superseded = 9,

    /// <summary>
    /// Validity elapsed (DateTime.UtcNow > ValidUntilDateUtc).
    /// </summary>
    Expired = 10,

    /// <summary>
    /// Withdrawn by operating company prior to customer acceptance.
    /// </summary>
    Cancelled = 11
}

/// <summary>
/// Source channel or origin of a Sales Quotation.
/// </summary>
public enum SalesQuotationType
{
    Standard = 1,
    CustomerRequest = 2,
    CustomB2B = 3
}
