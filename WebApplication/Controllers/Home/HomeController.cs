using Microsoft.AspNetCore.Mvc;

namespace WebApplication.Controllers.Home {
    public class HomeController : Controller
    {
        [HttpGet("/")]
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction("Index", "Dashboard");
        }
    }
}
