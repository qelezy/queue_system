using Microsoft.AspNetCore.Mvc;

namespace WebApplication.ViewComponents
{
    public class FormPanelViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(string title, string formView)
        {
            ViewBag.Title = title;
            return View("Default", formView);
        }
    }
}