namespace WebApplication.Models.Reports.Charts;

public static class HorizontalGroupedBarChartMetrics
{
    public const double ExportCategorySlotHeight = 22;
    public const double CategoryPercentage = 0.88;
    public const int ExportCategoryLabelFontSize = 10;
    public const int ExportAxisTickFontSize = 9;
    public const string CategoryLabelColor = "#1e293b";

    public static double ExportCategoryGap =>
        ExportCategorySlotHeight * (1.0 / CategoryPercentage - 1.0);

    public static double ExportPlotHeight(int categoryCount)
    {
        if (categoryCount <= 0)
            return 0;

        return categoryCount * ExportCategorySlotHeight
               + (categoryCount - 1) * ExportCategoryGap;
    }

    public static double ExportCategoryGroupY(double originY, int categoryIndex) =>
        originY + categoryIndex * (ExportCategorySlotHeight + ExportCategoryGap);

    public static IReadOnlyList<double> BuildAxisTickValues(double maxVal)
    {
        if (maxVal <= 0)
            return [0];

        var step = Math.Max(1, (int)Math.Ceiling(maxVal / 4.0));
        var ticks = new List<double>();
        for (var t = 0.0; t < maxVal; t += step)
            ticks.Add(t);

        ticks.Add(maxVal);
        return ticks;
    }

    public static bool IsSymmetricAxisMode(string? chartAxisMode) =>
        string.Equals(chartAxisMode?.Trim(), "symmetric", StringComparison.OrdinalIgnoreCase);

    public static (double AxisMin, double AxisMax) ResolveSplitAxisBounds(IEnumerable<double> values)
    {
        var posMax = 0.0;
        var negMax = 0.0;
        foreach (var v in values)
        {
            if (!double.IsFinite(v))
                continue;

            if (v > posMax)
                posMax = v;
            else if (v < 0)
                negMax = Math.Max(negMax, -v);
        }

        var posExtent = posMax > 0 ? Math.Max(posMax, DisplayMinutesExtentBound(posMax)) : 0;
        var negExtent = negMax > 0 ? Math.Max(negMax, DisplayMinutesExtentBound(negMax)) : 0;
        var axisMax = posExtent > 0 ? AxisTickUpperBound(posExtent) : 0;
        var axisMin = negExtent > 0 ? -AxisTickUpperBound(negExtent) : 0;

        if (axisMax <= 0 && axisMin >= 0)
            return (0, 1);

        return (axisMin, axisMax);
    }

    private static double DisplayMinutesExtentBound(double extentMinutes)
    {
        if (extentMinutes <= 0)
            return 0;

        var totalSeconds = (int)Math.Round(extentMinutes * 60.0, MidpointRounding.AwayFromZero);
        var abs = Math.Abs(totalSeconds);
        if (abs < 60)
            return abs / 60.0;

        return Math.Round((double)abs / 60, MidpointRounding.AwayFromZero);
    }

    private static double AxisTickUpperBound(double extent)
    {
        if (extent <= 0)
            return 0;

        var ticks = BuildAxisTickValues(extent);
        return ticks[^1];
    }

    public static IReadOnlyList<double> BuildBidirectionalAxisTickValues(double axisMin, double axisMax)
    {
        var ticks = new HashSet<double> { 0 };

        if (axisMin < 0)
        {
            foreach (var t in BuildAxisTickValues(-axisMin))
            {
                if (t > 0)
                    ticks.Add(-t);
            }

            ticks.Add(axisMin);
        }

        if (axisMax > 0)
        {
            foreach (var t in BuildAxisTickValues(axisMax))
                ticks.Add(t);
        }

        return ticks.OrderBy(static x => x).ToList();
    }
}
