using Microsoft.Extensions.Logging;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Common.Exceptions;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.DTOs.Reports;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Interfaces;

namespace WaterlilyInventory.Web.Services.Implementations;

/// <summary>
/// Service implementation for the reporting dashboard.
/// Retrieves and caches all five SQL Server stored-procedure report datasets.
/// Calls repository methods sequentially (never Task.WhenAll on the shared DbContext)
/// and preserves the database stored-procedure logic without substitute LINQ calculations.
/// </summary>
public class ReportService : IReportService
{
    private readonly IProductRepository _productRepository;
    private readonly IAppCache _cache;
    private readonly ILogger<ReportService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ReportService"/>.
    /// </summary>
    public ReportService(
        IProductRepository productRepository,
        IAppCache cache,
        ILogger<ReportService> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<OperationResult<ReportDashboardDto>> GetDashboardAsync(
        int lowStockThreshold = 10,
        CancellationToken cancellationToken = default)
    {
        // Validate threshold defensively before initiating repository operations
        if (lowStockThreshold < 0)
        {
            return OperationResult<ReportDashboardDto>.ValidationFailure(
                "Low-stock threshold must be zero or positive.",
                new Dictionary<string, string[]>
                {
                    { nameof(lowStockThreshold), new[] { "Threshold cannot be negative." } }
                });
        }

        try
        {
            // CRITICAL ARCHITECTURE RULE:
            // Await each database query sequentially because all operations share the current request's
            // scoped DbContext. Concurrent queries on one DbContext (e.g. Task.WhenAll) trigger EF Core
            // invalid concurrency errors.
            //
            // Each query uses CachedAsync with a 5-minute absolute expiration.
            // On cache miss, the AsyncDataFactory delegate executes the specific repository method.

            // 1. Average price per category
            var avgPrices = await _cache.CachedAsync(
                CacheKeys.ReportAveragePrice,
                async token => await _productRepository.GetAveragePriceByCategoryAsync(token),
                cancellationToken) ?? Array.Empty<CategoryAveragePriceDto>();

            // 2. Highest stock value category (supports ties)
            var highestValueCategories = await _cache.CachedAsync(
                CacheKeys.ReportHighestValue,
                async token => await _productRepository.GetHighestStockValueCategoriesAsync(token),
                cancellationToken) ?? Array.Empty<HighestStockValueCategoryDto>();

            // 3. Category inventory valuation
            var inventoryValuations = await _cache.CachedAsync(
                CacheKeys.ReportValuation,
                async token => await _productRepository.GetInventoryValuationByCategoryAsync(token),
                cancellationToken) ?? Array.Empty<CategoryInventoryValuationDto>();

            // 4. Low stock products (parameterized by threshold)
            var lowStockProducts = await _cache.CachedAsync(
                CacheKeys.ReportLowStock(lowStockThreshold),
                async token => await _productRepository.GetLowStockProductsAsync(lowStockThreshold, token),
                cancellationToken) ?? Array.Empty<LowStockProductDto>();

            // 5. Top five most expensive products
            var topExpensiveProducts = await _cache.CachedAsync(
                CacheKeys.ReportTopExpensive,
                async token => await _productRepository.GetTopFiveMostExpensiveProductsAsync(token),
                cancellationToken) ?? Array.Empty<TopExpensiveProductDto>();

            var dashboard = new ReportDashboardDto
            {
                AveragePrices = avgPrices,
                HighestValueCategories = highestValueCategories,
                InventoryValuations = inventoryValuations,
                LowStockProducts = lowStockProducts,
                TopExpensiveProducts = topExpensiveProducts,
                LowStockThreshold = lowStockThreshold
            };

            return OperationResult<ReportDashboardDto>.Success(dashboard);
        }
        catch (OperationCanceledException)
        {
            // Client disconnection propagated as cancellation — not an error
            throw;
        }
        catch (PersistenceException ex)
        {
            // If any stored procedure fails, return a clear failure rather than fabricated zeroes
            _logger.LogError(ex, "Failed to generate report dashboard for threshold {Threshold}.", lowStockThreshold);
            return OperationResult<ReportDashboardDto>.PersistenceFailure("A database error occurred while generating reports.");
        }
    }
}
