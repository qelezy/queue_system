using Microsoft.AspNetCore.Mvc;
using WebApplication.Models.Emails;

namespace WebApplication.ViewComponents.Emails;

public class PasswordResetEmailViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(PasswordResetEmailViewModel model) => View(model);
}
