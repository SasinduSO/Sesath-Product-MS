namespace WaterlilyInventory.Web.DTOs.Reports;

/// <summary>
/// Report DTO for the highest stock value category (or categories, if tied).
/// Mapped from stored procedure <c>dbo.usp_Product_HighestStockValueCategory</c>.
/// "Highest stock value" is the approved interpretation: greatest SUM(Price * Stock) per category.
/// All tied leaders are returned with deterministic ordering.
/// </summary>
/// <param name="Category">The product category name.</param>
/// <param name="TotalStockUnits">Total units across all products in this category (long to avoid int overflow in aggregation).</param>
/// <param name="InventoryValue">Total monetary inventory value: SUM(Price * Stock).</param>
public sealed record HighestStockValueCategoryDto(
    string Category,
    long TotalStockUnits,
    decimal InventoryValue
);
