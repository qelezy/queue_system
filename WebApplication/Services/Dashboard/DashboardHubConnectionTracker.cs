namespace WebApplication.Services.Dashboard;

public sealed class DashboardHubConnectionTracker : IDashboardHubConnectionTracker
{
    private int _connectionCount;

    public int ConnectionCount => Volatile.Read(ref _connectionCount);

    public void ConnectionOpened() => Interlocked.Increment(ref _connectionCount);

    public void ConnectionClosed()
    {
        var value = Interlocked.Decrement(ref _connectionCount);
        if (value < 0)
            Interlocked.Exchange(ref _connectionCount, 0);
    }
}
