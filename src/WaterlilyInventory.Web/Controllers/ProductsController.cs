using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.DTOs.Products;
using WaterlilyInventory.Web.Services.Interfaces;

namespace WaterlilyInventory.Web.Controllers;

/// <summary>
/// Controller for product inventory page navigation and AJAX data endpoints.
/// Thin web boundary: delegates all business logic, validation, and caching to <see cref="IProductService"/>.
/// Never accesses DbContext, repositories, SQL, or cache directly.
/// </summary>
[Route("products")]
public class ProductsController : Controller
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService productService, ILogger<ProductsController> logger)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// GET /products
    /// Returns the main Product Inventory management page shell.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// GET /products/data
    /// Returns the entire product collection as JSON.
    /// </summary>
    [HttpGet("data")]
    public async Task<IActionResult> GetAllData(CancellationToken cancellationToken)
    {
        var result = await _productService.GetAllProductsAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /products/{id:int}/data
    /// Returns a single product by Id as JSON. Returns 404 if not found.
    /// </summary>
    [HttpGet("{id:int}/data")]
    public async Task<IActionResult> GetByIdData(int id, CancellationToken cancellationToken)
    {
        var result = await _productService.GetProductByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// POST /products
    /// Creates a new product from submitted form data.
    /// Returns 201 Created with Location header on success, or 400 on validation failure.
    /// </summary>
    [HttpPost("")]
    public async Task<IActionResult> Create([FromForm] CreateProductDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ModelState.ToValidationFailureResult(this);
        }

        var result = await _productService.CreateProductAsync(dto, cancellationToken);
        return result.ToActionResult(
            this,
            createdAtActionName: nameof(GetByIdData),
            routeValues: new { id = result.Data?.Id });
    }

    /// <summary>
    /// POST /products/{id:int}/update
    /// Updates an existing product from submitted form data.
    /// The route Id is authoritative. Returns 200 on success (including unchanged), 400, 404, or 409.
    /// </summary>
    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateProductDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ModelState.ToValidationFailureResult(this);
        }

        var result = await _productService.UpdateProductAsync(id, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// POST /products/{id:int}/delete
    /// Deletes a product by Id.
    /// Returns 200 OK with <see cref="DeleteProductResultDto"/> (never a 204 bodyless response).
    /// </summary>
    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _productService.DeleteProductAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /products/sample-csv
    /// Downloads a sample CSV template file for bulk product imports.
    /// Supports withDemoData=true (default) to include the reference test rows,
    /// or withDemoData=false for a blank clean template.
    /// </summary>
    [HttpGet("sample-csv")]
    public IActionResult GetSampleCsv([FromQuery] bool withDemoData = true)
    {
        string csvContent;
        string fileName;

        if (withDemoData)
        {
            csvContent = "Id,Name,Category,Price,Stock\r\n" +
                         "1001,Laptop Sleeve 15 Inch,Accessories,14.99,12\r\n" +
                         "1002,Wireless Mouse,Accessories,19.95,20\r\n" +
                         "1003,USB-C Cable,Accessories,7.50,25\r\n" +
                         "1004,Promotional Lanyard,Accessories,0.00,15\r\n" +
                         "1005,LED Monitor 24 Inch,Electronics,179.99,10\r\n" +
                         "1006,Mechanical Keyboard,Electronics,69.95,8\r\n" +
                         "1007,Bluetooth Speaker,Electronics,39.99,18\r\n" +
                         "1008,HD Webcam,Electronics,49.99,5\r\n" +
                         "1009,Ergonomic Office Chair,Office Furniture,149.99,14\r\n" +
                         "1010,Height-Adjustable Desk,Office Furniture,249.99,6\r\n" +
                         "1011,Filing Cabinet,Office Furniture,119.99,0\r\n" +
                         "1012,A4 Notebook,Stationary,3.99,22\r\n" +
                         "1013,Blue Ballpoint Pen,Stationary,0.75,25\r\n" +
                         "1014,Sticky Notes Pack,Stationary,2.49,9\r\n" +
                         "1015,Document Folder,Stationary,1.25,16\r\n" +
                         "1016,Wireless Mouse@Pro,Accessories,24.99,20\r\n" +
                         "1017,Desk Organizer,Kitchen,9.99,12\r\n" +
                         "1018,Portable SSD,Electronics,,10\r\n" +
                         "1019,Desk Mat,Accessories,14.999,15\r\n" +
                         "1020,Whiteboard Marker,Stationary,1.50,-5\r\n";
            fileName = "sample_products_with_data.csv";
        }
        else
        {
            csvContent = "Name,Category,Price,Stock\r\n" +
                         "Ergonomic Keyboard,Electronics,45.00,15\r\n" +
                         "Desk Lamp,Office Furniture,29.99,8\r\n";
            fileName = "products_template.csv";
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
        return File(bytes, "text/csv", fileName);
    }

    /// <summary>
    /// POST /products/bulk-preview
    /// Accepts an uploaded spreadsheet (.csv or .xlsx), extracts and validates all rows,
    /// and returns a preview separating acceptable and rejected records.
    /// </summary>
    [HttpPost("bulk-preview")]
    public async Task<IActionResult> BulkPreview(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            var emptyResult = OperationResult<BulkProductPreviewDto>.ValidationFailure("Please select a valid CSV or Excel file to upload.");
            return emptyResult.ToActionResult(this);
        }

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (ext != ".csv" && ext != ".xlsx" && ext != ".txt")
        {
            var invalidExtResult = OperationResult<BulkProductPreviewDto>.ValidationFailure("Unsupported file format. Please upload a .csv or .xlsx file.");
            return invalidExtResult.ToActionResult(this);
        }

        // Limit maximum upload size to 10MB
        if (file.Length > 10 * 1024 * 1024)
        {
            var sizeResult = OperationResult<BulkProductPreviewDto>.ValidationFailure("File size cannot exceed 10MB.");
            return sizeResult.ToActionResult(this);
        }

        await using var stream = file.OpenReadStream();
        var result = await _productService.PreviewBulkProductsAsync(stream, file.FileName, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// POST /products/bulk-import
    /// Persists approved valid product records to the database and invalidates catalog and report caches.
    /// </summary>
    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImport([FromBody] BulkProductImportRequestDto request, CancellationToken cancellationToken)
    {
        if (request == null || request.Products == null || request.Products.Count == 0)
        {
            var emptyResult = OperationResult<BulkProductImportResultDto>.ValidationFailure("No valid products were provided for import.");
            return emptyResult.ToActionResult(this);
        }

        var result = await _productService.ImportBulkProductsAsync(request.Products, cancellationToken);
        return result.ToActionResult(this);
    }
}

