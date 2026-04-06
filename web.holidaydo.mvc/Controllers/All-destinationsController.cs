using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace web.holidaydo.mvc.Controllers
{
    [Route("all-destinations")]
    public class All_destinationsController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<All_destinationsController> _logger;
        private readonly IMemoryCache _memoryCache;

        private const string CacheKey = "all_destinations_cache";
        private const int CacheDurationMinutes = 60;

        public All_destinationsController(
            IWebHostEnvironment webHostEnvironment,
            IHttpClientFactory httpClientFactory,
            ILogger<All_destinationsController> logger,
            IMemoryCache memoryCache)
        {
            _webHostEnvironment = webHostEnvironment;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "HolidayDo - Do More on Holiday - View All Desitinations";
            ViewData["Description"] = "Discover tours, boat trips, water parks, family days out, city experiences, and hidden gems tailored to your next holiday.";

            try
            {
                // Check if data is in cache
                if (_memoryCache.TryGetValue(CacheKey, out List<RegionWithCountries>? cachedRegions))
                {
                    _logger.LogInformation("Returning cached all destinations data");
                    return View(cachedRegions);
                }

                var regionsPath = Path.Combine(_webHostEnvironment.WebRootPath, "data", "regions.json");
                List<RegionItem> regions = [];

                if (System.IO.File.Exists(regionsPath))
                {
                    await using var stream = System.IO.File.OpenRead(regionsPath);
                    regions = await JsonSerializer.DeserializeAsync<List<RegionItem>>(
                                   stream,
                                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                               ?? [];
                }

                // Sort regions A-Z by title
                regions = regions.OrderBy(r => r.Title).ToList();

                // Fetch countries for each region
                var regionsWithCountries = new List<RegionWithCountries>();
                var httpClient = _httpClientFactory.CreateClient();

                foreach (var region in regions)
                {
                    try
                    {
                        var apiUrl = $"https://fnholidayo.azurewebsites.net/api/fnRegionReturnCountrys?id={region.Id}";
                        var response = await httpClient.GetAsync(apiUrl);

                        var countries = new List<Country>();
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            
                            _logger.LogInformation($"API Response for region {region.Id}: {content}");
                            
                            countries = JsonSerializer.Deserialize<List<Country>>(
                                content,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                        }

                        regionsWithCountries.Add(new RegionWithCountries
                        {
                            Id = region.Id,
                            Title = region.Title,
                            Countries = countries.OrderBy(c => c.Name).ToList()
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to fetch countries for region {region.Id}");
                    }
                }

                // Cache the result for 60 minutes
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(CacheDurationMinutes));

                _memoryCache.Set(CacheKey, regionsWithCountries, cacheOptions);
                _logger.LogInformation($"All destinations data cached for {CacheDurationMinutes} minutes");

                return View(regionsWithCountries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all destinations");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        private sealed record RegionItem(
            [property: JsonPropertyName("id")] int Id,
            [property: JsonPropertyName("title")] string Title,
            [property: JsonPropertyName("slug")] string Slug);

        public sealed class RegionWithCountries
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public List<Country> Countries { get; set; } = [];
        }

        public sealed class Country
        {
            [JsonPropertyName("destinationId")]
            public int Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("slug")]
            public string Slug { get; set; } = string.Empty;
        }
    }
}
