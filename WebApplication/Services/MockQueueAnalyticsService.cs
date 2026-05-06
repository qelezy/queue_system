using Microsoft.Extensions.Options;
using WebApplication.Models;

namespace WebApplication.Services;

public sealed class MockQueueAnalyticsService : IQueueAnalyticsService
{
    private readonly MonitoringOptions _opt;

    public MockQueueAnalyticsService(IOptions<MonitoringOptions> options) =>
        _opt = options.Value;

    public Task<ManagerAnalyticsViewModel> GetManagerAnalyticsAsync(
        DateOnly from,
        DateOnly to,
        int? cabinetId,
        int? doctorId,
        int? categoryId,
        bool heatmapByDoctor,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
            (from, to) = (to, from);

        var maxDays = Math.Max(1, _opt.ManagerMaxRangeDays);
        if ((to.DayNumber - from.DayNumber) > maxDays)
            to = from.AddDays(maxDays);

        var periodDayCount = (to.DayNumber - from.DayNumber) + 1;
        var hourLabels = ElectronicQueueMockData.BuildHourLabels(_opt).ToList();
        var hourCount = hourLabels.Count;

        var cabinetOpts = ElectronicQueueMockData.Cabinets
            .Select(c => new SelectOptionViewModel { Value = c.Id, Text = c.Label })
            .ToList();
        cabinetOpts.Insert(0, new SelectOptionViewModel { Value = null, Text = "Все кабинеты" });

        var doctorOpts = ElectronicQueueMockData.Doctors
            .Select(d => new SelectOptionViewModel { Value = d.Id, Text = d.Name })
            .ToList();
        doctorOpts.Insert(0, new SelectOptionViewModel { Value = null, Text = "Все врачи" });

        var catOpts = ElectronicQueueMockData.Categories
            .Select(c => new SelectOptionViewModel { Value = c.Id, Text = c.Name })
            .ToList();
        catOpts.Insert(0, new SelectOptionViewModel { Value = null, Text = "Все категории" });

        var queueByHourPerDay = new List<ManagerDaySeriesViewModel>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var counts = new int[hourCount];
            for (var hi = 0; hi < hourCount; hi++)
                counts[hi] = (Math.Abs(d.DayNumber * 31 + hi * 7) % 9) + 1;

            queueByHourPerDay.Add(new ManagerDaySeriesViewModel
            {
                DayLabel = d.ToString("dd.MM"),
                Values = counts
            });
        }

        var dailyAvg = new double[hourCount];
        for (var hi = 0; hi < hourCount; hi++)
        {
            var sum = queueByHourPerDay.Sum(s => s.Values[hi]);
            dailyAvg[hi] = periodDayCount > 0 ? sum / (double)periodDayCount : 0;
        }

        var waitHistogram = new List<HistogramBucketViewModel>
        {
            new() { Label = "0–5 мин", Count = 40 },
            new() { Label = "5–15 мин", Count = 28 },
            new() { Label = "15–30 мин", Count = 15 },
            new() { Label = "30–60 мин", Count = 8 },
            new() { Label = "60+ мин", Count = 3 }
        };

        var avgWaitByDoctor = ElectronicQueueMockData.Doctors.Select(d => new NamedValueViewModel
        {
            Name = d.Name,
            ValueMinutes = 12 + (d.Id * 7) % 25
        }).ToList();

        var avgServiceByDoctor = ElectronicQueueMockData.Doctors.Select(d => new NamedValueViewModel
        {
            Name = d.Name,
            ValueMinutes = 15 + (d.Id * 5) % 20
        }).ToList();

        var heatmapRowLabels = new List<string>();
        var heatmapValues = new List<IReadOnlyList<double>>();

        if (heatmapByDoctor)
        {
            foreach (var d in ElectronicQueueMockData.Doctors)
            {
                heatmapRowLabels.Add(d.Name);
                var row = new double[hourCount];
                for (var hi = 0; hi < hourCount; hi++)
                    row[hi] = (d.Id + hi) % 4 + 0.2 * hi;

                heatmapValues.Add(row);
            }
        }
        else
        {
            foreach (var c in ElectronicQueueMockData.Cabinets)
            {
                heatmapRowLabels.Add(c.Label);
                var row = new double[hourCount];
                for (var hi = 0; hi < hourCount; hi++)
                    row[hi] = (c.Id + hi * 2) % 5 + 0.15 * hi;

                heatmapValues.Add(row);
            }
        }

        var vm = new ManagerAnalyticsViewModel
        {
            PeriodFrom = from,
            PeriodTo = to,
            FilterCabinetId = cabinetId,
            FilterDoctorId = doctorId,
            FilterCategoryId = categoryId,
            CabinetOptions = cabinetOpts,
            DoctorOptions = doctorOpts,
            CategoryOptions = catOpts,
            QueueByHourLabels = hourLabels,
            QueueByHourPerDay = queueByHourPerDay,
            QueueByHourDailyAverage = dailyAvg,
            WaitHistogram = waitHistogram,
            AvgWaitByDoctor = avgWaitByDoctor,
            AvgServiceByDoctor = avgServiceByDoctor,
            HeatmapHourLabels = hourLabels,
            HeatmapRowLabels = heatmapRowLabels,
            HeatmapValues = heatmapValues,
            HeatmapIsByDoctor = heatmapByDoctor
        };

        return Task.FromResult(vm);
    }
}
