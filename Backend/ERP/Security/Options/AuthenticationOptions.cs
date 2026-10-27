namespace Security.Options;

public class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenIdleLifetimeDays { get; set; } = 7;
    public int RefreshTokenAbsoluteLifetimeDays { get; set; } = 30;

    public void Validate()
    {
        if (AccessTokenLifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("AccessTokenLifetimeMinutes must be greater than zero.");
        }

        if (RefreshTokenIdleLifetimeDays <= 0)
        {
            throw new InvalidOperationException("RefreshTokenIdleLifetimeDays must be greater than zero.");
        }

        if (RefreshTokenAbsoluteLifetimeDays <= 0)
        {
            throw new InvalidOperationException("RefreshTokenAbsoluteLifetimeDays must be greater than zero.");
        }

        if (RefreshTokenIdleLifetimeDays > RefreshTokenAbsoluteLifetimeDays)
        {
            throw new InvalidOperationException("RefreshTokenIdleLifetimeDays cannot exceed RefreshTokenAbsoluteLifetimeDays.");
        }
    }
}
