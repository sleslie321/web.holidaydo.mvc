using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace web.holidaydo.mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

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

            if (!response.IsSuccessStatusCode)
            {
                return View(new List<TopDestination>());
            }

            await using var stream = await response.Content.ReadAsStreamAsync();

            var topDestinations = await JsonSerializer.DeserializeAsync<List<TopDestination>>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return View(topDestinations ?? []);
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
    }
}
