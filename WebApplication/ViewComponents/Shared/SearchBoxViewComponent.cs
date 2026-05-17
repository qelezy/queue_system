using Microsoft.AspNetCore.Mvc;

namespace WebApplication.ViewComponents.Shared {
    public class SearchBoxViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(SearchBoxViewModel? model = null)
        {
            return View(model ?? new SearchBoxViewModel());
        }
    }
}
