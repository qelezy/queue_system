using Microsoft.AspNetCore.Mvc;

namespace MyWebApplication.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet("/")]
        public IActionResult Index()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction("Index", "Dashboard");
        }
    }
}
