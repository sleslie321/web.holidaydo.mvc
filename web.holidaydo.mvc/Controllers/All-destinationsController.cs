using Microsoft.AspNetCore.Mvc;

namespace web.holidaydo.mvc.Controllers
{
    [Route("all-destinations")]
    public class All_destinationsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
