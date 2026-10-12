using WaterlilyInventory.Web.Delegates;

namespace WaterlilyInventory.Web.Caching;

/// <summary>
/// Reusable memory-cache abstraction consumed by Services.
/// Provides two cache modes using the <see cref="AsyncDataFactory{T}"/> delegate callback:
/// <list type="bullet">
///   <item><description><b>Cached</b> (<see cref="CachedAsync{T}"/>): Absolute expiration ≤ 5 minutes. Repeated hits do not extend.</description></item>
///   <item><description><b>CachedLong</b> (<see cref="CachedLongAsync{T}"/>): No time-based expiration within the running cache. Survives until explicit invalidation or process restart.</description></item>
/// </list>
/// Neither mode survives application restart.
/// </summary>
public interface IAppCache
{
    /// <summary>
    /// Returns cached data for the given key, invoking the <paramref name="factory"/> delegate
    /// callback only on a cache miss. Uses absolute expiration of 5 minutes.
    /// </summary>
    /// <typeparam name="T">The cached data type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Async data-factory delegate invoked on cache miss to load fresh data.</param>
    /// <param name="cancellationToken">Cancellation token propagated to the factory.</param>
    /// <returns>The cached or freshly loaded data.</returns>
    Task<T?> CachedAsync<T>(string key, AsyncDataFactory<T> factory, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Returns cached data for the given key, invoking the <paramref name="factory"/> delegate
    /// callback only on a cache miss. No time-based expiration — entries live until invalidation
    /// or process restart. Protected from priority-based compaction.
    /// </summary>
    Task<T?> CachedLongAsync<T>(string key, AsyncDataFactory<T> factory, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Removes a specific key from the cache.
    /// </summary>
    void Invalidate(string key);

    /// <summary>
    /// Removes all keys matching the given prefix from the tracked key set.
    /// Used for scope-based invalidation (e.g., all product keys, all report keys).
    /// </summary>
    void InvalidateByPrefix(string prefix);

    /// <summary>
    /// Invalidates all product list, detail, and affected report entries (including all threshold variants).
    /// Shared invalidation scope used by ProductService and ReportService on confirmed successful writes.
    /// Advances cache generation so in-flight old reads cannot repopulate stale data.
    /// </summary>
    /// <param name="productId">Optional product ID for specific detail invalidation, or null for all products.</param>
    void InvalidateProductAndReports(int? productId = null);
}
