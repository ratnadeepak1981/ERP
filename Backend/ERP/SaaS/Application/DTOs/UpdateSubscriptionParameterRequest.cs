using SaaS.Core.Models;

namespace SaaS.Application.DTOs;

public class UpdateSubscriptionParameterRequest
{
    public string ParameterKey { get; set; } = string.Empty;

    public SubscriptionParameterType ParameterType { get; set; }

    public bool IsActive { get; set; } = true;
}