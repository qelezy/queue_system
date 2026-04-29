using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace WebApplication.Controllers
{
    public class AccountController : Controller
    {
        public const string PostLoginRedirectPath = "/dashboard/index";

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            ViewData["LoginUrl"] = Url.Action("Login", "Auth", null, Request.Scheme);
            ViewData["PostLoginRedirectPath"] = PostLoginRedirectPath;
            return View();
        }

        [HttpGet]
        [Authorize]
        public IActionResult Profile() 
        {
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("accessToken");
            Response.Cookies.Delete("refreshToken");
            Response.Cookies.Delete("rememberMe");
            return RedirectToAction(nameof(Login));
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
