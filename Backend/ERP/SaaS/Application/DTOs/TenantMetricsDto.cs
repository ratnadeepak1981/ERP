namespace SaaS.Application.DTOs;

public class TenantMetricsDto
{
    public int TotalRegistered { get; set; }

    public int Pending { get; set; }

    public int InProgress { get; set; }

    public int Failed { get; set; }

    public int Completed { get; set; }

    public int ActiveSubscriptions { get; set; }

    public bool IsLive { get; set; } = true;
}
