namespace WebApplication.Models
{
    public class SearchBoxViewModel
    {
        public string InputId { get; set; } = "users-search-input";
        public string Placeholder { get; set; } = "Поиск";
        public string OnInput { get; set; } = string.Empty;
        public string InputCssClass { get; set; } = "toolbar-search-input";
    }
}
