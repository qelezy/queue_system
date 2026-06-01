namespace WebApplication.Models.Reports.Charts;

public static class ChartDatasetValues
{
    public const double Missing = double.NaN;

    public static bool IsMissing(double value) => !double.IsFinite(value);

    public static bool HasFiniteValue(IEnumerable<double> values) =>
        values.Any(static v => double.IsFinite(v));
}
