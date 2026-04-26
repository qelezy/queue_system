using Microsoft.AspNetCore.Mvc;

namespace MyWebApplication.Controllers
{
    public class QueueController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
