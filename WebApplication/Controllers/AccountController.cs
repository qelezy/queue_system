using Microsoft.AspNetCore.Mvc;

namespace MyWebApplication.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            ViewData["LoginUrl"] = Url.Action("Login", "Auth", null, Request.Scheme);
            return View();
        }

        [HttpGet]
        public IActionResult ConfirmEmail(string userId, string token)
        {
            ViewData["UserId"] = userId;
            ViewData["Token"] = Uri.EscapeDataString(token);
            ViewData["ConfirmEmailUrl"] = Url.Action("ConfirmEmail", "Auth", null, Request.Scheme);
            return View();
        }

        [HttpGet]
        public IActionResult ForgotPassword(string email, string token)
        {
            ViewData["Email"] = email;
            ViewData["Token"] = token;
            ViewData["ForgotUrl"] = Url.Action("ForgotPassword", "Auth", null, Request.Scheme);
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string userId, string token)
        {
            ViewData["UserId"] = userId;
            ViewData["Token"] = Uri.EscapeDataString(token);
            ViewData["ResetUrl"] = Url.Action("ResetPassword", "Auth", null, Request.Scheme);
            return View();
        }
    }
}
