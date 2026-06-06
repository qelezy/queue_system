using WebApplication.Models.Reports.Charts;
using WebApplication.Services.Reports.Charts;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class GroupedBarChartTimeAxisTests
{
    [Fact]
    public void Prepare_28_days_aggregates_to_four_or_five_weeks()
    {
        var days = Enumerable.Range(0, 28)
            .Select(i => new DateOnly(2026, 5, 1).AddDays(i))
            .ToList();
        var datasets = new List<ReportPreviewChartDataset>
        {
            new() { Label = "Series", Values = days.Select(_ => 1.0).ToList() }
        };

        var axis = GroupedBarChartTimeAxis.Prepare(days, datasets, GroupedBarBucketAggregation.Sum);

        Assert.True(axis.IsWeekly);
        Assert.InRange(axis.Labels.Count, 4, 5);
        Assert.Equal(axis.Labels.Count, axis.Datasets[0].Values.Count);
    }

    [Fact]
    public void Prepare_21_days_or_less_keeps_daily_labels()
    {
        var days = Enumerable.Range(0, 21)
            .Select(i => new DateOnly(2026, 5, 1).AddDays(i))
            .ToList();
        var datasets = new List<ReportPreviewChartDataset>
        {
            new() { Label = "Series", Values = days.Select((_, i) => (double)i).ToList() }
        };

        var axis = GroupedBarChartTimeAxis.Prepare(days, datasets, GroupedBarBucketAggregation.Average);

        Assert.False(axis.IsWeekly);
        Assert.Equal(21, axis.Labels.Count);
        Assert.All(axis.Labels, label => Assert.Contains("-2026", label));
    }

    [Fact]
    public void Prepare_sum_aggregates_values_within_week()
    {
        var days = Enumerable.Range(0, 28)
            .Select(i => new DateOnly(2026, 5, 1).AddDays(i))
            .ToList();
        var datasets = new List<ReportPreviewChartDataset>
        {
            new() { Label = "Count", Values = days.Select((_, i) => i < 7 ? 2.0 : 1.0).ToList() }
        };

        var axis = GroupedBarChartTimeAxis.Prepare(days, datasets, GroupedBarBucketAggregation.Sum);

        Assert.Equal(6, axis.Datasets[0].Values[0]);
    }

    [Fact]
    public void Prepare_average_includes_zero_days_in_week()
    {
        var days = Enumerable.Range(0, 28)
            .Select(i => new DateOnly(2026, 5, 1).AddDays(i))
            .ToList();
        var datasets = new List<ReportPreviewChartDataset>
        {
            new()
            {
                Label = "Wait",
                Values = days.Select((_, i) => i == 0 ? 10.0 : 0.0).ToList(),
                NormValues = days.Select((_, i) => i == 0 ? 20.0 : 0.0).ToList()
            }
        };

        var axis = GroupedBarChartTimeAxis.Prepare(days, datasets, GroupedBarBucketAggregation.Average);

        Assert.Equal(axis.Labels.Count, axis.Datasets[0].NormValues!.Count);
        Assert.Equal(10.0 / 3, axis.Datasets[0].Values[0], precision: 3);
        Assert.Equal(20.0 / 3, axis.Datasets[0].NormValues![0], precision: 3);
    }

    [Fact]
    public void Prepare_average_ignores_missing_values_in_week()
    {
        var days = Enumerable.Range(0, 22)
            .Select(i => new DateOnly(2026, 5, 1).AddDays(i))
            .ToList();
        var datasets = new List<ReportPreviewChartDataset>
        {
            new()
            {
                Label = "Wait",
                Values = days.Select((_, i) => i switch
                {
                    3 => 10.0,
                    5 => 20.0,
                    < 22 => ChartDatasetValues.Missing,
                    _ => ChartDatasetValues.Missing
                }).ToList()
            }
        };

        var axis = GroupedBarChartTimeAxis.Prepare(days, datasets, GroupedBarBucketAggregation.Average);

        Assert.True(axis.IsWeekly);
        Assert.Equal(15.0, axis.Datasets[0].Values[1]);
    }
}
