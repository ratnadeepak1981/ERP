using SaaS.Core.Models;

namespace SaaS.Application.DTOs;

public class CreateSubscriptionParameterRequest
{
    public string ParameterKey { get; set; } = string.Empty;

    public SubscriptionParameterType ParameterType { get; set; }
        = SubscriptionParameterType.Fixed;

    public bool IsActive { get; set; } = true;
}