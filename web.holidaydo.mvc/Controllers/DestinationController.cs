using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using web.holidaydo.mvc.Models;
using web.holidaydo.mvc.Services;

namespace web.holidaydo.mvc.Controllers
{
    public class DestinationController : Controller
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new() { PropertyNameCaseInsensitive = true };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly WowcherService _wowcherService;

        public DestinationController(IHttpClientFactory httpClientFactory, WowcherService wowcherService)
        {
            _httpClientFactory = httpClientFactory;
            _wowcherService = wowcherService;
        }

        public async Task<IActionResult> Index(string slug, int id)
        {
            var client = _httpClientFactory.CreateClient();

            // Fire both requests simultaneously
            var destinationFetch = client.GetAsync(
                $"https://fnholidayo.azurewebsites.net/api/destinations/{slug}");

            var productsFetch = id > 0
                ? client.GetAsync($"https://fnholidayo.azurewebsites.net/api/SearchProducts?vid={id}")
                : null;

            using var destinationResponse = await destinationFetch;

            if (!destinationResponse.IsSuccessStatusCode)
                return View((DestinationViewModel?)null);

            await using var destinationStream = await destinationResponse.Content.ReadAsStreamAsync();
            var apiResponse = await JsonSerializer.DeserializeAsync<DestinationApiResponse>(
                destinationStream, JsonOptions);

            if (apiResponse is null)
                return View((DestinationViewModel?)null);

            // Parse products
            SearchProductsResponse? searchProducts = null;
            if (productsFetch is not null)
            {
                using var productsResponse = await productsFetch;
                if (productsResponse.IsSuccessStatusCode)
                {
                    await using var productsStream = await productsResponse.Content.ReadAsStreamAsync();
                    searchProducts = await JsonSerializer.DeserializeAsync<SearchProductsResponse>(
                        productsStream, JsonOptions);
                }
            }

            // Fetch Wowcher deals
            List<CityBreakDeal>? deals = null;
            if (apiResponse.Extra?.DealApiUrl is not null)
                deals = await _wowcherService.GetDestinationDealsAsync(apiResponse.Extra.DealApiUrl);

            var title = apiResponse.Meta?.Name ?? apiResponse.Destination?.Name ?? FormatSlug(slug);

            ViewData["Title"]       = $"Holiday Activities for {title} - Do More on Holiday";
            ViewData["Description"] = $"{apiResponse.Meta?.Summary} - {title}";

            var viewModel = new DestinationViewModel
            {
                Title           = title,
                Description     = apiResponse.Meta?.Summary,
                LongDescription = apiResponse.Meta?.Content,
                Slug            = slug,
                Id              = id,
                Extra           = apiResponse.Extra,
                SearchProducts  = searchProducts,
                Deals           = deals
            };

            return View(viewModel);
        }

        public static string FormatSlug(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return "Destinations";
            return string.Join(" ", slug.Split('-')
                .Select(w => char.ToUpper(w[0]) + w[1..]));
        }

        public static string FormatFlag(string? flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) return string.Empty;
            flag = flag.Replace('_', ' ').ToLowerInvariant();
            return char.ToUpper(flag[0]) + flag[1..];
        }

        public static string FormatDuration(int? minutes, bool showUnit)
        {
            if (minutes is null or <= 0) return string.Empty;

            if (!showUnit)
            {
                return minutes < 60
                    ? minutes.Value.ToString()
                    : (minutes.Value / 60).ToString();
            }

            if (minutes < 60)
                return $"{minutes} minute{(minutes == 1 ? "" : "s")}";
            
            var hours = minutes.Value / 60;
            var mins  = minutes.Value % 60;

            return mins == 0
                ? $"{hours} hour{(hours == 1 ? "" : "s")}"
                : $"{hours} hour{(hours == 1 ? "" : "s")} {mins} minute{(mins == 1 ? "" : "s")}";
        }

        public static string FormatRating(double? rating) =>
            rating is null
                ? string.Empty
                : rating.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        // ── Inner models ────────────────────────────────────────────────────────

        public sealed class DestinationApiResponse
        {
            public DestinationData? Destination { get; set; }
            public DestinationMeta? Meta { get; set; }
            public DestinationExtra? Extra { get; set; }
        }

        public sealed class DestinationData
        {
            public int DestinationId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string? DestinationUrl { get; set; }
            public string? DefaultCurrencyCode { get; set; }
            public string? TimeZone { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int ParentDestinationId { get; set; }
        }

        public sealed class DestinationMeta
        {
            public string Name { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string? Summary { get; set; }
            public string? Content { get; set; }
            public string? DestinationId { get; set; }
        }

        public sealed class DestinationExtra
        {
            public string? DealApiUrl { get; set; }
            public int? GetId { get; set; }
            public bool Image { get; set; }
        }

        public sealed class DestinationViewModel
        {
            public string Title { get; init; } = string.Empty;
            public string? Description { get; init; }
            public string? LongDescription { get; init; }
            public string Slug { get; init; } = string.Empty;
            public int Id { get; init; }
            public DestinationExtra? Extra { get; init; }
            public SearchProductsResponse? SearchProducts { get; init; }
            public List<CityBreakDeal>? Deals { get; init; }
        }
    }
}
