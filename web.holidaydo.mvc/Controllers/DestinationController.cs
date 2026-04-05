using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace web.holidaydo.mvc.Controllers
{
    public class DestinationController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DestinationController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index(string slug, int id)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://fnholidayo.azurewebsites.net/api/destinations/{slug}";

            using var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return View((DestinationViewModel?)null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var apiResponse = await JsonSerializer.DeserializeAsync<DestinationApiResponse>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResponse is null)
            {
                return View((DestinationViewModel?)null);
            }

            var viewModel = new DestinationViewModel
            {
                Title       = apiResponse.Meta?.Name ?? apiResponse.Destination?.Name ?? slug,
                Description = apiResponse.Meta?.Summary,
                Content     = apiResponse.Meta?.Content,
                Slug        = slug,
                Id          = id,
                Destination = apiResponse.Destination,
                Meta        = apiResponse.Meta,
                Extra       = apiResponse.Extra
            };

            return View(viewModel);
        }

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
            public int GetId { get; set; }
            public bool Image { get; set; }
        }

        public sealed class DestinationViewModel
        {
            public string Title { get; init; } = string.Empty;
            public string? Description { get; init; }
            public string? Content { get; init; }
            public string Slug { get; init; } = string.Empty;
            public int Id { get; init; }
            public DestinationData? Destination { get; init; }
            public DestinationMeta? Meta { get; init; }
            public DestinationExtra? Extra { get; init; }
        }
    }
}
