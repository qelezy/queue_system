using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication.Controllers.Home {
    public class HomeController : Controller
    {
        [HttpGet("/")]
        [AllowAnonymous]
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
