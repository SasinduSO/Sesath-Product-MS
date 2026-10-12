namespace WaterlilyInventory.Web.DTOs.Reports;

/// <summary>
/// Report DTO for products below a stock threshold.
/// Mapped from stored procedure <c>dbo.usp_Product_LowStock</c>.
/// Includes products where Stock &lt; @Threshold. Stock equal to threshold is NOT low stock.
/// </summary>
/// <param name="Id">Product identity key.</param>
/// <param name="Name">Product name.</param>
/// <param name="Category">Product category.</param>
/// <param name="Price">Product price.</param>
/// <param name="Stock">Current stock quantity (below the threshold).</param>
public sealed record LowStockProductDto(
    int Id,
    string Name,
    string Category,
    decimal Price,
    int Stock
);
