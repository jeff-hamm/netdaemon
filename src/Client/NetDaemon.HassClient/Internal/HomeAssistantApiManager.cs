using NetDaemon.Client.Exceptions;

namespace NetDaemon.Client.Internal;

internal class HomeAssistantApiManager : IHomeAssistantApiManager
{
    private readonly string _apiUrl;

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializationOptions;

    public HomeAssistantApiManager(
        IOptions<HomeAssistantSettings> settings,
        HttpClient httpClient,
        JsonSerializerOptions serializationOptions
    )
    {
        _httpClient = httpClient;
        _serializationOptions = serializationOptions;
        _apiUrl = GetApiUrl(settings.Value);
        InitializeHttpClientWithAuthorizationHeaders(settings.Value.Token);
    }

    /// <inheritdoc />
    public async Task<T?> GetApiCallAsync<T>(string apiPath, CancellationToken cancelToken)
    {
        var apiUrl = $"{_apiUrl}/{apiPath}";

        var result = await _httpClient.GetAsync(new Uri(apiUrl),
            cancelToken).ConfigureAwait(false);
        if (result.IsSuccessStatusCode)
        {
            var content = await result.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(content, _serializationOptions, cancelToken)
                .ConfigureAwait(false);
        }

        throw new HomeAssistantApiCallException(
            $"Call to API unsuccessful, code {result.StatusCode}: reason: {result.ReasonPhrase}", result.StatusCode);
    }

    public async Task<T?> PostApiCallAsync<T>(string apiPath, CancellationToken cancelToken, object? data = null)
    {
        var apiUrl = $"{_apiUrl}/{apiPath}";
        var content = "";

        if (data != null) content = JsonSerializer.Serialize(data, _serializationOptions);
        using var sc = new StringContent(content, Encoding.UTF8);

        if (content.Length > 0) sc.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");

        var result = await _httpClient.PostAsync(new Uri(apiUrl),
            sc,
            cancelToken).ConfigureAwait(false);

        if (!result.IsSuccessStatusCode) return default;
        if (!(result.Content.Headers.ContentLength > 0)) return default;
        var stream = await result.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, _serializationOptions, cancelToken)
            .ConfigureAwait(false);
    }

    private void InitializeHttpClientWithAuthorizationHeaders(string token)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    private static string GetApiUrl(HomeAssistantSettings settings)
    {
        if (settings.Host == "supervisor")
            // We are inside a home assistant add-on
            return "http://supervisor/core/api";

        var httpScheme = settings.Ssl ? "https" : "http";
        return $"{httpScheme}://{settings.Host}:{settings.Port}/api";
    }
}
