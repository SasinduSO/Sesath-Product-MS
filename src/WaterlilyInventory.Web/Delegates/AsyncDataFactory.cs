namespace WaterlilyInventory.Web.Delegates;

/// <summary>
/// A named generic asynchronous data-factory delegate.
///
/// A delegate represents a callable method reference — it separates cache policy from
/// data retrieval strategy. The cache infrastructure accepts this delegate type and invokes
/// it only on a cache miss, so the expensive database query runs only when needed.
///
/// Services supply an async callback (typically calling a Repository method and mapping
/// results to DTOs) that the cache invokes within the active request scope. The delegate
/// must not be captured by singletons for later background execution because it may close
/// over scoped dependencies (DbContext, Repository).
/// </summary>
/// <typeparam name="T">The type of data produced by this factory.</typeparam>
/// <param name="cancellationToken">Cancellation token propagated from the original request.</param>
/// <returns>The freshly loaded data of type <typeparamref name="T"/>.</returns>
public delegate Task<T> AsyncDataFactory<T>(CancellationToken cancellationToken);
