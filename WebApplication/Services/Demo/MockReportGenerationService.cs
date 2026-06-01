using System.Globalization;
using System.Text;
using WebApplication.Services.Reports.Catalog;
using WebApplication.Services.Reports.LoadAndDowntime;

namespace WebApplication.Services.Demo;

public sealed class MockReportGenerationService : IReportGenerationService
{
    private readonly IReportsCatalog _catalog;
    private readonly ReportCatalogMetadataEnricher _metadataEnricher;
    private readonly IReadOnlyDictionary<ReportGeneratorKind, Func<ReportGenerateRequest, ReportGenerationPurpose, ReportResultViewModel>> _offlineByKind;

    public MockReportGenerationService(
        IReportsCatalog catalog,
        ReportCatalogMetadataEnricher metadataEnricher)
    {
        _catalog = catalog;
        _metadataEnricher = metadataEnricher;
        _offlineByKind = new Dictionary<ReportGeneratorKind, Func<ReportGenerateRequest, ReportGenerationPurpose, ReportResultViewModel>>
        {
            [ReportGeneratorKind.LoadAndDowntime] = GenerateLoadAndDowntimeOffline,
            [ReportGeneratorKind.WaitingBeforeAppointment] = GenerateWaitingBeforeAppointmentOffline,
            [ReportGeneratorKind.AppointmentDuration] = GenerateAppointmentDurationOffline,
            [ReportGeneratorKind.ServiceDelays] = GenerateServiceDelaysOffline,
            [ReportGeneratorKind.RouteAndPauses] = GenerateRouteAndPausesOffline,
            [ReportGeneratorKind.ServiceRouteOutcomes] = GenerateServiceRouteOutcomesOffline,
            [ReportGeneratorKind.ServiceCategoriesComparison] = GenerateServiceCategoriesComparisonOffline
        };
    }

    public bool IsImplementedOffline(string? reportId) =>
        _catalog.TryGetItem(reportId, out var item)
        && item is not null
        && _offlineByKind.ContainsKey(item.GeneratorKind);

    private static readonly string[] AppointmentDurationMockSpecialties =
    [
        "Терапия",
        "Кардиология",
        "Неврология",
        "Офтальмология",
        "ЛОР"
    ];

    public IReadOnlyList<ReportSelectOption> GetCabinetOptions() =>
        ElectronicQueueMockData.Cabinets
            .Select(c => new ReportSelectOption { Id = c.Id, Label = c.Label })
            .ToList();

    public IReadOnlyList<ReportSelectOption> GetDoctorOptions() =>
        ElectronicQueueMockData.Doctors
            .Select(d => new ReportSelectOption { Id = d.Id, Label = d.Name })
            .ToList();

    public IReadOnlyList<ReportSelectOption> GetCategoryOptions() =>
        ElectronicQueueMockData.Categories
            .Select(c => new ReportSelectOption { Id = c.Id, Label = c.Name })
            .ToList();

    public ReportGenerateResponse Generate(ReportGenerateRequest request, ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        var reportId = request.ReportId?.Trim() ?? "";
        if (!_catalog.TryGetItem(reportId, out var item)
            || item is null
            || !_offlineByKind.TryGetValue(item.GeneratorKind, out var factory))
        {
            return new ReportGenerateResponse
            {
                Success = true,
                Implemented = false,
                Message = "Формирование выбранного отчета пока не реализовано."
            };
        }

        var result = factory(request, purpose);
        _metadataEnricher.ApplyToResult(result, reportId);
        return new ReportGenerateResponse { Success = true, Implemented = true, Result = result };
    }

    public (byte[] Bytes, string ContentType, string FileName) BuildExport(ReportExportRequest request)
    {
        var generated = Generate(request, ReportGenerationPurpose.ExportOrFull);
        if (!generated.Implemented || generated.Result is null)
        {
            var stub = new ReportResultViewModel
            {
                GeneratedForReportId = "report",
                DownloadFileName = "report-not-implemented.csv",
                ColumnHeaders = ["report", "status"],
                Rows = [new ReportResultRowViewModel { Cells = ["not_implemented", "true"] }]
            };
            return ReportTabularExporter.Export(stub, "csv", request, ResolveGeneratorKind(request.ReportId));
        }

        return ReportTabularExporter.Export(
            generated.Result,
            request.Format,
            request,
            ResolveGeneratorKind(request.ReportId));
    }

    private ReportGeneratorKind? ResolveGeneratorKind(string? reportId)
    {
        var rid = reportId?.Trim() ?? "";
        return _catalog.TryGetItem(rid, out var item) && item is not null ? item.GeneratorKind : null;
    }

    public ReportResultViewModel GenerateLoadAndDowntimeOffline(
        ReportGenerateRequest request,
        ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        if (!DateTime.TryParse(
                request.DateFrom,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var periodFrom)
            || !DateTime.TryParse(
                request.DateTo,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var periodTo))
        {
            periodFrom = DateTime.UtcNow.Date.AddDays(-7);
            periodTo = DateTime.UtcNow;
        }

        if (periodFrom > periodTo)
            (periodFrom, periodTo) = (periodTo, periodFrom);

        var byCabinet = request.CustomParams is not null
                        && request.CustomParams.TryGetValue("analysisMode", out var am)
                        && string.Equals(am?.Trim(), "cabinet", StringComparison.OrdinalIgnoreCase);

        var fromDo = DateOnly.FromDateTime(periodFrom);
        var toDoOnly = DateOnly.FromDateTime(periodTo);
        var (rawLogs, listRows) = MockReportOfflineSeed.BuildLoadAndDowntimeData(fromDo, toDoOnly);

        return LoadAndDowntimeReportBuilder.BuildReport(
            rawLogs,
            listRows,
            MockReportOfflineSeed.MockDoctorNames(),
            MockReportOfflineSeed.MockCabinetNumbers(),
            periodFrom,
            periodTo,
            byCabinet,
            purpose);
    }

    public byte[] BuildDemoCsv(string reportId, string? analysisMode = null)
    {
        var rid = reportId.Trim();
        if (!_catalog.TryGetItem(rid, out var item) || item is null)
            return Encoding.UTF8.GetBytes("reportId;status\nunknown;not_found\n");

        var p = new ReportGenerateRequest
        {
            ReportId = rid,
            DateFrom = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTo = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            CustomParams = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

        if (item.GeneratorKind is ReportGeneratorKind.LoadAndDowntime or ReportGeneratorKind.ServiceDelays)
        {
            p.CustomParams["analysisMode"] = string.Equals(analysisMode?.Trim(), "cabinet", StringComparison.OrdinalIgnoreCase)
                ? "cabinet"
                : "doctor";
        }
        else if (item.GeneratorKind == ReportGeneratorKind.AppointmentDuration)
        {
            p.CustomParams["analysisMode"] = AppointmentDurationReportBuilder.ParseAnalysisMode(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analysisMode"] = string.IsNullOrWhiteSpace(analysisMode) ? "doctor" : analysisMode.Trim()
                });
        }

        var generated = Generate(p, ReportGenerationPurpose.ExportOrFull);
        if (!generated.Implemented || generated.Result is null)
            return Encoding.UTF8.GetBytes("reportId;status\nunknown;not_found\n");

        return ReportTabularExporter.WriteCsvBytes(generated.Result);
    }

    public static ReportResultViewModel GenerateAppointmentDurationOffline(
        ReportGenerateRequest request,
        ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);
        var analysisMode = AppointmentDurationReportBuilder.ParseAnalysisMode(request.CustomParams);
        var periodSeed = Math.Abs(fromDo.DayNumber * 41 + toDo.DayNumber * 19);
        var observations = new List<AppointmentDurationReportBuilder.DurationObservation>();

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            var daySeed = Math.Abs(periodSeed + day.DayNumber * 23);

            if (analysisMode == AppointmentDurationReportBuilder.ModeDoctor)
            {
                foreach (var doc in ElectronicQueueMockData.Doctors)
                {
                    var count = 2 + (daySeed + doc.Id * 7) % 6;
                    for (var n = 0; n < count; n++)
                    {
                        var svc = 12.0 + (daySeed + doc.Id * 11 + n * 5) % 28;
                        var si = (doc.Id + n) % AppointmentDurationMockSpecialties.Length;
                        var norm = 15 + (daySeed + doc.Id + n) % 31;
                        var idAppointment = day.DayNumber * 10000 + doc.Id * 100 + n;
                        observations.Add(new AppointmentDurationReportBuilder.DurationObservation(
                            day,
                            doc.Name,
                            idAppointment,
                            svc,
                            norm,
                            AppointmentDurationMockSpecialties[si]));
                    }
                }
            }
            else if (analysisMode == AppointmentDurationReportBuilder.ModeCabinet)
            {
                foreach (var cab in ElectronicQueueMockData.Cabinets)
                {
                    var count = 1 + (daySeed + cab.Id * 9) % 5;
                    for (var n = 0; n < count; n++)
                    {
                        var svc = 10.0 + (daySeed + cab.Id * 13 + n * 4) % 32;
                        var label = AppointmentDurationReportBuilder.FormatCabinetLabel(cab.Label);
                        var norm = 15 + (daySeed + cab.Id + n * 3) % 31;
                        var idAppointment = day.DayNumber * 10000 + cab.Id * 100 + n;
                        observations.Add(new AppointmentDurationReportBuilder.DurationObservation(
                            day, label, idAppointment, svc, norm, null));
                    }
                }
            }
            else
            {
                for (var si = 0; si < AppointmentDurationMockSpecialties.Length; si++)
                {
                    var count = 1 + (daySeed + si * 11) % 4;
                    for (var n = 0; n < count; n++)
                    {
                        var svc = 14.0 + (daySeed + si * 17 + n * 6) % 26;
                        var norm = 15 + (daySeed + si + n * 2) % 31;
                        var idAppointment = day.DayNumber * 10000 + si * 100 + n;
                        observations.Add(new AppointmentDurationReportBuilder.DurationObservation(
                            day,
                            AppointmentDurationMockSpecialties[si],
                            idAppointment,
                            svc,
                            norm,
                            AppointmentDurationMockSpecialties[si]));
                    }
                }
            }
        }

        return AppointmentDurationReportBuilder.BuildReport(observations, fromDo, toDo, analysisMode, purpose);
    }

    public static ReportResultViewModel GenerateWaitingBeforeAppointmentOffline(
        ReportGenerateRequest request,
        ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        var (periodFrom, periodTo, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);
        var periodSeed = Math.Abs(fromDo.DayNumber * 37 + toDo.DayNumber * 13);
        var rows = new List<CatalogReportWaitingHelper.WaitStageRow>();
        var observations = new List<WaitingBeforeAppointmentReportBuilder.WaitingObservation>();

        if (fromDo <= toDo)
        {
            var demoDay = fromDo;
            const int multiStageApptId = 9001;
            rows.Add(new CatalogReportWaitingHelper.WaitStageRow(
                900101,
                multiStageApptId,
                demoDay,
                new TimeOnly(8, 0),
                new TimeOnly(8, 15),
                new TimeOnly(8, 15),
                new TimeOnly(10, 0)));
            rows.Add(new CatalogReportWaitingHelper.WaitStageRow(
                900102,
                multiStageApptId,
                demoDay,
                new TimeOnly(8, 0),
                new TimeOnly(10, 15),
                new TimeOnly(10, 15),
                new TimeOnly(10, 30)));

            const int fallbackApptId = 9002;
            rows.Add(new CatalogReportWaitingHelper.WaitStageRow(
                900201,
                fallbackApptId,
                demoDay,
                new TimeOnly(9, 0),
                new TimeOnly(9, 10),
                new TimeOnly(9, 10),
                null));
            rows.Add(new CatalogReportWaitingHelper.WaitStageRow(
                900202,
                fallbackApptId,
                demoDay,
                new TimeOnly(9, 0),
                new TimeOnly(9, 40),
                new TimeOnly(9, 40),
                new TimeOnly(10, 0)));

            observations.AddRange(CatalogReportWaitingHelper.BuildWaitingObservations(rows, periodFrom, periodTo));
        }

        for (var day = fromDo; day <= toDo; day = day.AddDays(1))
        {
            foreach (var slot in WaitingBeforeAppointmentReportBuilder.GetHourSlotsForDay(day, periodFrom, periodTo))
            {
                var callTime = TimeOnly.FromTimeSpan(TimeSpan.FromHours(slot.Hour) + TimeSpan.FromMinutes(30));
                if (!WaitingBeforeAppointmentReportBuilder.IsCallInPeriod(day, callTime, periodFrom, periodTo))
                    continue;

                var seed = Math.Abs(periodSeed + day.DayNumber * 31 + slot.Hour * 17);
                var count = seed % 5;
                for (var n = 0; n < count; n++)
                {
                    var wait = 5.0 + (seed + n * 3) % 35 + slot.Hour * 0.2;
                    observations.Add(new WaitingBeforeAppointmentReportBuilder.WaitingObservation(day, slot.Hour, wait));
                }
            }
        }

        return WaitingBeforeAppointmentReportBuilder.BuildReport(
            observations, fromDo, toDo, periodFrom, periodTo, purpose);
    }

    public static ReportResultViewModel GenerateServiceRouteOutcomesOffline(
        ReportGenerateRequest request,
        ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);
        var (appointments, listItems, categories) = MockReportOfflineSeed.BuildArrivedAndCompletedData(fromDo, toDo);
        return ServiceRouteOutcomesReportBuilder.BuildReport(appointments, listItems, categories, purpose);
    }

    public static ReportResultViewModel GenerateServiceDelaysOffline(
        ReportGenerateRequest request,
        ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);
        var analysisMode = ServiceDelaysReportBuilder.ParseAnalysisMode(request.CustomParams);
        var stages = MockReportOfflineSeed.BuildServiceDelaysStages(fromDo, toDo);
        var entityLabels = MockReportOfflineSeed.BuildServiceDelaysResourceLabels(analysisMode);
        var metrics = ServiceDelaysQueries.BuildEntityMetrics(stages, entityLabels, analysisMode);
        return ServiceDelaysReportBuilder.BuildReport(metrics, analysisMode, purpose);
    }

    public static ReportResultViewModel GenerateRouteAndPausesOffline(
        ReportGenerateRequest request,
        ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        var (periodFrom, periodTo, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);
        var stages = MockReportOfflineSeed.BuildRouteStageObservations(fromDo, toDo);
        return RouteAndPausesReportBuilder.BuildReport(stages, periodFrom, periodTo, purpose);
    }

    public static ReportResultViewModel GenerateServiceCategoriesComparisonOffline(
        ReportGenerateRequest request,
        ReportGenerationPurpose purpose = ReportGenerationPurpose.ExportOrFull)
    {
        var (_, _, fromDo, toDo) = CatalogReportShared.ParsePeriod(request);
        var observations = MockReportOfflineSeed.BuildServiceCategoryObservations(fromDo, toDo);
        return ServiceCategoriesComparisonReportBuilder.BuildReport(observations, purpose);
    }
}
