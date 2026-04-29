namespace WebApplication.Models
{
    public class SidebarViewModel
    {
        public string ActiveKey { get; set; } = "";
        public List<SidebarItem> MenuItems { get; set; } = [];
        public string UserEmail { get; set; } = "";
        public string UserFullName { get; set; } = "";
    }

    public record SidebarItem(string Key, string Label, string Href);
}
