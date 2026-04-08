using Microsoft.AspNetCore.Mvc;

namespace web.holidaydo.mvc.Controllers
{
    public class FlightFinderController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
