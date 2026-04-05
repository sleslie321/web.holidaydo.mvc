using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace web.holidaydo.mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private static readonly TopCountryLink[] TopCountries =
        [
            new() { Title = "Barbados", Slug = "barbados", Id = 30 },
            new() { Title = "Cape Verde", Slug = "cape-verde", Id = 4461 },
            new() { Title = "Cyprus", Slug = "cyprus", Id = 47 },
            new() { Title = "Greece", Slug = "greece", Id = 53 },
            new() { Title = "Iceland", Slug = "iceland", Id = 55 },
            new() { Title = "Ireland", Slug = "ireland", Id = 56 },
            new() { Title = "Italy", Slug = "italy", Id = 57 },
            new() { Title = "Japan", Slug = "japan", Id = 16 },
            new() { Title = "Mauritius", Slug = "mauritius", Id = 4463 },
            new() { Title = "Montenegro", Slug = "montenegro", Id = 4475 },
            new() { Title = "Morocco", Slug = "morocco", Id = 825 },
            new() { Title = "Portugal", Slug = "portugal", Id = 63 },
            new() { Title = "Singapore", Slug = "singapore", Id = 18 },
            new() { Title = "Spain", Slug = "spain", Id = 67 },
            new() { Title = "Sri Lanka", Slug = "sri-lanka", Id = 19 },
            new() { Title = "Switzerland", Slug = "switzerland", Id = 69 },
            new() { Title = "Thailand", Slug = "thailand", Id = 20 },
            new() { Title = "Tunisia", Slug = "tunisia", Id = 4393 },
            new() { Title = "Turkey", Slug = "turkey", Id = 70 },
            new() { Title = "United Kingdom", Slug = "united-kingdom", Id = 60457 }
        ];

        private static readonly TopDestinationLink[] TopDestinations =
        [
            new() { Title = "Amsterdam", Slug = "amsterdam", Id = 525 },
            new() { Title = "Paris", Slug = "paris", Id = 479 },
            new() { Title = "Rome", Slug = "rome", Id = 511 },
            new() { Title = "Milan", Slug = "milan", Id = 512 },
            new() { Title = "Barcelona", Slug = "barcelona", Id = 562 },
            new() { Title = "New York", Slug = "new-york", Id = 5560 },
            new() { Title = "Lisbon", Slug = "lisbon", Id = 538 },
            new() { Title = "London", Slug = "london", Id = 737 },
            new() { Title = "Krakow", Slug = "krakow", Id = 529 },
            new() { Title = "Crete", Slug = "crete", Id = 960 },
            new() { Title = "Manchester", Slug = "manchester", Id = 4056 },
            new() { Title = "Liverpool", Slug = "liverpool", Id = 940 },
            new() { Title = "Edinburgh", Slug = "edinburgh", Id = 739 },
            new() { Title = "Cardiff", Slug = "cardiff", Id = 5158 },
            new() { Title = "Cambridge", Slug = "cambridge", Id = 22327 },
            new() { Title = "Bath", Slug = "bath", Id = 27175 },
            new() { Title = "Tenerife", Slug = "tenerife", Id = 5404 },
            new() { Title = "Gran Canaria", Slug = "gran-canaria", Id = 792 },
            new() { Title = "Copenhagen", Slug = "copenhagen", Id = 463 }
        ];

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "HolidayDo - Do More on Holiday";
            ViewData["Description"] = "Discover tours, boat trips, water parks, family days out, city experiences, and hidden gems tailored to your next holiday.";

            var client = _httpClientFactory.CreateClient();

            using var response = await client.GetAsync("https://fnholidayo.azurewebsites.net/api/GetTopDestinations");

            List<TopDestination>? popularDestinations = [];

            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();

                popularDestinations = await JsonSerializer.DeserializeAsync<List<TopDestination>>(
                    stream,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }

            var model = new HomeIndexViewModel
            {
                PopularDestinations = popularDestinations ?? [],
                TopCountries = TopCountries,
                TopDestinations = TopDestinations
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Autocomplete(string q, int take = 10)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Json(Array.Empty<object>());
            }

            var client = _httpClientFactory.CreateClient();

            var url =
                $"https://fnholidayo.azurewebsites.net/api/destinations/autocomplete?q={Uri.EscapeDataString(q)}&take={take}";

            using var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return Json(Array.Empty<object>());
            }

            await using var stream = await response.Content.ReadAsStreamAsync();

            var results = await JsonSerializer.DeserializeAsync<List<DestinationSuggestion>>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return Json(results ?? []);
        }

        public sealed class HomeIndexViewModel
        {
            public List<TopDestination> PopularDestinations { get; init; } = [];
            public IReadOnlyList<TopCountryLink> TopCountries { get; init; } = [];
            public IReadOnlyList<TopDestinationLink> TopDestinations { get; init; } = [];
        }

        public sealed class DestinationSuggestion
        {
            public int Id { get; set; }
            public string RowKey { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
        }

        public sealed class TopDestination
        {
            public int Id { get; set; }
            public string Slug { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
        }

        public sealed class TopCountryLink
        {
            public string Title { get; init; } = string.Empty;
            public string Slug { get; init; } = string.Empty;
            public int Id { get; init; }
        }

        public sealed class TopDestinationLink
        {
            public string Title { get; init; } = string.Empty;
            public string Slug { get; init; } = string.Empty;
            public int Id { get; init; }
        }
    }
}
