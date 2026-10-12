namespace WaterlilyInventory.Web.DTOs.Reports;

/// <summary>
/// Aggregated report dashboard DTO containing all five report datasets.
/// Returned by <c>IReportService.GetDashboardAsync</c> and serialized as JSON for the report page.
/// Highest-value results are a collection because tied categories are supported.
/// </summary>
public sealed class ReportDashboardDto
{
    /// <summary>
    /// Average price per category. Each entry represents one category with its AVG(Price).
    /// </summary>
    public required IReadOnlyList<CategoryAveragePriceDto> AveragePrices { get; init; }

    /// <summary>
    /// Category (or categories) with the highest inventory value: SUM(Price * Stock).
    /// A collection because ties are returned — all categories sharing the maximum value.
    /// </summary>
    public required IReadOnlyList<HighestStockValueCategoryDto> HighestValueCategories { get; init; }

    /// <summary>
    /// Inventory valuation for every category, with total stock units and monetary value.
    /// </summary>
    public required IReadOnlyList<CategoryInventoryValuationDto> InventoryValuations { get; init; }

    /// <summary>
    /// Products with stock below the selected threshold.
    /// </summary>
    public required IReadOnlyList<LowStockProductDto> LowStockProducts { get; init; }

    /// <summary>
    /// Up to 5 most expensive products, ordered by Price DESC then Id ASC.
    /// </summary>
    public required IReadOnlyList<TopExpensiveProductDto> TopExpensiveProducts { get; init; }

    /// <summary>
    /// The low-stock threshold used for this report query.
    /// </summary>
    public required int LowStockThreshold { get; init; }
}
