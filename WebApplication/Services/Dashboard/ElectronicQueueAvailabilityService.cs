using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WebApplication.Data;

namespace WebApplication.Services.Dashboard;

public sealed class ElectronicQueueAvailabilityService : IElectronicQueueAvailability
{
    public const string CacheKey = nameof(ElectronicQueueAvailabilityService) + ":CanConnect";

    private readonly ElectronicQueueDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly MonitoringOptions _opt;

    public ElectronicQueueAvailabilityService(
        ElectronicQueueDbContext db,
        IMemoryCache cache,
        IOptions<MonitoringOptions> options)
    {
        _db = db;
        _cache = cache;
        _opt = options.Value;
    }

    public bool TryGetCachedAvailability(out bool canConnectLive) =>
        _cache.TryGetValue(CacheKey, out canConnectLive);

    public async Task<bool> CanQueryLiveDataAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out bool cached))
            return cached;

        var ttl = TimeSpan.FromSeconds(Math.Max(5, _opt.QueueAvailabilityCacheSeconds));
        bool ok;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(3));
            ok = await _db.Database.CanConnectAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ok = false;
        }
        catch
        {
            ok = false;
        }

        _cache.Set(CacheKey, ok, CacheEntryOptions(ttl));
        return ok;
    }

    public void MarkUnavailable()
    {
        var ttl = TimeSpan.FromSeconds(Math.Max(5, _opt.QueueAvailabilityCacheSeconds));
        _cache.Set(CacheKey, false, CacheEntryOptions(ttl));
    }

    private static MemoryCacheEntryOptions CacheEntryOptions(TimeSpan ttl) =>
        new() { AbsoluteExpirationRelativeToNow = ttl };
}
