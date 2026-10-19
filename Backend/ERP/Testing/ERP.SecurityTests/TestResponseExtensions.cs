using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Contracts;

namespace ERP.SecurityTests;

public static class TestResponseExtensions
{
    private static readonly JsonSerializerOptions _defaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ApiResponse<T>?> ReadApiResponseAsync<T>(
        this HttpResponseMessage response,
        JsonSerializerOptions? options = null)
    {
        return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(options ?? _defaultOptions);
    }

    public static async Task<T?> ReadApiResponseDataAsync<T>(
        this HttpResponseMessage response,
        JsonSerializerOptions? options = null)
    {
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(options ?? _defaultOptions);
        return apiResponse != null ? apiResponse.Data : default;
    }

    public static async Task<JsonElement> ReadDataJsonElementAsync(
        this HttpResponseMessage response,
        JsonSerializerOptions? options = null)
    {
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(options ?? _defaultOptions);
        if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("data", out var dataProp))
        {
            return dataProp;
        }
        return doc;
    }
}
