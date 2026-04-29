using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

namespace WebApplication.ViewComponents
{
    public class SearchBoxViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(SearchBoxViewModel? model = null)
        {
            return View(model ?? new SearchBoxViewModel());
        }
    }
}
