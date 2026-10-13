namespace ERP.Domain.Features.CRM;

public enum LeadSource
{
    DirectInquiry = 1,
    Website = 2,
    Referral = 3,
    ColdCall = 4,
    Campaign = 5,
    TradeShow = 6,
    Partner = 7,
    OrganicSearch = 8,
    SocialMedia = 9,
    Other = 99
}

public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    Qualified = 3,
    Disqualified = 4,
    Converted = 5
}

public enum OpportunityStage
{
    Prospecting = 1,
    Qualification = 2,
    NeedsAnalysis = 3,
    Proposal = 4,
    Negotiation = 5,
    ClosedWon = 6,
    ClosedLost = 7
}

public enum CrmActivityType
{
    Note = 1,
    PhoneCall = 2,
    Email = 3,
    Meeting = 4,
    Task = 5,
    FollowUp = 6
}
