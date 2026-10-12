using Moq;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Delegates;

namespace WaterlilyInventory.UnitTests.Common;

/// <summary>
/// Reusable test setup helpers for configuring <see cref="IAppCache"/> mocks.
/// Ensures miss behaviors invoke and await the real application's <see cref="AsyncDataFactory{T}"/> delegate callback.
/// </summary>
public static class CacheMockExtensions
{
    /// <summary>
    /// Configures <see cref="IAppCache.CachedAsync{T}"/> to simulate a cache miss by executing
    /// and awaiting the supplied <see cref="AsyncDataFactory{T}"/> delegate callback.
    /// </summary>
    public static void SetupCachedMiss<T>(this Mock<IAppCache> cacheMock, string key) where T : class
    {
        cacheMock
            .Setup(c => c.CachedAsync(
                key,
                It.IsAny<AsyncDataFactory<T>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, AsyncDataFactory<T> factory, CancellationToken token) => (T?)await factory(token));
    }

    /// <summary>
    /// Configures <see cref="IAppCache.CachedLongAsync{T}"/> to simulate a cache miss by executing
    /// and awaiting the supplied <see cref="AsyncDataFactory{T}"/> delegate callback.
    /// </summary>
    public static void SetupCachedLongMiss<T>(this Mock<IAppCache> cacheMock, string key) where T : class
    {
        cacheMock
            .Setup(c => c.CachedLongAsync(
                key,
                It.IsAny<AsyncDataFactory<T>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, AsyncDataFactory<T> factory, CancellationToken token) => (T?)await factory(token));
    }

    /// <summary>
    /// Configures <see cref="IAppCache.CachedAsync{T}"/> to simulate a cache hit returning pre-stored snapshot data
    /// without invoking the factory delegate callback.
    /// </summary>
    public static void SetupCachedHit<T>(this Mock<IAppCache> cacheMock, string key, T cachedValue) where T : class
    {
        cacheMock
            .Setup(c => c.CachedAsync(
                key,
                It.IsAny<AsyncDataFactory<T>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedValue);
    }

    /// <summary>
    /// Configures <see cref="IAppCache.CachedLongAsync{T}"/> to simulate a cache hit returning pre-stored snapshot data
    /// without invoking the factory delegate callback.
    /// </summary>
    public static void SetupCachedLongHit<T>(this Mock<IAppCache> cacheMock, string key, T cachedValue) where T : class
    {
        cacheMock
            .Setup(c => c.CachedLongAsync(
                key,
                It.IsAny<AsyncDataFactory<T>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedValue);
    }
}
