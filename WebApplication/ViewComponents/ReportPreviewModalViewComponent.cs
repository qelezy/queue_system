using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

namespace WebApplication.ViewComponents;

public class ReportPreviewModalViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ReportPreviewModalViewModel? model)
    {
        return View(model ?? new ReportPreviewModalViewModel());
    }
}
