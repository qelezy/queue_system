using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyWebApplication.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        [HttpGet]
        public IActionResult RegisterUser()
        {
            ViewData["RegisterUserUrl"] = Url.Action("Register", "User", null, Request.Scheme);
            return View();
        }

        [HttpGet]
        public IActionResult UpdateUser()
        {
            ViewData["UpdateUserUrl"] = Url.Action("Update", "User", null, Request.Scheme);
            return View();
        }

        [HttpGet]
        public IActionResult DeleteUser()
        {
            ViewData["DeleteUserUrl"] = Url.Action("Delete", "User", null, Request.Scheme);
            return View();
        }
    }
}
