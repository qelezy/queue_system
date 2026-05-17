namespace WebApplication.Models;

public static class ReportsUiConfiguration
{
    public static IReadOnlyDictionary<ReportGeneratorKind, IReadOnlyList<ReportCustomFieldDefinition>> FieldsByGeneratorKind { get; } =
        new Dictionary<ReportGeneratorKind, IReadOnlyList<ReportCustomFieldDefinition>>
        {
            [ReportGeneratorKind.LoadAndDowntime] =
            [
                new ReportCustomFieldDefinition
                {
                    Key = "analysisMode",
                    Label = "Срез",
                    Type = "select",
                    Options =
                    [
                        new ReportCustomFieldOption { Value = "doctor", Label = "По врачам" },
                        new ReportCustomFieldOption { Value = "cabinet", Label = "По кабинетам" }
                    ]
                }
            ],
            [ReportGeneratorKind.AppointmentDuration] =
            [
                new ReportCustomFieldDefinition
                {
                    Key = "analysisMode",
                    Label = "Срез",
                    Type = "select",
                    Options =
                    [
                        new ReportCustomFieldOption { Value = "doctor", Label = "По врачам" },
                        new ReportCustomFieldOption { Value = "specialty", Label = "По специальностям" },
                        new ReportCustomFieldOption { Value = "cabinet", Label = "По кабинетам" }
                    ]
                }
            ],
            [ReportGeneratorKind.ServiceDelays] =
            [
                new ReportCustomFieldDefinition
                {
                    Key = "analysisMode",
                    Label = "Срез",
                    Type = "select",
                    Options =
                    [
                        new ReportCustomFieldOption { Value = "doctor", Label = "По врачам" },
                        new ReportCustomFieldOption { Value = "cabinet", Label = "По кабинетам" }
                    ]
                }
            ],
            [ReportGeneratorKind.ServiceCategoriesComparison] = []
        };

    public static IReadOnlyDictionary<string, IReadOnlyList<ReportCustomFieldDefinition>> BuildCustomConfigByReportId(
        IReadOnlyList<ReportCatalogItemViewModel> catalog)
    {
        var dict = new Dictionary<string, IReadOnlyList<ReportCustomFieldDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in catalog)
        {
            if (FieldsByGeneratorKind.TryGetValue(item.GeneratorKind, out var fields))
                dict[item.Id] = fields;
            else
                dict[item.Id] = [];
        }

        return dict;
    }
}

public sealed class ReportCustomFieldDefinition
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Type { get; init; } = "text";
    public IReadOnlyList<ReportCustomFieldOption> Options { get; init; } = [];
}

public sealed class ReportCustomFieldOption
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
}
