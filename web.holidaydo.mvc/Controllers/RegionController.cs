using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace web.holidaydo.mvc.Controllers
{
    public class RegionController : Controller
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;

        public RegionController(IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
        {
            _httpClientFactory = httpClientFactory;
            _env = env;
        }

        public async Task<IActionResult> Index(string slug, int id)
        {
            var lookup = await ReadRegionsJsonAsync();
            var match  = lookup.FirstOrDefault(x => x.Id == id);

            var title       = string.IsNullOrWhiteSpace(match?.Title) ? FormatSlug(slug) : match.Title;
            var description = match?.Description ?? "No description available.";

            ViewData["Title"]       = $"Holiday Activities for {title} - Do More on Holiday";
            ViewData["Description"] = $"{description} - {title}";

            List<RegionDestination> regions = [];
            string? errorMessage = null;

            try
            {
                var client = _httpClientFactory.CreateClient();
                using var response = await client.GetAsync(
                    $"https://fnholidayo.azurewebsites.net/api/fnRegion?id={id}");

                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync();
                    regions = await JsonSerializer.DeserializeAsync<List<RegionDestination>>(
                        stream, JsonOptions) ?? [];
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to load region data: {ex.Message}";
            }

            return View(new RegionViewModel
            {
                Title        = title,
                Description  = description,
                Slug         = slug,
                Id           = id,
                Regions      = regions,
                ErrorMessage = errorMessage
            });
        }

        private async Task<List<RegionLookup>> ReadRegionsJsonAsync()
        {
            try
            {
                var path = Path.Combine(_env.WebRootPath, "data", "regions.json");
                await using var stream = System.IO.File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<List<RegionLookup>>(stream, JsonOptions) ?? [];
            }
            catch
            {
                return [];
            }
        }

        public static string GetIconClass(string? type) =>
            type?.Trim().ToUpperInvariant() switch
            {
                "COUNTRY"       => "fa-solid fa-earth-europe",
                "REGION"        => "fa-solid fa-globe",
                "PROVINCE"      => "fa-solid fa-tree-city",
                "CITY"          => "fa-solid fa-city",
                "TOWN"          => "fa-solid fa-building",
                "ISLAND"        => "fa-solid fa-umbrella-beach",
                "NATIONAL_PARK" => "fa-solid fa-tree",
                _               => "fa-solid fa-location-dot"
            };

        public static string FormatSlug(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return "Destinations";
            return char.ToUpper(slug[0]) + slug[1..];
        }

        // ── Inner models ────────────────────────────────────────────────────────

        private sealed class RegionLookup
        {
            public int Id { get; set; }
            public string Slug { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
        }

        public sealed class RegionDestination
        {
            public int DestinationId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public int ParentDestinationId { get; set; }
            public List<RegionDestination> Children { get; set; } = [];
        }

        public sealed class RegionViewModel
        {
            public string Title { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public string Slug { get; init; } = string.Empty;
            public int Id { get; init; }
            public List<RegionDestination> Regions { get; init; } = [];
            public string? ErrorMessage { get; init; }
        }
    }
}
