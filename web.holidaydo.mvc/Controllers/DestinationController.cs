using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _memoryCache;

        private const int CacheDurationMinutes = 15;

        public DestinationController(
            IHttpClientFactory httpClientFactory,
            WowcherService wowcherService,
            IMemoryCache memoryCache)
        {
            _httpClientFactory = httpClientFactory;
            _wowcherService = wowcherService;
            _memoryCache = memoryCache;
        }

        public async Task<IActionResult> Index(string slug, int id)
        {
            var cacheKey = $"destination_{slug}_{id}";

            if (_memoryCache.TryGetValue(cacheKey, out DestinationViewModel? cachedViewModel))
            {
                return View(cachedViewModel);
            }

            var client = _httpClientFactory.CreateClient();

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

            List<CityBreakDeal>? deals = null;
            if (apiResponse.Extra?.DealApiUrl is not null)
                deals = await _wowcherService.GetDestinationDealsAsync(apiResponse.Extra.DealApiUrl);

            var destinationType = apiResponse.Destination?.Type ?? string.Empty;
            var destinationId = apiResponse.Destination?.DestinationId ?? 0;

            List<DestinationLink> countryCities = [];
            if (destinationType == "COUNTRY" && destinationId > 0)
            {
                using var treeResponse = await client.GetAsync(
                    $"https://fnholidayo.azurewebsites.net/api/destination/tree/{destinationId}");

                if (treeResponse.IsSuccessStatusCode)
                {
                    await using var treeStream = await treeResponse.Content.ReadAsStreamAsync();
                    var tree = await JsonSerializer.DeserializeAsync<List<DestinationTreeNode>>(
                        treeStream, JsonOptions) ?? [];

                    countryCities = GetLeafDestinations(tree)
                        .Where(x =>
                            x.DestinationId != destinationId &&
                            string.Equals(x.Type, "CITY", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => x.Name)
                        .Select(x => new DestinationLink
                        {
                            DestinationId = x.DestinationId,
                            Name = x.Name,
                            Slug = ToSlug(x.Name)
                        })
                        .ToList();
                }
            }

            var title = apiResponse.Meta?.Name ?? apiResponse.Destination?.Name ?? FormatSlug(slug);

            ViewData["Title"] = $"Holiday Activities for {title} - Do More on Holiday";
            ViewData["Description"] = $"{apiResponse.Meta?.Summary} - {title}";

            var viewModel = new DestinationViewModel
            {
                Title = title,
                Description = apiResponse.Meta?.Summary,
                LongDescription = apiResponse.Meta?.Content,
                Type = destinationType,
                DestinationId = destinationId,
                Slug = slug,
                Id = id,
                Extra = apiResponse.Extra,
                SearchProducts = searchProducts,
                Deals = deals,
                CountryCities = countryCities
            };

            ViewData["Title"] = "Find Great Activites for " + viewModel.Title + " | HolidayDo - Do More on Holiday";
            ViewData["Description"] = viewModel.Description;

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CacheDurationMinutes));

            _memoryCache.Set(cacheKey, viewModel, cacheOptions);

            return View(viewModel);
        }

        private static IEnumerable<DestinationTreeNode> GetLeafDestinations(IEnumerable<DestinationTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Children is null || node.Children.Count == 0)
                {
                    yield return node;
                    continue;
                }

                foreach (var child in GetLeafDestinations(node.Children))
                {
                    yield return child;
                }
            }
        }

        private static string ToSlug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new System.Text.StringBuilder();
            var previousWasDash = false;

            foreach (var ch in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    previousWasDash = false;
                }
                else if (!previousWasDash)
                {
                    builder.Append('-');
                    previousWasDash = true;
                }
            }

            return builder.ToString().Trim('-');
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

        public sealed class DestinationTreeNode
        {
            public int DestinationId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public int ParentDestinationId { get; set; }
            public List<DestinationTreeNode> Children { get; set; } = [];
        }

        public sealed class DestinationLink
        {
            public int DestinationId { get; init; }
            public string Name { get; init; } = string.Empty;
            public string Slug { get; init; } = string.Empty;
        }

        public sealed class DestinationViewModel
        {
            public string Title { get; init; } = string.Empty;
            public string? Description { get; init; }
            public string? LongDescription { get; init; }
            public string Type { get; init; } = string.Empty;
            public int DestinationId { get; init; }
            public string Slug { get; init; } = string.Empty;
            public int Id { get; init; }
            public DestinationExtra? Extra { get; init; }
            public SearchProductsResponse? SearchProducts { get; init; }
            public List<CityBreakDeal>? Deals { get; init; }
            public List<DestinationLink> CountryCities { get; init; } = [];
        }
    }
}
