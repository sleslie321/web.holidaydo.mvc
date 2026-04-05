using System.Xml.Linq;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;

namespace web.holidaydo.mvc.Controllers
{
    public class SitemapController : Controller
    {
        private const string TableName = "TableDestinations";
        private const string BaseUrl   = "https://www.holidaydo.com";

        private readonly TableClient _tableClient;
        private readonly ILogger<SitemapController> _logger;

        public SitemapController(IConfiguration configuration, ILogger<SitemapController> logger)
        {
            _logger = logger;

            var connectionString = configuration["AzureWebJobsStorage"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("`AzureWebJobsStorage` is not configured.");

            _tableClient = new TableClient(connectionString, TableName);
        }

        [HttpGet("sitemap.xml")]
        public async Task<IActionResult> Destinations()
        {
            try
            {
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

                var urlset = new XElement(ns + "urlset");
                var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                return Content(
                    document.ToString(SaveOptions.DisableFormatting),
                    "application/xml; charset=utf-8");
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
    }
}