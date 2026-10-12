namespace WaterlilyInventory.Web.DTOs.Products;

/// <summary>
/// Data transfer object for approving and persisting bulk valid products to the database.
/// Contains the list of validated products confirmed by the user.
/// </summary>
public class BulkProductImportRequestDto
{
    /// <summary>
    /// List of validated product records to create in the database.
    /// </summary>
    public List<CreateProductDto> Products { get; set; } = new();
}
