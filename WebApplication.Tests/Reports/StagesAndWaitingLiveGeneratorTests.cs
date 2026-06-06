using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Reports.Catalog;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class StagesAndWaitingLiveGeneratorTests
{
    [Fact]
    public void LoadStages_and_BuildReport_compute_pause_and_route_duration()
    {
        var day = new DateOnly(2026, 5, 12);
        var appointments = new List<EqAppointment>
        {
            new()
            {
                IdAppointment = 1,
                IdCategory = 1,
                DateArrival = day,
                TimeArrival = new TimeOnly(8, 0),
                Info = "РўРµСЃС‚"
            }
        };
        var listItems = new List<EqListItem>
        {
            new()
            {
                IdListItem = 10,
                IdAppointment = 1,
                IdSpecialty = 1,
                IdStatusItem = 1,
                IdCabinet = 1,
                IdDoctor = 1,
                TimeCall = new TimeOnly(8, 55),
                TimeStartServicing = new TimeOnly(9, 0),
                TimeEndServicing = new TimeOnly(9, 30)
            },
            new()
            {
                IdListItem = 11,
                IdAppointment = 1,
                IdSpecialty = 1,
                IdStatusItem = 1,
                IdCabinet = 1,
                IdDoctor = 1,
                TimeStartServicing = new TimeOnly(10, 0),
                TimeEndServicing = new TimeOnly(10, 20)
            }
        };

        var stages = StagesAndWaitingQueries.LoadStages(listItems, appointments, day, day);
        var periodFrom = new DateTime(2026, 5, 12, 0, 0, 0);
        var periodTo = new DateTime(2026, 5, 12, 23, 59, 59);
        var model = StagesAndWaitingReportBuilder.BuildReport(
            stages, periodFrom, periodTo, ReportGenerationPurpose.ExportOrFull);

        Assert.Equal(1, model.Rows.Count);
        Assert.Equal("08:00–10:20", model.Rows[0].Cells[1]);
        Assert.Equal("50 мин", model.Rows[0].Cells[3]);
        Assert.Equal("5 мин", model.Rows[0].Cells[4]);
        Assert.NotNull(model.PreviewCharts);
    }
}
