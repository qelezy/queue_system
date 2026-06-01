using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApplication.Services;

namespace WebApplication.Controllers.Account {
    public class AccountController : Controller
    {
        public const string PostLoginRedirectPath = "/dashboard/index";

        private readonly IAuthService _authService;

        public AccountController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            ViewData["Title"] = "Доступ запрещён";
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            ViewData["LoginUrl"] = Url.Action("Login", "Auth", null, Request.Scheme);
            ViewData["PostLoginRedirectPath"] = PostLoginRedirectPath;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            Request.Cookies.TryGetValue(AuthCookieHelper.RefreshTokenCookieName, out var refreshFromCookie);
            await _authService.LogoutAsync(User, refreshFromCookie);

            AuthCookieHelper.DeleteAuthCookies(Response);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ConfirmEmail(string userId, string token)
        {
            ViewData["UserId"] = userId;
            ViewData["Token"] = token;
            ViewData["ConfirmEmailUrl"] = Url.Action("ConfirmEmail", "Auth", null, Request.Scheme);
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword(string email, string token)
        {
            ViewData["Email"] = email;
            ViewData["Token"] = token;
            ViewData["ForgotUrl"] = Url.Action("ForgotPassword", "Auth", null, Request.Scheme);
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string userId, string token)
        {
            ViewData["UserId"] = userId;
            ViewData["Token"] = token;
            ViewData["ResetUrl"] = Url.Action("ResetPassword", "Auth", null, Request.Scheme);
            return View();
        }
    }
}
