using Microsoft.AspNetCore.Mvc;

namespace WebApplication.ViewComponents.Users {
    public class UserRegistrationModalViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(UserRegistrationModalViewModel model)
        {
            return View(model);
        }
    }
}
