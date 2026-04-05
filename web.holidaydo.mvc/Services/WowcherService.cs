using System.Text.Json;
using web.holidaydo.mvc.Models;

namespace web.holidaydo.mvc.Services
{
    public sealed class WowcherService(IHttpClientFactory httpClientFactory)
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new() { PropertyNameCaseInsensitive = true };

        public async Task<List<CityBreakDeal>?> GetDestinationDealsAsync(string url)
        {
            var client = httpClientFactory.CreateClient();

            using var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync();

            // Wowcher API returns { "deals": [...] }
            var wrapper = await JsonSerializer.DeserializeAsync<WowcherDealResponse>(stream, JsonOptions);

            return wrapper?.Deals;
        }

        private sealed class WowcherDealResponse
        {
            public List<CityBreakDeal>? Deals { get; set; }
        }
    }
}