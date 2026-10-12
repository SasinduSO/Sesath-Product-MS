using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WaterlilyInventory.Web.Delegates;

namespace WaterlilyInventory.Web.Caching;

/// <summary>
/// IMemoryCache-backed implementation of <see cref="IAppCache"/>.
/// Registered as a singleton — the underlying IMemoryCache is also singleton.
/// This class must not capture scoped Services/Repositories/DbContext; instead,
/// the <see cref="AsyncDataFactory{T}"/> delegate is supplied per-request by scoped Services.
/// 
/// Stale-data prevention: uses a generation counter (<see cref="_generation"/>)
/// that is incremented on invalidation. A cache miss captures the current generation
/// before loading; after loading, data is stored only if the generation has not changed.
/// This prevents a slow read that started before a write from repopulating the cache
/// with stale data after invalidation.
/// 
/// Neither Cached nor CachedLong mode survives application restart.
/// </summary>
public class AppCache : IAppCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<AppCache> _logger;
    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new();
    private long _generation;

    public AppCache(IMemoryCache cache, ILogger<AppCache> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<T?> CachedAsync<T>(string key, AsyncDataFactory<T> factory, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (_cache.TryGetValue(key, out T? cached))
        {
            _logger.LogDebug("Cache HIT for key {CacheKey} (Cached mode).", key);
            return cached;
        }

        _logger.LogDebug("Cache MISS for key {CacheKey} (Cached mode). Invoking AsyncDataFactory delegate.", key);

        // Capture generation before loading to detect concurrent invalidation
        var capturedGeneration = Interlocked.Read(ref _generation);

        // Invoke the delegate callback — runs within the active request scope
        var data = await factory(cancellationToken);

        // Only store if the generation has not changed (no invalidation occurred during load)
        if (Interlocked.Read(ref _generation) == capturedGeneration && data is not null)
        {
            // Cached mode: absolute expiration of 5 minutes; repeated hits do not extend
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
            _cache.Set(key, data, options);
            _trackedKeys.TryAdd(key, 0);
            _logger.LogDebug("Stored key {CacheKey} in cache (Cached mode, 5-min absolute expiry).", key);
        }

        return data;
    }

    /// <inheritdoc/>
    public async Task<T?> CachedLongAsync<T>(string key, AsyncDataFactory<T> factory, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (_cache.TryGetValue(key, out T? cached))
        {
            _logger.LogDebug("Cache HIT for key {CacheKey} (CachedLong mode).", key);
            return cached;
        }

        _logger.LogDebug("Cache MISS for key {CacheKey} (CachedLong mode). Invoking AsyncDataFactory delegate.", key);

        var capturedGeneration = Interlocked.Read(ref _generation);
        var data = await factory(cancellationToken);

        if (Interlocked.Read(ref _generation) == capturedGeneration && data is not null)
        {
            // CachedLong mode: no time-based expiration; NeverRemove priority prevents compaction eviction.
            // Entries live until explicit invalidation or process restart.
            var options = new MemoryCacheEntryOptions
            {
                Priority = CacheItemPriority.NeverRemove
            };
            _cache.Set(key, data, options);
            _trackedKeys.TryAdd(key, 0);
            _logger.LogDebug("Stored key {CacheKey} in cache (CachedLong mode, no time expiry, NeverRemove priority).", key);
        }

        return data;
    }

    /// <inheritdoc/>
    public void Invalidate(string key)
    {
        _cache.Remove(key);
        _trackedKeys.TryRemove(key, out _);
        // Increment generation to prevent stale in-flight loads from repopulating
        Interlocked.Increment(ref _generation);
        _logger.LogDebug("Invalidated cache key {CacheKey}. Generation advanced.", key);
    }

    /// <inheritdoc/>
    public void InvalidateByPrefix(string prefix)
    {
        var keysToRemove = _trackedKeys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _trackedKeys.TryRemove(key, out _);
        }
        // Increment generation once for the entire batch
        Interlocked.Increment(ref _generation);
        _logger.LogDebug("Invalidated {Count} cache keys with prefix {Prefix}. Generation advanced.", keysToRemove.Count, prefix);
    }

    /// <inheritdoc/>
    public void InvalidateProductAndReports(int? productId = null)
    {
        // Invalidate full product list
        _cache.Remove(CacheKeys.ProductList);
        _trackedKeys.TryRemove(CacheKeys.ProductList, out _);

        // Invalidate specific product if provided
        if (productId.HasValue)
        {
            var productKey = CacheKeys.ProductById(productId.Value);
            _cache.Remove(productKey);
            _trackedKeys.TryRemove(productKey, out _);
        }

        // Invalidate all report entries (all five reports, including every threshold variant)
        var reportKeys = _trackedKeys.Keys.Where(k => k.StartsWith(CacheKeys.ReportPrefix, StringComparison.Ordinal)).ToList();
        foreach (var key in reportKeys)
        {
            _cache.Remove(key);
            _trackedKeys.TryRemove(key, out _);
        }

        // Advance generation once so old in-flight reads cannot repopulate stale data
        Interlocked.Increment(ref _generation);
        _logger.LogInformation("Invalidated shared product/report cache scope for ProductId {ProductId}. Cleared {ReportCount} report entries.",
            productId?.ToString() ?? "All", reportKeys.Count);
    }
}
