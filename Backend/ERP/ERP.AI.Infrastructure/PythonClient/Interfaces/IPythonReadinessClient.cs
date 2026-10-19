namespace ERP.AI.Infrastructure.PythonClient.Interfaces;

public interface IPythonReadinessClient
{
    Task<PythonHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default);

    Task<PythonHandshakeResponse> PerformHandshakeAsync(CancellationToken cancellationToken = default);
}

public class PythonHealthResponse
{
    public bool IsSuccess { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;
}

public class PythonHandshakeResponse
{
    public bool IsSuccess { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;
}
