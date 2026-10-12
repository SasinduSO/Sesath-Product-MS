using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterlilyInventory.UnitTests.Common;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Common.Exceptions;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.DTOs.Reports;
using WaterlilyInventory.Web.Delegates;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Implementations;

namespace WaterlilyInventory.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="ReportService.GetDashboardAsync"/>.
/// Exercises the real <see cref="ReportService"/> implementation using strict Moq mocks
/// for repository and cache boundaries, verifying all five report datasets, threshold validation,
/// tie handling, cache key separation, cancellation propagation, and sequential execution.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("ReportService")]
public sealed class ReportServiceTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly Mock<IAppCache> _cacheMock = new(MockBehavior.Strict);

    // =========================================================================
    // 1. VALID THRESHOLD PRODUCES ALL FIVE DATASETS (Ties & Distinct Mapping)
    // =========================================================================

    [TestMethod]
    public async Task GetDashboard_ValidThreshold_ReturnsAllFiveDistinctDatasetsAndPreservesThreshold()
    {
        // Arrange
        const int threshold = 15;
        var cancellationToken = CancellationToken.None;

        // Distinct fixed DTO datasets so swapped or omitted mappings are detectable
        IReadOnlyList<CategoryAveragePriceDto> expectedAvgPrices =
        [
            new("Electronics", 45.50m),
            new("Office Furniture", 120.00m)
        ];

        // Tied leaders: both categories have identical highest inventory value ($3,000)
        IReadOnlyList<HighestStockValueCategoryDto> expectedHighestValues =
        [
            new("Electronics", 110, 3000.00m),
            new("Office Furniture", 30, 3000.00m)
        ];

        IReadOnlyList<CategoryInventoryValuationDto> expectedValuations =
        [
            new("Accessories", 40, 500.00m),
            new("Stationery", 130, 1500.00m)
        ];

        IReadOnlyList<LowStockProductDto> expectedLowStock =
        [
            new(101, "Notebook Set", "Stationery", 15.00m, 4),
            new(102, "Desk Lamp", "Electronics", 25.00m, 8)
        ];

        IReadOnlyList<TopExpensiveProductDto> expectedTopExpensive =
        [
            new(201, "Ergonomic Chair", "Office Furniture", 299.99m, 12),
            new(202, "Mechanical Keyboard", "Electronics", 149.99m, 42),
            new(203, "Standing Desk", "Office Furniture", 139.99m, 5)
        ];

        _repositoryMock.Setup(r => r.GetAveragePriceByCategoryAsync(cancellationToken)).ReturnsAsync(expectedAvgPrices);
        _repositoryMock.Setup(r => r.GetHighestStockValueCategoriesAsync(cancellationToken)).ReturnsAsync(expectedHighestValues);
        _repositoryMock.Setup(r => r.GetInventoryValuationByCategoryAsync(cancellationToken)).ReturnsAsync(expectedValuations);
        _repositoryMock.Setup(r => r.GetLowStockProductsAsync(threshold, cancellationToken)).ReturnsAsync(expectedLowStock);
        _repositoryMock.Setup(r => r.GetTopFiveMostExpensiveProductsAsync(cancellationToken)).ReturnsAsync(expectedTopExpensive);

        _cacheMock.SetupCachedMiss<IReadOnlyList<CategoryAveragePriceDto>>(CacheKeys.ReportAveragePrice);
        _cacheMock.SetupCachedMiss<IReadOnlyList<HighestStockValueCategoryDto>>(CacheKeys.ReportHighestValue);
        _cacheMock.SetupCachedMiss<IReadOnlyList<CategoryInventoryValuationDto>>(CacheKeys.ReportValuation);
        _cacheMock.SetupCachedMiss<IReadOnlyList<LowStockProductDto>>(CacheKeys.ReportLowStock(threshold));
        _cacheMock.SetupCachedMiss<IReadOnlyList<TopExpensiveProductDto>>(CacheKeys.ReportTopExpensive);

        var service = new ReportService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ReportService>.Instance);

        // Act
        var result = await service.GetDashboardAsync(threshold, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, "Dashboard generation with valid threshold must succeed.");
        Assert.IsNotNull(result.Data);

        // Preserves the selected threshold
        Assert.AreEqual(threshold, result.Data.LowStockThreshold);

        // Distinct dataset assertions
        Assert.AreSame(expectedAvgPrices, result.Data.AveragePrices);
        Assert.AreSame(expectedHighestValues, result.Data.HighestValueCategories);
        Assert.AreSame(expectedValuations, result.Data.InventoryValuations);
        Assert.AreSame(expectedLowStock, result.Data.LowStockProducts);
        Assert.AreSame(expectedTopExpensive, result.Data.TopExpensiveProducts);

        // Verify ties are preserved
        Assert.HasCount(2, result.Data.HighestValueCategories);
        Assert.AreEqual("Electronics", result.Data.HighestValueCategories[0].Category);
        Assert.AreEqual("Office Furniture", result.Data.HighestValueCategories[1].Category);

        // Verify Top 5 data passed through cleanly
        Assert.HasCount(3, result.Data.TopExpensiveProducts);
        Assert.AreEqual(201, result.Data.TopExpensiveProducts[0].Id);

        VerifyAllFiveRepositoryCalls(threshold, cancellationToken);
        VerifyAllFiveCacheCalls(threshold, cancellationToken);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // 2. THRESHOLD ZERO IS EXPLICITLY VALID
    // =========================================================================

    [TestMethod]
    public async Task GetDashboard_ThresholdZero_IsAcceptedAndPassedToRepositoryAndKey()
    {
        // Arrange
        const int zeroThreshold = 0;
        var cancellationToken = CancellationToken.None;

        IReadOnlyList<CategoryAveragePriceDto> emptyAvg = Array.Empty<CategoryAveragePriceDto>();
        IReadOnlyList<HighestStockValueCategoryDto> emptyHighest = Array.Empty<HighestStockValueCategoryDto>();
        IReadOnlyList<CategoryInventoryValuationDto> emptyVal = Array.Empty<CategoryInventoryValuationDto>();
        IReadOnlyList<LowStockProductDto> emptyLow = Array.Empty<LowStockProductDto>();
        IReadOnlyList<TopExpensiveProductDto> emptyTop = Array.Empty<TopExpensiveProductDto>();

        _repositoryMock.Setup(r => r.GetAveragePriceByCategoryAsync(cancellationToken)).ReturnsAsync(emptyAvg);
        _repositoryMock.Setup(r => r.GetHighestStockValueCategoriesAsync(cancellationToken)).ReturnsAsync(emptyHighest);
        _repositoryMock.Setup(r => r.GetInventoryValuationByCategoryAsync(cancellationToken)).ReturnsAsync(emptyVal);
        _repositoryMock.Setup(r => r.GetLowStockProductsAsync(zeroThreshold, cancellationToken)).ReturnsAsync(emptyLow);
        _repositoryMock.Setup(r => r.GetTopFiveMostExpensiveProductsAsync(cancellationToken)).ReturnsAsync(emptyTop);

        _cacheMock.SetupCachedMiss<IReadOnlyList<CategoryAveragePriceDto>>(CacheKeys.ReportAveragePrice);
        _cacheMock.SetupCachedMiss<IReadOnlyList<HighestStockValueCategoryDto>>(CacheKeys.ReportHighestValue);
        _cacheMock.SetupCachedMiss<IReadOnlyList<CategoryInventoryValuationDto>>(CacheKeys.ReportValuation);
        _cacheMock.SetupCachedMiss<IReadOnlyList<LowStockProductDto>>(CacheKeys.ReportLowStock(zeroThreshold));
        _cacheMock.SetupCachedMiss<IReadOnlyList<TopExpensiveProductDto>>(CacheKeys.ReportTopExpensive);

        var service = new ReportService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ReportService>.Instance);

        // Act
        var result = await service.GetDashboardAsync(zeroThreshold, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, "Threshold of zero is explicitly valid.");
        Assert.AreEqual(zeroThreshold, result.Data!.LowStockThreshold);

        VerifyAllFiveRepositoryCalls(zeroThreshold, cancellationToken);
        VerifyAllFiveCacheCalls(zeroThreshold, cancellationToken);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // 3. NEGATIVE THRESHOLD REJECTION
    // =========================================================================

    [TestMethod]
    [DataRow(-1)]
    [DataRow(-10)]
    public async Task GetDashboard_NegativeThreshold_ReturnsValidationFailureBeforeDataAccess(int negativeThreshold)
    {
        // Arrange
        var service = new ReportService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ReportService>.Instance);

        // Act
        var result = await service.GetDashboardAsync(negativeThreshold);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsTrue(result.Message?.Contains("Low-stock threshold must be zero or positive.", StringComparison.OrdinalIgnoreCase) ?? false);
        Assert.IsNotNull(result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey("lowStockThreshold"));

        // No database queries and no cache interactions
        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // 4. EMPTY DATASETS RETURN LEGITIMATE SUCCESSFUL DASHBOARD
    // =========================================================================

    [TestMethod]
    public async Task GetDashboard_EmptyReportDatasets_ReturnsSuccessfulDashboardWithEmptyCollections()
    {
        // Arrange
        const int threshold = 10;
        var cancellationToken = CancellationToken.None;

        IReadOnlyList<CategoryAveragePriceDto> emptyAvg = Array.Empty<CategoryAveragePriceDto>();
        IReadOnlyList<HighestStockValueCategoryDto> emptyHighest = Array.Empty<HighestStockValueCategoryDto>();
        IReadOnlyList<CategoryInventoryValuationDto> emptyVal = Array.Empty<CategoryInventoryValuationDto>();
        IReadOnlyList<LowStockProductDto> emptyLow = Array.Empty<LowStockProductDto>();
        IReadOnlyList<TopExpensiveProductDto> emptyTop = Array.Empty<TopExpensiveProductDto>();

        _repositoryMock.Setup(r => r.GetAveragePriceByCategoryAsync(cancellationToken)).ReturnsAsync(emptyAvg);
        _repositoryMock.Setup(r => r.GetHighestStockValueCategoriesAsync(cancellationToken)).ReturnsAsync(emptyHighest);
        _repositoryMock.Setup(r => r.GetInventoryValuationByCategoryAsync(cancellationToken)).ReturnsAsync(emptyVal);
        _repositoryMock.Setup(r => r.GetLowStockProductsAsync(threshold, cancellationToken)).ReturnsAsync(emptyLow);
        _repositoryMock.Setup(r => r.GetTopFiveMostExpensiveProductsAsync(cancellationToken)).ReturnsAsync(emptyTop);

        _cacheMock.SetupCachedMiss<IReadOnlyList<CategoryAveragePriceDto>>(CacheKeys.ReportAveragePrice);
        _cacheMock.SetupCachedMiss<IReadOnlyList<HighestStockValueCategoryDto>>(CacheKeys.ReportHighestValue);
        _cacheMock.SetupCachedMiss<IReadOnlyList<CategoryInventoryValuationDto>>(CacheKeys.ReportValuation);
        _cacheMock.SetupCachedMiss<IReadOnlyList<LowStockProductDto>>(CacheKeys.ReportLowStock(threshold));
        _cacheMock.SetupCachedMiss<IReadOnlyList<TopExpensiveProductDto>>(CacheKeys.ReportTopExpensive);

        var service = new ReportService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ReportService>.Instance);

        // Act
        var result = await service.GetDashboardAsync(threshold, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, "Empty datasets are legitimate results, not a 404 or failure.");
        Assert.IsNotNull(result.Data);
        Assert.IsEmpty(result.Data.AveragePrices);
        Assert.IsEmpty(result.Data.HighestValueCategories);
        Assert.IsEmpty(result.Data.InventoryValuations);
        Assert.IsEmpty(result.Data.LowStockProducts);
        Assert.IsEmpty(result.Data.TopExpensiveProducts);

        VerifyAllFiveRepositoryCalls(threshold, cancellationToken);
        VerifyAllFiveCacheCalls(threshold, cancellationToken);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // 5. PERSISTENCE FAILURE IN ANY REPORT RETURNS SAFE FAILURE NOT INVENTED ZEROES
    // =========================================================================

    [TestMethod]
    public async Task GetDashboard_AnyReportPersistenceFailure_ReturnsSafeFailureNotInventedZeroes()
    {
        // Arrange
        const int threshold = 10;
        const string diagnosticMarker = "DB_STORED_PROC_TIMEOUT_0x80131904";
        var cancellationToken = CancellationToken.None;

        IReadOnlyList<CategoryAveragePriceDto> avgPrices = [new("Electronics", 45m)];
        _repositoryMock.Setup(r => r.GetAveragePriceByCategoryAsync(cancellationToken)).ReturnsAsync(avgPrices);
        _cacheMock.SetupCachedMiss<IReadOnlyList<CategoryAveragePriceDto>>(CacheKeys.ReportAveragePrice);

        // Report 2 fails with PersistenceException
        _cacheMock.SetupCachedMiss<IReadOnlyList<HighestStockValueCategoryDto>>(CacheKeys.ReportHighestValue);
        _repositoryMock
            .Setup(r => r.GetHighestStockValueCategoriesAsync(cancellationToken))
            .ThrowsAsync(new PersistenceException(diagnosticMarker));

        var service = new ReportService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ReportService>.Instance);

        // Act
        var result = await service.GetDashboardAsync(threshold, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess, "Failure in any required report must return failure, not invented zero values.");
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode);
        Assert.IsNull(result.Data, "Failed dashboard must not return partial data.");
        Assert.AreEqual("A database error occurred while generating reports.", result.Message);

        // Diagnostic suppressed
        Assert.IsFalse(result.Message?.Contains(diagnosticMarker, StringComparison.OrdinalIgnoreCase) ?? false);

        _repositoryMock.Verify(r => r.GetAveragePriceByCategoryAsync(cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.GetHighestStockValueCategoriesAsync(cancellationToken), Times.Once);
        _repositoryMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // 6. CACHE HIT BYPASSES REPOSITORY
    // =========================================================================

    [TestMethod]
    public async Task GetDashboard_CacheHits_BypassesRepositoryCompletely()
    {
        // Arrange
        const int threshold = 10;
        var cancellationToken = CancellationToken.None;

        IReadOnlyList<CategoryAveragePriceDto> cachedAvg = [new("Electronics", 50m)];
        IReadOnlyList<HighestStockValueCategoryDto> cachedHighest = [new("Electronics", 100, 5000m)];
        IReadOnlyList<CategoryInventoryValuationDto> cachedVal = [new("Electronics", 100, 5000m)];
        IReadOnlyList<LowStockProductDto> cachedLow = [new(1, "Item", "Cat", 10m, 2)];
        IReadOnlyList<TopExpensiveProductDto> cachedTop = [new(1, "Item", "Cat", 10m, 2)];

        _cacheMock.SetupCachedHit(CacheKeys.ReportAveragePrice, cachedAvg);
        _cacheMock.SetupCachedHit(CacheKeys.ReportHighestValue, cachedHighest);
        _cacheMock.SetupCachedHit(CacheKeys.ReportValuation, cachedVal);
        _cacheMock.SetupCachedHit(CacheKeys.ReportLowStock(threshold), cachedLow);
        _cacheMock.SetupCachedHit(CacheKeys.ReportTopExpensive, cachedTop);

        var service = new ReportService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ReportService>.Instance);

        // Act
        var result = await service.GetDashboardAsync(threshold, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(cachedAvg, result.Data!.AveragePrices);

        // Repository was never queried on full cache hit
        _repositoryMock.VerifyNoOtherCalls();

        VerifyAllFiveCacheCalls(threshold, cancellationToken);
        _cacheMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // 7. CANCELLATION PROPAGATES
    // =========================================================================

    [TestMethod]
    public async Task GetDashboard_CancellationPropagates_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _cacheMock
            .Setup(c => c.CachedAsync(
                CacheKeys.ReportAveragePrice,
                It.IsAny<AsyncDataFactory<IReadOnlyList<CategoryAveragePriceDto>>>(),
                token))
            .ThrowsAsync(new OperationCanceledException(token));

        var service = new ReportService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ReportService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetDashboardAsync(10, token));

        _repositoryMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // 8. SEQUENTIAL EXECUTION COORDINATION CHECK (No Overlapping Repository Calls)
    // =========================================================================

    [TestMethod]
    public async Task GetDashboard_SequentialExecution_DoesNotStartOverlappingRepositoryCalls()
    {
        // Arrange
        const int threshold = 10;
        var cancellationToken = CancellationToken.None;

        var inFlightCount = 0;
        var maxConcurrentCalls = 0;

        async Task TrackConcurrency()
        {
            var current = Interlocked.Increment(ref inFlightCount);
            if (current > maxConcurrentCalls)
            {
                Interlocked.Exchange(ref maxConcurrentCalls, current);
            }
            await Task.Yield();
            Interlocked.Decrement(ref inFlightCount);
        }

        _repositoryMock
            .Setup(r => r.GetAveragePriceByCategoryAsync(cancellationToken))
            .Returns(async () =>
            {
                await TrackConcurrency();
                return Array.Empty<CategoryAveragePriceDto>();
            });

        _repositoryMock
            .Setup(r => r.GetHighestStockValueCategoriesAsync(cancellationToken))
            .Returns(async () =>
            {
                await TrackConcurrency();
                return Array.Empty<HighestStockValueCategoryDto>();
            });

        _repositoryMock
            .Setup(r => r.GetInventoryValuationByCategoryAsync(cancellationToken))
            .Returns(async () =>
            {
                await TrackConcurrency();
                return Array.Empty<CategoryInventoryValuationDto>();
            });

        _repositoryMock
            .Setup(r => r.GetLowStockProductsAsync(threshold, cancellationToken))
            .Returns(async () =>
            {
                await TrackConcurrency();
                return Array.Empty<LowStockProductDto>();
            });

        _repositoryMock
            .Setup(r => r.GetTopFiveMostExpensiveProductsAsync(cancellationToken))
            .Returns(async () =>
            {
                await TrackConcurrency();
                return Array.Empty<TopExpensiveProductDto>();
            });

        _cacheMock.SetupCachedMiss<IReadOnlyList<CategoryAveragePriceDto>>(CacheKeys.ReportAveragePrice);
        _cacheMock.SetupCachedMiss<IReadOnlyList<HighestStockValueCategoryDto>>(CacheKeys.ReportHighestValue);
        _cacheMock.SetupCachedMiss<IReadOnlyList<CategoryInventoryValuationDto>>(CacheKeys.ReportValuation);
        _cacheMock.SetupCachedMiss<IReadOnlyList<LowStockProductDto>>(CacheKeys.ReportLowStock(threshold));
        _cacheMock.SetupCachedMiss<IReadOnlyList<TopExpensiveProductDto>>(CacheKeys.ReportTopExpensive);

        var service = new ReportService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ReportService>.Instance);

        // Act
        var result = await service.GetDashboardAsync(threshold, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, maxConcurrentCalls, "Repository calls on the shared scoped DbContext must be strictly sequential (non-overlapping).");

        VerifyAllFiveRepositoryCalls(threshold, cancellationToken);
        VerifyAllFiveCacheCalls(threshold, cancellationToken);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    private void VerifyAllFiveRepositoryCalls(int threshold, CancellationToken cancellationToken)
    {
        _repositoryMock.Verify(r => r.GetAveragePriceByCategoryAsync(cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.GetHighestStockValueCategoriesAsync(cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.GetInventoryValuationByCategoryAsync(cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.GetLowStockProductsAsync(threshold, cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.GetTopFiveMostExpensiveProductsAsync(cancellationToken), Times.Once);
    }

    private void VerifyAllFiveCacheCalls(int threshold, CancellationToken cancellationToken)
    {
        _cacheMock.Verify(c => c.CachedAsync(CacheKeys.ReportAveragePrice, It.IsAny<AsyncDataFactory<IReadOnlyList<CategoryAveragePriceDto>>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.CachedAsync(CacheKeys.ReportHighestValue, It.IsAny<AsyncDataFactory<IReadOnlyList<HighestStockValueCategoryDto>>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.CachedAsync(CacheKeys.ReportValuation, It.IsAny<AsyncDataFactory<IReadOnlyList<CategoryInventoryValuationDto>>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.CachedAsync(CacheKeys.ReportLowStock(threshold), It.IsAny<AsyncDataFactory<IReadOnlyList<LowStockProductDto>>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.CachedAsync(CacheKeys.ReportTopExpensive, It.IsAny<AsyncDataFactory<IReadOnlyList<TopExpensiveProductDto>>>(), cancellationToken), Times.Once);
    }
}
