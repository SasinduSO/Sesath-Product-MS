namespace WaterlilyInventory.Web.DTOs.Reports;

/// <summary>
/// Report DTO for inventory valuation by category.
/// Mapped from stored procedure <c>dbo.usp_Product_InventoryValuationByCategory</c>.
/// Returns SUM(Price * Stock) for every category, with total stock units alongside.
/// </summary>
/// <param name="Category">The product category name.</param>
/// <param name="TotalStockUnits">Total units in this category (long to avoid int overflow).</param>
/// <param name="InventoryValue">Total monetary value: SUM(Price * Stock).</param>
public sealed record CategoryInventoryValuationDto(
    string Category,
    long TotalStockUnits,
    decimal InventoryValue
);
