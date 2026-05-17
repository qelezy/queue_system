namespace WebApplication.Models.Reports.Configuration;

public static class ReportGeneratorKindParser
{
    public static bool TryParse(string? raw, out ReportGeneratorKind kind)
    {
        kind = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return Enum.TryParse(raw.Trim(), ignoreCase: true, out kind);
    }

    public static ReportGeneratorKind ParseRequired(string? raw, string context)
    {
        if (!TryParse(raw, out var kind))
            throw new InvalidOperationException($"Недопустимый GeneratorKind «{raw}» ({context}).");

        return kind;
    }
}
