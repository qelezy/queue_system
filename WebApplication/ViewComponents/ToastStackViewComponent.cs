using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

namespace WebApplication.ViewComponents
{
    public class ToastStackViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(ToastStackViewModel? model = null)
        {
            return View(model ?? new ToastStackViewModel());
        }
    }
}
