using System.Net.Http.Json;

namespace Kumunita.Web.Client.Services;

/// <summary>
/// Typed HTTP client for all API calls from the Blazor WASM client.
/// Automatically attaches the bearer token via the OIDC handler.
/// All methods return null on 404 rather than throwing.
/// </summary>
public interface IApiClient
{
    Task<T?> GetAsync<T>(string url, CancellationToken ct = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default);
    Task PostAsync<TRequest>(string url, TRequest body, CancellationToken ct = default);
    Task<TResponse?> PutAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default);
    Task PutAsync<TRequest>(string url, TRequest body, CancellationToken ct = default);
    Task DeleteAsync(string url, CancellationToken ct = default);
}

public class ApiClient : IApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    public async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.GetAsync(url, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string url, TRequest body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync(url, body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(ct);
    }

    public async Task PostAsync<TRequest>(string url, TRequest body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PostAsJsonAsync(url, body, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(
        string url, TRequest body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PutAsJsonAsync(url, body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(ct);
    }

    public async Task PutAsync<TRequest>(string url, TRequest body, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.PutAsJsonAsync(url, body, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _http.DeleteAsync(url, ct);
        response.EnsureSuccessStatusCode();
    }
}