namespace WebApplication.Models.ViewModels.Shared;

public sealed class TableToolbarFilterOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class TableToolbarFilterViewModel
{
    public string SelectId { get; set; } = "table-toolbar-filter";
    public string AllLabel { get; set; } = "Все";
    public string OnChange { get; set; } = string.Empty;
    public IReadOnlyList<TableToolbarFilterOptionViewModel> Options { get; set; } = [];
}

public sealed class TableToolbarViewModel
{
    public SearchBoxViewModel Search { get; set; } = new();
    public TableToolbarFilterViewModel? Filter { get; set; }
    public bool ShowArchivedToggle { get; set; }
    public string ArchivedToggleInputId { get; set; } = "table-toolbar-show-archived";
    public string ArchivedToggleOnChange { get; set; } = string.Empty;
    public string CreateButtonText { get; set; } = string.Empty;
    public string CreateButtonOnClick { get; set; } = string.Empty;
    public string CreateButtonIcon { get; set; } = "bi-plus-lg";
}
