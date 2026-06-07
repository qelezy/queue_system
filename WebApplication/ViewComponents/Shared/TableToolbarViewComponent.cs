using Microsoft.AspNetCore.Mvc;
using WebApplication.Models.ViewModels.Shared;

namespace WebApplication.ViewComponents.Shared;

public class TableToolbarViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(TableToolbarViewModel? model = null)
    {
        return View(model ?? new TableToolbarViewModel());
    }
}
