namespace WebApplication.Services.Dashboard;

public interface IQueueDashboardClock
{
    DateOnly TodayDateOnly();

    DateTime Now();
}
