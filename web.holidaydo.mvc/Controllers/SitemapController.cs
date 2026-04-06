using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace web.holidaydo.mvc.Controllers
{
    public class SitemapController : Controller
    {
        private const string TableName = "TableDestinations";
        private const string BaseUrl   = "https://www.holidaydo.com";
        private const string CacheKey = "sitemap_xml_cache";
        private const int CacheDurationMinutes = 60;

        private readonly TableClient _tableClient;
        private readonly ILogger<SitemapController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IMemoryCache _memoryCache;

        public SitemapController(
            IConfiguration configuration,
            ILogger<SitemapController> logger,
            IWebHostEnvironment webHostEnvironment,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _memoryCache = memoryCache;

            var connectionString = configuration["AzureWebJobsStorage"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("`AzureWebJobsStorage` is not configured.");

            _tableClient = new TableClient(connectionString, TableName);
        }

        [HttpGet("sitemap.xml")]
        public async Task<IActionResult> Destinations()
        {
            // Check if sitemap is in cache
            if (_memoryCache.TryGetValue(CacheKey, out string? cachedSitemap))
            {
                _logger.LogInformation("Returning cached sitemap");
                return Content(cachedSitemap, "application/xml; charset=utf-8");
            }

            try
            {
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

                var urlset = new XElement(ns + "urlset");
                var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                urlset.Add(
                    new XElement(ns + "url",
                        new XElement(ns + "loc",        BaseUrl),
                        new XElement(ns + "lastmod",    "2026-04-06"),
                        new XElement(ns + "changefreq", "weekly"),
                        new XElement(ns + "priority",   "1.0")));

                urlset.Add(
                    new XElement(ns + "url",
                        new XElement(ns + "loc", "https://www.holidaydo.com/all-destinations"),
                        new XElement(ns + "lastmod", DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd")),
                        new XElement(ns + "changefreq", "monthly"),
                        new XElement(ns + "priority", "1.0")));

                var regionsPath = Path.Combine(_webHostEnvironment.WebRootPath, "data", "regions.json");
                if (System.IO.File.Exists(regionsPath))
                {
                    await using var stream = System.IO.File.OpenRead(regionsPath);
                    var regions = await JsonSerializer.DeserializeAsync<List<RegionItem>>(stream)
                                  ?? [];

                    foreach (var region in regions)
                    {
                        var loc = $"{BaseUrl}/region/{Uri.EscapeDataString(region.Slug)}/{region.Id}";

                        urlset.Add(
                            new XElement(ns + "url",
                                new XElement(ns + "loc",        loc),
                                new XElement(ns + "lastmod",    "2026-04-06"),
                                new XElement(ns + "changefreq", "monthly"),
                                new XElement(ns + "priority",   "0.5")));
                    }
                }

                await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                    select: ["RowKey", "Slug"]))
                {
                    var rowKey = entity.RowKey;
                    var slug   = entity.GetString("Slug");

                    if (string.IsNullOrWhiteSpace(rowKey) || string.IsNullOrWhiteSpace(slug))
                        continue;

                    if (!seen.Add(rowKey))
                        continue;

                    var loc = $"{BaseUrl}/destination/{Uri.EscapeDataString(slug)}/{Uri.EscapeDataString(rowKey)}";

                    urlset.Add(
                        new XElement(ns + "url",
                            new XElement(ns + "loc",        loc),
                            new XElement(ns + "lastmod",    today),
                            new XElement(ns + "changefreq", "daily"),
                            new XElement(ns + "priority",   "0.8")));
                }

                var document = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    urlset);

                var sitemapContent = document.ToString(SaveOptions.DisableFormatting);

                // Cache the sitemap for 60 minutes
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(CacheDurationMinutes));

                _memoryCache.Set(CacheKey, sitemapContent, cacheOptions);
                _logger.LogInformation($"Sitemap cached for {CacheDurationMinutes} minutes");

                return Content(sitemapContent, "application/xml; charset=utf-8");
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Sitemap query failed.");
                return StatusCode(StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected sitemap generation error.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        private sealed record RegionItem(
            [property: JsonPropertyName("id")] int Id,
            [property: JsonPropertyName("slug")] string Slug);
    }
}