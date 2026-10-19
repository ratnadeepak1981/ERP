using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.AI.Infrastructure.PythonClient.Interfaces;
using Microsoft.Extensions.Logging;
using Security.Interfaces;

namespace ERP.AI.Infrastructure.PythonClient;

public class PythonReadinessClient : IPythonReadinessClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenService _tokenService;
    private readonly ILogger<PythonReadinessClient> _logger;

    public const string ReadinessRole = "Service Agent";
    public const string ReadinessPermission = "AI.READINESS";

    public PythonReadinessClient(
        HttpClient httpClient,
        ITokenService tokenService,
        ILogger<PythonReadinessClient> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<PythonHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("health", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PythonHealthResponse
                {
                    IsSuccess = false,
                    Status = "FAIL",
                    ErrorDetails = $"Python /health returned HTTP {(int)response.StatusCode}."
                };
            }

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            var root = doc.RootElement;
            string status = root.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            string message = root.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;

            return new PythonHealthResponse
            {
                IsSuccess = string.Equals(status, "PASS", StringComparison.OrdinalIgnoreCase),
                Status = status,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to connect to Python AI service during health check: {ErrorMessage}", ex.Message);
            return new PythonHealthResponse
            {
                IsSuccess = false,
                Status = "UNAVAILABLE",
                ErrorDetails = ex.Message
            };
        }
    }

    public async Task<PythonHandshakeResponse> PerformHandshakeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Generates a dedicated service token with minimal readiness scope (5-minute expiry)
            string serviceToken = _tokenService.GenerateServiceToken(
                serviceName: "erp-ai-readiness-client",
                roles: new[] { ReadinessRole },
                permissions: new[] { ReadinessPermission },
                lifetime: TimeSpan.FromMinutes(5));

            using var request = new HttpRequestMessage(HttpMethod.Post, "security/handshake");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PythonHandshakeResponse
                {
                    IsSuccess = false,
                    Status = "FAIL",
                    ErrorDetails = $"Python /security/handshake returned HTTP {(int)response.StatusCode}."
                };
            }

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            var root = doc.RootElement;
            string status = root.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            string stage = root.TryGetProperty("stage", out var st) ? st.GetString() ?? string.Empty : string.Empty;
            string message = root.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;

            bool isPass = string.Equals(status, "PASS", StringComparison.OrdinalIgnoreCase);

            return new PythonHandshakeResponse
            {
                IsSuccess = isPass,
                Status = isPass ? status : "FAIL",
                Stage = stage,
                Message = message,
                ErrorDetails = isPass ? string.Empty : $"Python handshake responded with status: {status}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to execute cryptographic handshake with Python AI service: {ErrorMessage}", ex.Message);
            return new PythonHandshakeResponse
            {
                IsSuccess = false,
                Status = "UNAVAILABLE",
                ErrorDetails = ex.Message
            };
        }
    }
}
