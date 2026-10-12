namespace WaterlilyInventory.Web.DTOs.Products;

/// <summary>
/// Output DTO returned after a successful product deletion.
/// Contains the Id of the deleted product so the client can remove it from the UI.
/// </summary>
/// <param name="DeletedId">The Id of the product that was deleted.</param>
public sealed record DeleteProductResultDto(int DeletedId);
