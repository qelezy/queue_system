using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

namespace WebApplication.ViewComponents
{
    public class UserRegistrationModalViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(UserRegistrationModalViewModel model)
        {
            return View(model);
        }
    }
}
