using System.Globalization;
using WebApplication.Models.Reports.Charts;
using WebApplication.Services.Reports.Catalog;

namespace WebApplication.Services.Reports.Charts;

public enum GroupedBarBucketAggregation
{
    Sum,
    Average
}

public sealed record GroupedBarTimeAxisResult(
    IReadOnlyList<string> Labels,
    IReadOnlyList<ReportPreviewChartDataset> Datasets,
    bool IsWeekly);

public static class GroupedBarChartTimeAxis
{
    public const int WeeklyThresholdDays = 21;

    public static GroupedBarTimeAxisResult Prepare(
        IReadOnlyList<DateOnly> days,
        IReadOnlyList<ReportPreviewChartDataset> datasets,
        GroupedBarBucketAggregation aggregation)
    {
        if (days.Count == 0)
            return new([], CloneDatasets(datasets), false);

        var normalized = NormalizeDatasets(datasets, days.Count);

        if (days.Count <= WeeklyThresholdDays)
        {
            var labels = days.Select(CatalogReportShared.FormatChartDayLabel).ToList();
            return new(labels, normalized, false);
        }

        var buckets = BuildWeekBuckets(days);
        var weekLabels = buckets
            .Select(b => FormatWeekLabel(days[b.FirstIndex], days[b.LastIndex]))
            .ToList();
        var aggregated = normalized
            .Select(ds => AggregateDataset(ds, buckets, aggregation))
            .ToList();

        return new(weekLabels, aggregated, true);
    }

    private readonly record struct WeekBucket(int FirstIndex, int LastIndex);

    private static List<WeekBucket> BuildWeekBuckets(IReadOnlyList<DateOnly> days)
    {
        var buckets = new List<WeekBucket>();
        if (days.Count == 0)
            return buckets;

        var bucketStart = 0;
        var currentWeek = GetWeekStartMonday(days[0]);

        for (var i = 1; i < days.Count; i++)
        {
            var week = GetWeekStartMonday(days[i]);
            if (week == currentWeek)
                continue;

            buckets.Add(new WeekBucket(bucketStart, i - 1));
            bucketStart = i;
            currentWeek = week;
        }

        buckets.Add(new WeekBucket(bucketStart, days.Count - 1));
        return buckets;
    }

    private static DateOnly GetWeekStartMonday(DateOnly day)
    {
        var offset = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-offset);
    }

    private static string FormatWeekLabel(DateOnly first, DateOnly last) =>
        first.ToString("dd.MM", CultureInfo.InvariantCulture)
        + "–"
        + last.ToString("dd.MM", CultureInfo.InvariantCulture);

    private static ReportPreviewChartDataset AggregateDataset(
        ReportPreviewChartDataset dataset,
        IReadOnlyList<WeekBucket> buckets,
        GroupedBarBucketAggregation aggregation)
    {
        var values = buckets
            .Select(b => AggregateSlice(dataset.Values, b.FirstIndex, b.LastIndex, aggregation))
            .ToList();

        List<double>? normValues = null;
        if (dataset.NormValues is not null)
        {
            normValues = buckets
                .Select(b => AggregateSlice(dataset.NormValues, b.FirstIndex, b.LastIndex, aggregation))
                .ToList();
        }

        return new ReportPreviewChartDataset
        {
            Label = dataset.Label,
            Values = values,
            NormValues = normValues,
            ChartSeriesType = dataset.ChartSeriesType
        };
    }

    private static double AggregateSlice(
        IReadOnlyList<double> values,
        int firstIndex,
        int lastIndex,
        GroupedBarBucketAggregation aggregation)
    {
        var slice = new List<double>(lastIndex - firstIndex + 1);
        for (var i = firstIndex; i <= lastIndex; i++)
            slice.Add(i < values.Count ? values[i] : ChartDatasetValues.Missing);

        var finite = slice.Where(static v => double.IsFinite(v)).ToList();
        if (finite.Count == 0)
            return ChartDatasetValues.Missing;

        return aggregation switch
        {
            GroupedBarBucketAggregation.Sum => finite.Sum(),
            GroupedBarBucketAggregation.Average => CatalogReportShared.RoundDurationChartValue(
                CatalogReportShared.AverageDurationMinutes(finite)),
            _ => throw new ArgumentOutOfRangeException(nameof(aggregation))
        };
    }

    private static List<ReportPreviewChartDataset> NormalizeDatasets(
        IReadOnlyList<ReportPreviewChartDataset> datasets,
        int dayCount)
    {
        return datasets
            .Select(ds => new ReportPreviewChartDataset
            {
                Label = ds.Label,
                Values = PadOrTrim(ds.Values, dayCount),
                NormValues = ds.NormValues is null ? null : PadOrTrim(ds.NormValues, dayCount),
                ChartSeriesType = ds.ChartSeriesType
            })
            .ToList();
    }

    private static List<double> PadOrTrim(IReadOnlyList<double> values, int dayCount)
    {
        var list = values.ToList();
        while (list.Count < dayCount)
            list.Add(ChartDatasetValues.Missing);
        if (list.Count > dayCount)
            list = list.Take(dayCount).ToList();
        return list;
    }

    private static List<ReportPreviewChartDataset> CloneDatasets(IReadOnlyList<ReportPreviewChartDataset> datasets) =>
        datasets
            .Select(ds => new ReportPreviewChartDataset
            {
                Label = ds.Label,
                Values = ds.Values.ToList(),
                NormValues = ds.NormValues?.ToList(),
                ChartSeriesType = ds.ChartSeriesType
            })
            .ToList();
}
