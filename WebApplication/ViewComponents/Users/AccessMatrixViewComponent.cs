using Microsoft.AspNetCore.Mvc;

namespace WebApplication.ViewComponents.Users;

public class AccessMatrixViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(AccessSettingsViewModel? model = null)
    {
        return View(model ?? new AccessSettingsViewModel());
    }
}
