namespace WebApplication.Models;

public class TabsBarViewModel
{
    public required IReadOnlyList<TabItemViewModel> Tabs { get; init; }
}

public class TabItemViewModel
{
    public required string Label { get; init; }
    public required string Href { get; init; }
    public bool IsActive { get; init; }
}

