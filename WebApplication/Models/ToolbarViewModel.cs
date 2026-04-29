namespace MyWebApplication.Models
{
    public class ToolbarViewModel
    {
        public List<ToolbarButtonViewModel> LeftButtons { get; set; } = new();
        public List<ToolbarButtonViewModel> RightButtons { get; set; } = new();
    }

    public class ToolbarButtonViewModel
    {
        public string Text { get; set; } = "";
        public string CssClass { get; set; } = "btn-primary";
        public string Icon { get; set; } = "";
        public string OnClick { get; set; } = "";
    }
}
