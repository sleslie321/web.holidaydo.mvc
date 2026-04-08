using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

public interface IAmazonAuthTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class AmazonAuthTokenService : IAmazonAuthTokenService
{
    private const string CacheKey = "AmazonCreatorsApi.AccessToken";
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AmazonAuthTokenService> _logger;

    public AmazonAuthTokenService(
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AmazonAuthTokenService> logger)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<string>(CacheKey, out var cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return cachedToken;
        }

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue<string>(CacheKey, out cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
            {
                return cachedToken;
            }

            var authUrl = _configuration["amazon_api_auth_url"];
            var clientId = _configuration["client_id"];
            var clientSecret = _configuration["client_secret"];

            if (string.IsNullOrWhiteSpace(authUrl) ||
                string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException("Missing Amazon auth configuration values.");
            }

            var payload = new
            {
                grant_type = "client_credentials",
                client_id = clientId,
                client_secret = clientSecret,
                scope = "creatorsapi::default"
            };

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(authUrl, payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Amazon token call failed. Status: {StatusCode}, Body: {Body}", response.StatusCode, body);
                throw new HttpRequestException($"Amazon token request failed: {(int)response.StatusCode} {body}");
            }

            var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(body)
                ?? throw new InvalidOperationException("Unable to parse Amazon token response.");

            if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken) || tokenResponse.ExpiresIn <= 0)
            {
                throw new InvalidOperationException("Amazon token response is missing required fields.");
            }

            var ttl = TimeSpan.FromSeconds(Math.Max(5, tokenResponse.ExpiresIn - 60));
            _cache.Set(CacheKey, tokenResponse.AccessToken, ttl);

            return tokenResponse.AccessToken;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
