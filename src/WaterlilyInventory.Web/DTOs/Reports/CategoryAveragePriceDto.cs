namespace WaterlilyInventory.Web.DTOs.Reports;

/// <summary>
/// Report DTO for average price per category.
/// Mapped from stored procedure <c>dbo.usp_Product_AveragePriceByCategory</c>.
/// Uses AVG(Price) — not stock-weighted. SQL aliases must match these property names exactly.
/// </summary>
/// <param name="Category">The product category name.</param>
/// <param name="AveragePrice">The average price of all products in this category.</param>
public sealed record CategoryAveragePriceDto(
    string Category,
    decimal AveragePrice
);
