using Microsoft.AspNetCore.Mvc;

namespace WebApplication.ViewComponents.Shared {
    public class TableToolbarViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(UsersToolbarViewModel? model = null)
        {
            return View(model ?? new UsersToolbarViewModel());
        }
    }
}