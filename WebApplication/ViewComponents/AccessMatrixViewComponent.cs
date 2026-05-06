using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

namespace WebApplication.ViewComponents;

public class AccessMatrixViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(AccessSettingsViewModel? model = null)
    {
        return View(model ?? new AccessSettingsViewModel());
    }
}
