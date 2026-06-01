using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using WebApplication.Data;

namespace WebApplication.Tests.Reports;

internal static class ElectronicQueueTestDb
{
    internal const string RequiresDbTrait = "RequiresDb";

    private static string? _connectionString;

    internal static string? TryGetConnectionString()
    {
        if (_connectionString is not null)
            return string.IsNullOrWhiteSpace(_connectionString) ? null : _connectionString;

        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__ElectronicQueue");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            _connectionString = fromEnv;
            return _connectionString;
        }

        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WebApplication"));
        var envPath = Path.Combine(webRoot, ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
            fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__ElectronicQueue");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                _connectionString = fromEnv;
                return _connectionString;
            }
        }

        _connectionString = "";
        return null;
    }

    internal static ElectronicQueueDbContext CreateContext()
    {
        var cs = TryGetConnectionString()
                 ?? throw new InvalidOperationException(
                     "ConnectionStrings__ElectronicQueue is not configured for integration tests.");

        var options = new DbContextOptionsBuilder<ElectronicQueueDbContext>()
            .UseSqlServer(cs)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new ElectronicQueueDbContext(options);
    }

    internal static async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        var cs = TryGetConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
            return false;

        await using var db = CreateContext();
        return await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
    }
}
