using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Reports.Catalog;
using WebApplication.Services.Reports.LoadAndDowntime;

namespace WebApplication.Services.Reports;

/// <summary>Синтетические наблюдения для mock-отчётов (те же типы, что читает live из ElectronicQueueProf).</summary>
internal static class MockReportOfflineSeed
{
    private static readonly string[] SpecialtyLabels =
    [
        "Терапия",
        "Кардиология",
        "Неврология",
        "Офтальмология",
        "ЛОР"
    ];

    internal static (List<LoadAndDowntimeReportBuilder.LogWorkLite> Logs, List<LoadAndDowntimeReportBuilder.ListRowLite> Items)
        BuildLoadAndDowntimeData(DateOnly fromDo, DateOnly toDo)
    {
        var rawLogs = new List<LoadAndDowntimeReportBuilder.LogWorkLite>();
        var listRows = new List<LoadAndDowntimeReportBuilder.ListRowLite>();
        var nextApptId = 1000;

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            foreach (var doc in ElectronicQueueMockData.Doctors)
            {
                var cabIndex = 0;
                foreach (var cab in ElectronicQueueMockData.Cabinets)
                {
                    cabIndex++;
                    var seed = Math.Abs(day.DayNumber * 23 + doc.Id * 11 + cab.Id * 7);
                    if (seed % 4 == 0)
                        continue;

                    var begin = new TimeOnly(8 + seed % 2, 15);
                    var end = new TimeOnly(13 + seed % 5, seed % 2 == 0 ? 30 : 0);
                    rawLogs.Add(new LoadAndDowntimeReportBuilder.LogWorkLite(doc.Id, cab.Id, day, begin, end));

                    var apptId = nextApptId++;
                    var stageCount = 1 + seed % 3;
                    for (var s = 0; s < stageCount; s++)
                    {
                        var startMin = 30 + s * 50 + seed % 20;
                        var start = begin.Add(TimeSpan.FromMinutes(startMin));
                        var endSvc = start.Add(TimeSpan.FromMinutes(15 + (seed + s) % 25));
                        if (endSvc > end)
                            endSvc = end.Add(TimeSpan.FromMinutes(-5));

                        listRows.Add(new LoadAndDowntimeReportBuilder.ListRowLite(
                            apptId,
                            doc.Id,
                            cab.Id,
                            day,
                            1,
                            "Обслуживание",
                            start.Add(TimeSpan.FromMinutes(-3)),
                            start,
                            endSvc,
                            SpecialtyLabels[(seed + s) % SpecialtyLabels.Length]));
                    }

                    if (cabIndex >= 2)
                        break;
                }
            }
        }

        return (rawLogs, listRows);
    }

    internal static Dictionary<int, string> MockDoctorNames() =>
        ElectronicQueueMockData.Doctors.ToDictionary(d => d.Id, d => d.Name);

    internal static Dictionary<int, string> MockCabinetNumbers() =>
        ElectronicQueueMockData.Cabinets.ToDictionary(
            c => c.Id,
            c => c.Label.Replace("Каб. ", "", StringComparison.Ordinal).Trim());

    internal static (
        List<ArrivedAndCompletedReportBuilder.ArrivedAppointmentObservation> Appointments,
        List<ArrivedAndCompletedReportBuilder.ArrivedListItemObservation> ListItems,
        Dictionary<int, (string Name, int Priority)> Categories)
        BuildArrivedAndCompletedData(DateOnly fromDo, DateOnly toDo)
    {
        var categories = ElectronicQueueMockData.Categories
            .ToDictionary(c => c.Id, c => (c.Name, c.Id));
        var appointments = new List<ArrivedAndCompletedReportBuilder.ArrivedAppointmentObservation>();
        var listItems = new List<ArrivedAndCompletedReportBuilder.ArrivedListItemObservation>();
        var nextApptId = 1;

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            foreach (var cat in ElectronicQueueMockData.Categories)
            {
                var seed = Math.Abs(day.DayNumber * 31 + cat.Id * 17);
                var total = 6 + seed % 10;
                var noShowCount = Math.Min(total, seed % 4);

                for (var i = 0; i < total; i++)
                {
                    var apptId = nextApptId++;
                    appointments.Add(new ArrivedAndCompletedReportBuilder.ArrivedAppointmentObservation(
                        apptId, day, cat.Id));

                    if (i < noShowCount)
                        continue;

                    var itemSeed = seed + i * 13;
                    if (itemSeed % 5 == 0)
                    {
                        listItems.Add(new ArrivedAndCompletedReportBuilder.ArrivedListItemObservation(
                            apptId,
                            new TimeOnly(9, 30),
                            new TimeOnly(9, 45),
                            null));
                    }
                    else if (itemSeed % 7 == 0)
                    {
                        listItems.Add(new ArrivedAndCompletedReportBuilder.ArrivedListItemObservation(
                            apptId,
                            new TimeOnly(10, 0),
                            null,
                            null));
                    }
                    else
                    {
                        listItems.Add(new ArrivedAndCompletedReportBuilder.ArrivedListItemObservation(
                            apptId,
                            new TimeOnly(10, 15),
                            new TimeOnly(10, 20),
                            new TimeOnly(10, 50)));
                    }
                }
            }
        }

        return (appointments, listItems, categories);
    }

    internal static List<BottleneckRankingQueries.StageObservation> BuildBottleneckStages(
        DateOnly fromDo,
        DateOnly toDo)
    {
        var stages = new List<BottleneckRankingQueries.StageObservation>();
        var nextApptId = 50_000;
        var nextListItemId = 60_000;
        const int normMin = 15;

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            foreach (var doc in ElectronicQueueMockData.Doctors)
            {
                var seed = Math.Abs(day.DayNumber * 23 + doc.Id * 11);
                var stageCount = 2 + seed % 3;

                for (var n = 0; n < stageCount; n++)
                {
                    var cab = ElectronicQueueMockData.Cabinets[n % ElectronicQueueMockData.Cabinets.Count];
                    var callTime = new TimeOnly(8 + n, 0);
                    var startTime = callTime.Add(TimeSpan.FromMinutes(5 + n * 8));
                    var endTime = startTime.Add(TimeSpan.FromMinutes(normMin + (n == 0 ? 20 : 0)));
                    stages.Add(new BottleneckRankingQueries.StageObservation(
                        nextListItemId++,
                        nextApptId++,
                        day,
                        doc.Id,
                        cab.Id,
                        callTime,
                        startTime,
                        endTime,
                        normMin,
                        SpecialtyLabels[doc.Id % SpecialtyLabels.Length]));
                }
            }

            foreach (var cab in ElectronicQueueMockData.Cabinets)
            {
                var seed = Math.Abs(day.DayNumber * 17 + cab.Id * 13);
                if (seed % 3 != 0)
                    continue;

                var doc = ElectronicQueueMockData.Doctors[seed % ElectronicQueueMockData.Doctors.Count];
                var callTime = new TimeOnly(10, 0);
                var startTime = callTime.Add(TimeSpan.FromMinutes(12));
                var endTime = startTime.Add(TimeSpan.FromMinutes(normMin + 10));
                stages.Add(new BottleneckRankingQueries.StageObservation(
                    nextListItemId++,
                    nextApptId++,
                    day,
                    doc.Id,
                    cab.Id,
                    callTime,
                    startTime,
                    endTime,
                    normMin,
                    SpecialtyLabels[seed % SpecialtyLabels.Length]));
            }
        }

        return stages;
    }

    internal static Dictionary<int, string> BuildBottleneckResourceLabels(string analysisMode)
    {
        if (string.Equals(analysisMode, BottleneckRankingReportBuilder.ModeCabinet, StringComparison.OrdinalIgnoreCase))
        {
            return ElectronicQueueMockData.Cabinets.ToDictionary(
                c => c.Id,
                c => BottleneckRankingReportBuilder.FormatCabinetLabel(
                    c.Label.Replace("Каб. ", "", StringComparison.Ordinal).Trim()));
        }

        return ElectronicQueueMockData.Doctors.ToDictionary(d => d.Id, d => d.Name);
    }

    internal static List<RouteAndPausesReportBuilder.RouteStageObservation> BuildRouteStageObservations(
        DateOnly fromDo,
        DateOnly toDo)
    {
        var (appointments, listItems) = BuildRoutePausesEntities(fromDo, toDo);
        return RouteAndPausesQueries.LoadStages(listItems, appointments, fromDo, toDo);
    }

    internal static (List<EqAppointment> Appointments, List<EqListItem> ListItems) BuildRoutePausesEntities(
        DateOnly fromDo,
        DateOnly toDo)
    {
        var appointments = new List<EqAppointment>();
        var listItems = new List<EqListItem>();
        var nextApptId = 3000;
        var nextListItemId = 50000;

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            var seed = Math.Abs(day.DayNumber * 41);
            var multiCount = 2 + seed % 4;
            for (var t = 0; t < multiCount; t++)
            {
                var apptId = nextApptId++;
                var info = $"Пациент {apptId % 100}";
                var appt = CreateRoutePausesAppointment(apptId, day, info);
                appointments.Add(appt);

                var stageCount = 2 + (seed + t) % 2;
                var routeBaseStart = new TimeOnly(9, 15);
                for (var s = 0; s < stageCount; s++)
                {
                    var start = routeBaseStart.AddMinutes(s * 70);
                    var end = start.AddMinutes(30);
                    listItems.Add(CreateRoutePausesListItem(ref nextListItemId, appt, start, end, start.AddMinutes(-10)));
                }
            }

            AppendRoutePausesEdgeCaseFixtures(appointments, listItems, day, ref nextApptId, ref nextListItemId);
        }

        return (appointments, listItems);
    }

    private static EqAppointment CreateRoutePausesAppointment(int idAppointment, DateOnly day, string info) =>
        new()
        {
            IdAppointment = idAppointment,
            IdCategory = 1,
            DateArrival = day,
            TimeArrival = new TimeOnly(8, 0),
            Info = info
        };

    private static EqListItem CreateRoutePausesListItem(
        ref int nextListItemId,
        EqAppointment appointment,
        TimeOnly? timeStart,
        TimeOnly? timeEnd,
        TimeOnly? timeCall = null) =>
        new()
        {
            IdListItem = nextListItemId++,
            IdAppointment = appointment.IdAppointment,
            IdSpecialty = 1,
            IdStatusItem = 1,
            IdCabinet = 1,
            IdDoctor = 1,
            TimeCall = timeCall,
            TimeStartServicing = timeStart,
            TimeEndServicing = timeEnd
        };

    private static void AppendRoutePausesEdgeCaseFixtures(
        List<EqAppointment> appointments,
        List<EqListItem> listItems,
        DateOnly day,
        ref int nextApptId,
        ref int nextListItemId)
    {
        var single = CreateRoutePausesAppointment(nextApptId++, day, "Одноэтапный");
        appointments.Add(single);
        listItems.Add(CreateRoutePausesListItem(
            ref nextListItemId, single, new TimeOnly(10, 0), new TimeOnly(10, 30)));

        var three = CreateRoutePausesAppointment(nextApptId++, day, "Три этапа");
        appointments.Add(three);
        listItems.Add(CreateRoutePausesListItem(
            ref nextListItemId, three, new TimeOnly(8, 0), new TimeOnly(8, 25), new TimeOnly(7, 55)));
        listItems.Add(CreateRoutePausesListItem(ref nextListItemId, three, new TimeOnly(9, 0), null));
        listItems.Add(CreateRoutePausesListItem(ref nextListItemId, three, new TimeOnly(10, 30), new TimeOnly(11, 0)));

        var late = CreateRoutePausesAppointment(nextApptId++, day, "Поздний старт");
        appointments.Add(late);
        listItems.Add(CreateRoutePausesListItem(ref nextListItemId, late, new TimeOnly(8, 0), new TimeOnly(8, 20)));
        listItems.Add(CreateRoutePausesListItem(ref nextListItemId, late, null, new TimeOnly(12, 0)));

        var clipDemo = CreateRoutePausesAppointment(nextApptId++, day, "Обрезка по периоду");
        appointments.Add(clipDemo);
        listItems.Add(CreateRoutePausesListItem(ref nextListItemId, clipDemo, new TimeOnly(7, 0), new TimeOnly(7, 30)));
        listItems.Add(CreateRoutePausesListItem(ref nextListItemId, clipDemo, new TimeOnly(9, 0), new TimeOnly(9, 30)));
    }

    internal static List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>
        BuildServiceCategoryObservations(DateOnly fromDo, DateOnly toDo)
    {
        var observations = new List<ServiceCategoriesComparisonReportBuilder.CategoryStageObservation>();
        var nextApptId = 5000;

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            foreach (var cat in ElectronicQueueMockData.Categories)
            {
                var seed = Math.Abs(day.DayNumber * 43 + cat.Id * 11);
                for (var i = 0; i < 3 + seed % 5; i++)
                {
                    var apptId = nextApptId++;
                    double? wait = 8.0 + (seed + i) % 25;
                    double? svc = 12.0 + (seed + i * 3) % 30;
                    if (i % 4 == 0)
                        wait = null;
                    if (i % 5 == 0)
                        svc = null;

                    observations.Add(new ServiceCategoriesComparisonReportBuilder.CategoryStageObservation(
                        apptId, cat.Id, cat.Name, wait, svc));

                    if (cat.Id == 2 && i % 2 == 0)
                    {
                        observations.Add(new ServiceCategoriesComparisonReportBuilder.CategoryStageObservation(
                            apptId, cat.Id, cat.Name,
                            wait.HasValue ? wait + 1.5 : null,
                            svc.HasValue ? svc + 2.0 : null));
                    }
                }
            }
        }

        return observations;
    }
}
