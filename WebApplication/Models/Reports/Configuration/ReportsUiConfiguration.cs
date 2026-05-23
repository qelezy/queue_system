using System.Globalization;
using WebApplication.Models.Reports.Contracts;

namespace WebApplication.Models.Reports.Configuration;

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

    public static IReadOnlyList<string> FormatAppliedParameterLines(
        ReportGeneratorKind kind,
        IReadOnlyDictionary<string, string?>? customParams)
    {
        if (!FieldsByGeneratorKind.TryGetValue(kind, out var fields) || fields.Count == 0)
            return [];

        var lines = new List<string>();
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key))
                continue;

            var display = ResolveFieldDisplayValue(field, customParams);
            if (!string.IsNullOrWhiteSpace(display))
                lines.Add($"{field.Label}: {display}");
        }

        return lines;
    }

    public static IReadOnlyList<string> FormatExportHeaderLines(ReportGeneratorKind kind, ReportGenerateRequest request)
    {
        var lines = new List<string>();
        var periodLine = FormatPeriodLine(request);
        if (!string.IsNullOrWhiteSpace(periodLine))
            lines.Add(periodLine);

        return lines;
    }

    private static string ResolveFieldDisplayValue(
        ReportCustomFieldDefinition field,
        IReadOnlyDictionary<string, string?>? customParams)
    {
        var raw = customParams is not null && customParams.TryGetValue(field.Key, out var v) ? v : null;
        var value = raw?.Trim() ?? "";

        if (string.IsNullOrEmpty(value)
            && string.Equals(field.Type, "select", StringComparison.OrdinalIgnoreCase)
            && field.Options.Count > 0)
            value = field.Options[0].Value;

        if (string.IsNullOrEmpty(value))
            return "";

        if (string.Equals(field.Type, "select", StringComparison.OrdinalIgnoreCase))
        {
            var opt = field.Options.FirstOrDefault(o =>
                string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(opt?.Label))
                return opt.Label;
        }

        return value;
    }

    private static string? FormatPeriodLine(ReportGenerateRequest r)
    {
        var d0 = r.DateFrom?.Trim();
        var d1 = r.DateTo?.Trim();
        if (string.IsNullOrEmpty(d0) && string.IsNullOrEmpty(d1))
        {
            if (!string.IsNullOrWhiteSpace(r.WeekStart)
                && DateTime.TryParse(r.WeekStart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var mon))
            {
                var end = mon.Date.AddDays(6);
                return "Период (неделя): " + mon.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"))
                    + " — " + end.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
            }

            return null;
        }

        static string Part(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "…";
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
            return s;
        }

        return "Период: " + Part(d0) + " — " + Part(d1);
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
