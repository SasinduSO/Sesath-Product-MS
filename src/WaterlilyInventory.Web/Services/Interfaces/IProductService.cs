using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.DTOs.Products;

namespace WaterlilyInventory.Web.Services.Interfaces;

/// <summary>
/// Service interface for product CRUD operations.
/// All methods return <see cref="OperationResult{T}"/> and accept <see cref="CancellationToken"/>.
/// Services own business validation, caching decisions, and callback coordination.
/// Services never reference HttpContext, IActionResult, DbContext, SQL commands, or EF query APIs.
/// </summary>
public interface IProductService
{
    /// <summary>Returns all products as immutable DTOs. An empty list is a successful result.</summary>
    Task<OperationResult<IReadOnlyList<ProductDto>>> GetAllProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a single product by Id. A missing product is NotFound.</summary>
    Task<OperationResult<ProductDto>> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new product after Service-boundary validation. Returns the created ProductDto.</summary>
    Task<OperationResult<ProductDto>> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing product. Returns the updated ProductDto. Returns Unchanged message when no values differ.</summary>
    Task<OperationResult<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes a product by Id. Returns DeleteProductResultDto containing the deleted Id.</summary>
    Task<OperationResult<DeleteProductResultDto>> DeleteProductAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses an uploaded bulk product spreadsheet (CSV or Excel) and executes domain validation
    /// on every extracted row. Returns a preview segregating acceptable and rejected records without persisting.
    /// </summary>
    Task<OperationResult<BulkProductPreviewDto>> PreviewBulkProductsAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists approved valid products in bulk to the database within a single persistence transaction
    /// and invalidates the affected product catalog and report caches.
    /// </summary>
    Task<OperationResult<BulkProductImportResultDto>> ImportBulkProductsAsync(IEnumerable<CreateProductDto> products, CancellationToken cancellationToken = default);
}
