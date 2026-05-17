using Microsoft.AspNetCore.Mvc;

namespace WebApplication.ViewComponents.Users {
    public class UserEditModalViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(UserEditModalViewModel model)
        {
            return View(model);
        }
    }
}
