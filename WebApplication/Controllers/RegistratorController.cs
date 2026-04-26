using Microsoft.AspNetCore.Mvc;

namespace MyWebApplication.Controllers
{
    public class RegistratorController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
