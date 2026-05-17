using Microsoft.AspNetCore.Mvc;

namespace WebApplication.ViewComponents.Shared {
    public class ToastStackViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(ToastStackViewModel? model = null)
        {
            return View(model ?? new ToastStackViewModel());
        }
    }
}
