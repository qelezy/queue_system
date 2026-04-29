using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

namespace WebApplication.ViewComponents
{
    public class TableToolbarViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(UsersToolbarViewModel? model = null)
        {
            return View(model ?? new UsersToolbarViewModel());
        }
    }
}