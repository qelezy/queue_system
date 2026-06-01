using Microsoft.AspNetCore.Mvc;
using WebApplication.Models.Emails;

namespace WebApplication.ViewComponents.Emails;

public class RegistrationEmailViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(RegistrationEmailViewModel model) => View(model);
}
