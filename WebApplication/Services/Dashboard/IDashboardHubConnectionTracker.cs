namespace WebApplication.Services.Dashboard;

public interface IDashboardHubConnectionTracker
{
    int ConnectionCount { get; }

    void ConnectionOpened();

    void ConnectionClosed();
}
