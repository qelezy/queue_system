using Microsoft.AspNetCore.Mvc;

namespace WebApplication.Controllers
{
    public class ReportsController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = "Отчёты";
            return View();
        }
    }
}