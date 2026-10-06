using SaaS.Core.Models;

namespace SaaS.Application.DTOs;

public class SubscriptionParameterResponse
{
    public Guid Id { get; set; }

    public string ParameterKey { get; set; } = string.Empty;

    public SubscriptionParameterType ParameterType { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public Guid? ModifiedBy { get; set; }
}