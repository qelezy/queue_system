using Microsoft.AspNetCore.Mvc;

namespace MyWebApplication.Controllers
{
    public class ManagerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
