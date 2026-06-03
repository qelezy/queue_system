using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Dashboard;
using Xunit;

namespace WebApplication.Tests.Dashboard;

public sealed class QueueDashboardCompletedStagesTests
{
    [Fact]
    public void IsCompletedStage_true_when_ended()
    {
        var li = new EqListItem
        {
            TimeEndServicing = new TimeOnly(11, 0),
            StatusItem = new EqStatusItemList { Name = "Обслужен" }
        };
        Assert.True(QueueDashboardCompletedStagesMapper.IsCompletedStage(li));
    }

    [Fact]
    public void IsCompletedStage_false_without_end_time()
    {
        var li = new EqListItem
        {
            TimeStartServicing = new TimeOnly(10, 0),
            StatusItem = new EqStatusItemList { Name = "Обслуживается" }
        };
        Assert.False(QueueDashboardCompletedStagesMapper.IsCompletedStage(li));
    }

    [Fact]
    public void IsRouteStage_true_for_open_called_step()
    {
        var li = new EqListItem
        {
            TimeCall = new TimeOnly(11, 0),
            StatusItem = new EqStatusItemList { Name = "Вызван" }
        };
        Assert.True(QueueDashboardCompletedStagesMapper.IsRouteStage(li));
        Assert.False(QueueDashboardCompletedStagesMapper.IsCompletedStage(li));
    }

    [Fact]
    public void ToDto_formats_specialty_cabinet_status_and_times()
    {
        var li = new EqListItem
        {
            TimeCall = new TimeOnly(9, 15, 30),
            TimeStartServicing = new TimeOnly(9, 20, 0),
            TimeEndServicing = new TimeOnly(9, 45, 12),
            Specialty = new EqSpecialty { Definition = "Терапевт" },
            Cabinet = new EqCabinet { CabinetNumber = "201" },
            StatusItem = new EqStatusItemList { Name = "Обслужен" }
        };

        var dto = QueueDashboardCompletedStagesMapper.ToDto(li);

        Assert.Equal("Терапевт", dto.Specialty);
        Assert.Equal("201", dto.Cabinet);
        Assert.Equal("Завершён", dto.StatusLabel);
        Assert.Equal("done", dto.StatusCode);
        Assert.Equal("09:15:30", dto.TimeCall);
        Assert.Equal("09:20:00", dto.TimeStart);
        Assert.Equal("09:45:12", dto.TimeEnd);
    }

    [Fact]
    public void Route_stages_include_open_and_completed_sorted_by_id_list_item()
    {
        var items = new[]
        {
            new EqListItem { IdListItem = 3, TimeEndServicing = new TimeOnly(12, 0), StatusItem = new EqStatusItemList { Name = "Обслужен" } },
            new EqListItem { IdListItem = 1, TimeEndServicing = new TimeOnly(10, 0), StatusItem = new EqStatusItemList { Name = "Обслужен" } },
            new EqListItem { IdListItem = 2, TimeCall = new TimeOnly(11, 0), StatusItem = new EqStatusItemList { Name = "Вызван" } },
        };

        var ordered = items
            .Where(QueueDashboardCompletedStagesMapper.IsRouteStage)
            .OrderBy(li => li.IdListItem)
            .Select(li => li.IdListItem)
            .ToList();

        Assert.Equal([1, 2, 3], ordered);
    }
}
