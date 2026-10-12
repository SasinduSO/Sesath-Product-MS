namespace WaterlilyInventory.Web.DTOs.Reports;

/// <summary>
/// Report DTO for top 5 most expensive products.
/// Mapped from stored procedure <c>dbo.usp_Product_TopFiveMostExpensive</c>.
/// Returns TOP(5) ordered by Price DESC, Id ASC. May return fewer than 5 when fewer products exist.
/// </summary>
/// <param name="Id">Product identity key.</param>
/// <param name="Name">Product name.</param>
/// <param name="Category">Product category.</param>
/// <param name="Price">Product price.</param>
/// <param name="Stock">Current stock quantity.</param>
public sealed record TopExpensiveProductDto(
    int Id,
    string Name,
    string Category,
    decimal Price,
    int Stock
);
