using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Dashboard;
using Xunit;

namespace WebApplication.Tests.Dashboard;

public sealed class QueueDashboardStatusMapperTests
{
    [Fact]
    public void IsWaitingQueueStep_true_when_no_call_and_waiting_status()
    {
        var li = new EqListItem
        {
            TimeCall = null,
            StatusItem = new EqStatusItemList { Name = "Ожидает" }
        };
        Assert.True(QueueDashboardStatusMapper.IsWaitingQueueStep(li));
    }

    [Fact]
    public void IsWaitingQueueStep_false_when_called()
    {
        var li = new EqListItem
        {
            TimeCall = new TimeOnly(10, 0),
            StatusItem = new EqStatusItemList { Name = "Вызван" }
        };
        Assert.False(QueueDashboardStatusMapper.IsWaitingQueueStep(li));
    }

    [Fact]
    public void IsInServiceStep_true_when_servicing_started_not_ended()
    {
        var li = new EqListItem
        {
            TimeStartServicing = new TimeOnly(10, 0),
            TimeEndServicing = null,
            StatusItem = new EqStatusItemList { Name = "Обслуживается" }
        };
        Assert.True(QueueDashboardStatusMapper.IsInServiceStep(li));
    }

    [Fact]
    public void IsInServiceStep_false_when_only_called()
    {
        var li = new EqListItem
        {
            TimeCall = new TimeOnly(10, 0),
            TimeStartServicing = null,
            StatusItem = new EqStatusItemList { Name = "Вызван" }
        };
        Assert.False(QueueDashboardStatusMapper.IsInServiceStep(li));
    }

    [Fact]
    public void IsInServiceStep_false_when_stage_ended()
    {
        var li = new EqListItem
        {
            TimeStartServicing = new TimeOnly(10, 0),
            TimeEndServicing = new TimeOnly(10, 30),
            StatusItem = new EqStatusItemList { Name = "Обслужен" }
        };
        Assert.False(QueueDashboardStatusMapper.IsInServiceStep(li));
    }
}
