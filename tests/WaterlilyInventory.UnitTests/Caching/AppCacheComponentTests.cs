using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.DTOs.Products;

namespace WaterlilyInventory.UnitTests.Caching;

/// <summary>
/// Component tests for the real production <see cref="AppCache"/> implementation.
/// Uses a fresh, actual <see cref="IMemoryCache"/> per test with deterministic time control
/// via <see cref="TimeProvider"/>, testing cache hit/miss, key separation, null handling,
/// invalidation scopes, 5-minute absolute expiration, and stale in-flight read prevention.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("CacheComponent")]
public sealed class AppCacheComponentTests : IDisposable
{
    private readonly TestClock _clock = new();
    private readonly IMemoryCache _memoryCache;
    private readonly AppCache _appCache;

    public AppCacheComponentTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            Clock = _clock
        });

        _appCache = new AppCache(_memoryCache, NullLogger<AppCache>.Instance);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    // =========================================================================
    // 1. MISS THEN HIT
    // =========================================================================

    [TestMethod]
    public async Task CachedAsync_MissThenHit_InvokesFactoryOnceAndReturnsCachedValue()
    {
        // Arrange
        const string key = "test:miss-then-hit";
        var invocationCount = 0;
        var expectedItem = new TestItem("Item 1");

        // Act - First read (cache miss)
        var result1 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(expectedItem);
        });

        // Act - Second read (cache hit)
        var result2 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(new TestItem("Item 2"));
        });

        // Assert
        Assert.AreEqual(1, invocationCount, "Factory must only be invoked once across two reads for the same key.");
        Assert.AreSame(expectedItem, result1);
        Assert.AreSame(expectedItem, result2);
    }

    [TestMethod]
    public async Task CachedLongAsync_MissThenHit_InvokesFactoryOnceAndReturnsCachedValue()
    {
        // Arrange
        const string key = "test:long:miss-then-hit";
        var invocationCount = 0;
        var expectedItem = new TestItem("Long Item 1");

        // Act - First read (miss)
        var result1 = await _appCache.CachedLongAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(expectedItem);
        });

        // Act - Second read (hit)
        var result2 = await _appCache.CachedLongAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(new TestItem("Long Item 2"));
        });

        // Assert
        Assert.AreEqual(1, invocationCount);
        Assert.AreSame(expectedItem, result1);
        Assert.AreSame(expectedItem, result2);
    }

    // =========================================================================
    // 2. KEY SEPARATION
    // =========================================================================

    [TestMethod]
    public async Task CachedAsync_KeySeparation_MaintainsIndependentEntriesForDifferentKeys()
    {
        // Arrange
        var key1 = CacheKeys.ProductById(101);
        var key2 = CacheKeys.ProductById(102);
        var reportKey1 = CacheKeys.ReportLowStock(5);
        var reportKey2 = CacheKeys.ReportLowStock(10);

        var item1 = new TestItem("Product 101");
        var item2 = new TestItem("Product 102");
        var reportItem1 = new TestItem("LowStock Threshold 5");
        var reportItem2 = new TestItem("LowStock Threshold 10");

        // Act
        var res1 = await _appCache.CachedAsync(key1, token => Task.FromResult(item1));
        var res2 = await _appCache.CachedAsync(key2, token => Task.FromResult(item2));
        var rep1 = await _appCache.CachedAsync(reportKey1, token => Task.FromResult(reportItem1));
        var rep2 = await _appCache.CachedAsync(reportKey2, token => Task.FromResult(reportItem2));

        // Assert
        Assert.AreSame(item1, res1);
        Assert.AreSame(item2, res2);
        Assert.AreSame(reportItem1, rep1);
        Assert.AreSame(reportItem2, rep2);
        Assert.AreNotSame(res1, res2);
        Assert.AreNotSame(rep1, rep2);
    }

    // =========================================================================
    // 3. SUCCESSFUL EMPTY COLLECTION RETAINED
    // =========================================================================

    [TestMethod]
    public async Task CachedAsync_SuccessfulEmptyCollection_IsRetainedInCache()
    {
        // Arrange
        const string key = "test:empty-collection";
        var invocationCount = 0;
        IReadOnlyList<ProductDto> emptyList = Array.Empty<ProductDto>();

        // Act - First read (miss)
        var result1 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(emptyList);
        });

        // Act - Second read (hit)
        var result2 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult<IReadOnlyList<ProductDto>>(new List<ProductDto>
            {
                new(1, "Should Not Be Returned", "Cat", 10m, 5)
            });
        });

        // Assert
        Assert.AreEqual(1, invocationCount, "Empty collection is legitimate data and must be cached on subsequent reads.");
        Assert.IsNotNull(result1);
        Assert.IsEmpty(result1);
        Assert.AreSame(emptyList, result2);
    }

    // =========================================================================
    // 4. MISSING PRODUCT / NULL NOT RETAINED
    // =========================================================================

    [TestMethod]
    public async Task CachedAsync_MissingProductOrNull_IsNotRetainedInCache()
    {
        // Arrange
        const string key = "test:null-not-retained";
        var invocationCount = 0;

        // Act - First read returns null (e.g. 404 lookup)
        var result1 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult<TestItem>(null!);
        });

        // Act - Second read returns valid item
        var expectedItem = new TestItem("Recovered Item");
        var result2 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(expectedItem);
        });

        // Assert
        Assert.IsNull(result1);
        Assert.AreSame(expectedItem, result2);
        Assert.AreEqual(2, invocationCount, "Null result must not be cached; second request must re-invoke factory.");
    }

    // =========================================================================
    // 5. FACTORY EXCEPTION OR CANCELLATION DOES NOT POPULATE CACHE
    // =========================================================================

    [TestMethod]
    public async Task CachedAsync_FactoryThrowsException_DoesNotPopulateCache()
    {
        // Arrange
        const string key = "test:exception-not-cached";
        var invocationCount = 0;

        // Act 1 - First read throws
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _appCache.CachedAsync<TestItem>(key, token =>
            {
                invocationCount++;
                throw new InvalidOperationException("Simulated factory failure");
            });
        });

        // Act 2 - Later legitimate request succeeds
        var expectedItem = new TestItem("Healthy Item");
        var result = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(expectedItem);
        });

        // Assert
        Assert.AreEqual(2, invocationCount);
        Assert.AreSame(expectedItem, result);
    }

    [TestMethod]
    public async Task CachedAsync_FactoryCancelled_DoesNotPopulateCache()
    {
        // Arrange
        const string key = "test:cancellation-not-cached";
        var invocationCount = 0;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act 1 - Cancelled call
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _appCache.CachedAsync<TestItem>(key, token =>
            {
                invocationCount++;
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new TestItem("Cancelled"));
            }, cts.Token);
        });

        // Act 2 - Subsequent uncancelled call
        var expectedItem = new TestItem("Uncancelled Item");
        var result = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(expectedItem);
        }, CancellationToken.None);

        // Assert
        Assert.AreEqual(2, invocationCount);
        Assert.AreSame(expectedItem, result);
    }

    // =========================================================================
    // 6. SHARED SCOPE INVALIDATION
    // =========================================================================

    [TestMethod]
    public async Task InvalidateProductAndReports_InvalidatesBothModesAndRepresentativeKeys()
    {
        // Arrange
        const int productId = 101;
        var listKey = CacheKeys.ProductList;
        var detailKey = CacheKeys.ProductById(productId);
        var avgPriceKey = CacheKeys.ReportAveragePrice;
        var lowStockKey10 = CacheKeys.ReportLowStock(10);
        var lowStockKey5 = CacheKeys.ReportLowStock(5);
        var topExpKey = CacheKeys.ReportTopExpensive;

        // Populate Cached mode entries
        await _appCache.CachedAsync(listKey, token => Task.FromResult(new TestItem("List V1")));
        await _appCache.CachedAsync(avgPriceKey, token => Task.FromResult(new TestItem("AvgPrice V1")));
        await _appCache.CachedAsync(lowStockKey10, token => Task.FromResult(new TestItem("LowStock10 V1")));
        await _appCache.CachedAsync(lowStockKey5, token => Task.FromResult(new TestItem("LowStock5 V1")));
        await _appCache.CachedAsync(topExpKey, token => Task.FromResult(new TestItem("TopExp V1")));

        // Populate CachedLong mode entry
        await _appCache.CachedLongAsync(detailKey, token => Task.FromResult(new TestItem("Detail V1")));

        // Act - Invalidate shared product and report scope for product 101
        _appCache.InvalidateProductAndReports(productId);

        // Assert - Subsequent reads across all keys must invoke factory again (receiving V2)
        var listV2 = await _appCache.CachedAsync(listKey, token => Task.FromResult(new TestItem("List V2")));
        var detailV2 = await _appCache.CachedLongAsync(detailKey, token => Task.FromResult(new TestItem("Detail V2")));
        var avgPriceV2 = await _appCache.CachedAsync(avgPriceKey, token => Task.FromResult(new TestItem("AvgPrice V2")));
        var lowStock10V2 = await _appCache.CachedAsync(lowStockKey10, token => Task.FromResult(new TestItem("LowStock10 V2")));
        var lowStock5V2 = await _appCache.CachedAsync(lowStockKey5, token => Task.FromResult(new TestItem("LowStock5 V2")));
        var topExpV2 = await _appCache.CachedAsync(topExpKey, token => Task.FromResult(new TestItem("TopExp V2")));

        Assert.AreEqual("List V2", listV2!.Name);
        Assert.AreEqual("Detail V2", detailV2!.Name);
        Assert.AreEqual("AvgPrice V2", avgPriceV2!.Name);
        Assert.AreEqual("LowStock10 V2", lowStock10V2!.Name);
        Assert.AreEqual("LowStock5 V2", lowStock5V2!.Name);
        Assert.AreEqual("TopExp V2", topExpV2!.Name);
    }

    // =========================================================================
    // 7. GENERIC NON-COLLECTION RESULT (Zero/False Value Preservation)
    // =========================================================================

    [TestMethod]
    public async Task CachedAsync_ZeroOrFalseValue_IsNotConfusedWithMissingReference()
    {
        // Arrange
        const string key = "test:zero-metric";
        var invocationCount = 0;
        var legitimateZeroValue = new MetricResult(Count: 0, IsActive: false);

        // Act - First read (miss)
        var result1 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(legitimateZeroValue);
        });

        // Act - Second read (hit)
        var result2 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(new MetricResult(Count: 99, IsActive: true));
        });

        // Assert
        Assert.AreEqual(1, invocationCount, "Zero/false reference value must be cached and not treated as missing.");
        Assert.AreSame(legitimateZeroValue, result1);
        Assert.AreSame(legitimateZeroValue, result2);
        Assert.AreEqual(0, result2!.Count);
        Assert.IsFalse(result2.IsActive);
    }

    // =========================================================================
    // 8. EXPIRATION (5-Minute Absolute Deadline vs. CachedLong)
    // =========================================================================

    [TestMethod]
    public async Task CachedAsync_AbsoluteExpiration_ExpiresAfterFiveMinutesAndHitsDoNotExtend()
    {
        // Arrange
        const string key = "test:absolute-expiry";
        var invocationCount = 0;

        // Act 1 - Initial store at t = 0m
        var result1 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(new TestItem($"Version {invocationCount}"));
        });
        Assert.AreEqual(1, invocationCount);
        Assert.AreEqual("Version 1", result1!.Name);

        // Act 2 - Hit at t = +4m 50s (just before 5m deadline)
        _clock.Advance(TimeSpan.FromSeconds(290)); // 4m 50s

        var result2 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(new TestItem($"Version {invocationCount}"));
        });
        Assert.AreEqual(1, invocationCount, "Hit before 5m deadline must not invoke factory.");
        Assert.AreEqual("Version 1", result2!.Name);

        // Act 3 - Advance past absolute deadline to t = +5m 10s (total +310s from start)
        // If the hit at 4m 50s had incorrectly extended expiration, this would still be cached.
        // Under absolute expiration, it MUST expire.
        _clock.Advance(TimeSpan.FromSeconds(20)); // Total 5m 10s from start

        var result3 = await _appCache.CachedAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(new TestItem($"Version {invocationCount}"));
        });

        // Assert
        Assert.AreEqual(2, invocationCount, "Absolute expiration must expire at 5m; prior hits must NOT extend it.");
        Assert.AreEqual("Version 2", result3!.Name);
    }

    [TestMethod]
    public async Task CachedLongAsync_NoTimeBasedExpiration_SurvivesPastOrdinaryCacheDuration()
    {
        // Arrange
        const string key = "test:cached-long:no-expiry";
        var invocationCount = 0;

        // Act 1 - Initial store
        var result1 = await _appCache.CachedLongAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(new TestItem($"Long V{invocationCount}"));
        });
        Assert.AreEqual(1, invocationCount);

        // Act 2 - Advance clock past 5 minutes, 1 hour, and 24 hours
        _clock.Advance(TimeSpan.FromHours(24));

        var result2 = await _appCache.CachedLongAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(new TestItem($"Long V{invocationCount}"));
        });

        // Assert - Still cached! Zero additional factory calls
        Assert.AreEqual(1, invocationCount, "CachedLong must not have time-based expiration.");
        Assert.AreEqual("Long V1", result2!.Name);

        // Act 3 - Explicit invalidation forces refresh
        _appCache.Invalidate(key);

        var result3 = await _appCache.CachedLongAsync(key, token =>
        {
            invocationCount++;
            return Task.FromResult(new TestItem($"Long V{invocationCount}"));
        });

        Assert.AreEqual(2, invocationCount, "Explicit invalidation must refresh CachedLong entry.");
        Assert.AreEqual("Long V2", result3!.Name);
    }

    // =========================================================================
    // 9. STALE IN-FLIGHT READ PREVENTION
    // =========================================================================

    [TestMethod]
    public async Task StaleInFlightRead_OldGenerationLoad_DoesNotOverwriteNewGenerationData()
    {
        // Arrange
        var key = CacheKeys.ProductList;
        var oldFactoryEnteredSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldFactorySignal = new TaskCompletionSource<TestItem>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Step 1: Start an old-generation cache miss and pause its factory after entry
        var oldReadTask = _appCache.CachedAsync(key, async token =>
        {
            oldFactoryEnteredSignal.TrySetResult(true);
            return await releaseOldFactorySignal.Task;
        });

        // Await factory entry
        var entered = await oldFactoryEnteredSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(entered, "Old factory must enter.");

        // Step 2: Invalidate the product scope (representing a confirmed database write in another request)
        // This increments _generation from 0 to 1 and removes the key.
        _appCache.InvalidateProductAndReports();

        // Step 3: Read the same logical key in the new generation and publish a new value
        var newGenerationItem = new TestItem("New Generation Fresh Value");
        var newReadResult = await _appCache.CachedAsync(key, token => Task.FromResult(newGenerationItem));
        Assert.AreSame(newGenerationItem, newReadResult);

        // Step 4: Release the old factory with the stale value and await its completion
        var staleItem = new TestItem("Old Generation Stale Value");
        releaseOldFactorySignal.TrySetResult(staleItem);
        var oldReadResult = await oldReadTask.WaitAsync(TimeSpan.FromSeconds(5));

        // The old caller legitimately receives its snapshot
        Assert.AreSame(staleItem, oldReadResult);

        // Step 5: Read the cache again. Prove that the new-generation value remains in the cache
        // and the old load did NOT overwrite it with stale data!
        var subsequentRead = await _appCache.CachedAsync(key, token =>
        {
            Assert.Fail("Factory should not be invoked because new-generation value must be cached.");
            return Task.FromResult(new TestItem("Unexpected Value"));
        });

        Assert.AreSame(newGenerationItem, subsequentRead, "New-generation value must not be overwritten by delayed old-generation load.");
    }

    // =========================================================================
    // HELPER TEST TYPES
    // =========================================================================

    private sealed record TestItem(string Name);

    private sealed record MetricResult(int Count, bool IsActive);

    private sealed class TestClock : Microsoft.Extensions.Internal.ISystemClock
    {
        private DateTimeOffset _utcNow = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset UtcNow => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
