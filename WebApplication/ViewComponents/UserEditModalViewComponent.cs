using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

namespace WebApplication.ViewComponents
{
    public class UserEditModalViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(UserEditModalViewModel model)
        {
            return View(model);
        }
    }
}
