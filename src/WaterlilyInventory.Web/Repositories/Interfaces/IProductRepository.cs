using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Reports;

namespace WaterlilyInventory.Web.Repositories.Interfaces;

/// <summary>
/// Product repository interface extending <see cref="IGenericRepository{Product}"/>.
/// Adds asynchronous methods for executing the five SQL Server stored-procedure reports.
/// Calculations execute in SQL stored procedures and map directly to typed DTOs.
/// </summary>
public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>
    /// Executes stored procedure <c>dbo.usp_Product_AveragePriceByCategory</c> to calculate
    /// the average price per category (AVG(Price) — not stock-weighted).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A read-only list of category average prices.</returns>
    Task<IReadOnlyList<CategoryAveragePriceDto>> GetAveragePriceByCategoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes stored procedure <c>dbo.usp_Product_HighestStockValueCategory</c> to identify
    /// the category or categories with the highest total monetary inventory value (SUM(Price * Stock)).
    /// Returns all tied leaders with deterministic ordering.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A read-only list of highest stock value categories.</returns>
    Task<IReadOnlyList<HighestStockValueCategoryDto>> GetHighestStockValueCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes stored procedure <c>dbo.usp_Product_InventoryValuationByCategory</c> to calculate
    /// total stock units and monetary inventory valuation for all categories.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A read-only list of category inventory valuations.</returns>
    Task<IReadOnlyList<CategoryInventoryValuationDto>> GetInventoryValuationByCategoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes stored procedure <c>dbo.usp_Product_LowStock</c> to retrieve products with stock
    /// strictly below the specified threshold (Stock &lt; @Threshold).
    /// </summary>
    /// <param name="threshold">Stock threshold. Products with Stock strictly below this are returned. Default is 10.</param>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A read-only list of products with stock below the threshold.</returns>
    Task<IReadOnlyList<LowStockProductDto>> GetLowStockProductsAsync(int threshold = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes stored procedure <c>dbo.usp_Product_TopFiveMostExpensive</c> to retrieve up to
    /// 5 products with the highest price, ordered by Price DESC then Id ASC.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the database operation.</param>
    /// <returns>A read-only list of up to five most expensive products.</returns>
    Task<IReadOnlyList<TopExpensiveProductDto>> GetTopFiveMostExpensiveProductsAsync(CancellationToken cancellationToken = default);
}
