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
}
