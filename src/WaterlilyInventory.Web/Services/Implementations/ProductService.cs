using System.Globalization;
using Microsoft.Extensions.Logging;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Common.Exceptions;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Delegates;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Products;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Interfaces;

namespace WaterlilyInventory.Web.Services.Implementations;

/// <summary>
/// Service implementation for product CRUD workflows.
/// Owns business validation, category normalization, outcome mapping, and caching decisions.
/// Coordinates with <see cref="IProductRepository"/> for database access and <see cref="IAppCache"/> for caching.
/// Never references HttpContext, IActionResult, DbContext, SQL commands, or EF query APIs.
/// </summary>
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IAppCache _cache;
    private readonly ILogger<ProductService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ProductService"/>.
    /// Dependencies are injected via constructor with scoped/singleton lifetimes appropriately managed.
    /// </summary>
    public ProductService(
        IProductRepository productRepository,
        IAppCache cache,
        ILogger<ProductService> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<OperationResult<IReadOnlyList<ProductDto>>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Cached mode: 5-minute absolute expiration for product list queries.
            // An AsyncDataFactory<IReadOnlyList<ProductDto>> delegate callback is passed to IAppCache.
            // Purpose of delegate: separates the caching policy (5-min expiry, generation tracking)
            // from the actual data retrieval logic. The callback executes within the active request scope
            // ONLY upon a cache miss, ensuring repository/DbContext are not captured by singletons.
            var products = await _cache.CachedAsync(
                CacheKeys.ProductList,
                async token =>
                {
                    var entities = await _productRepository.GetAllAsync(token);
                    return (IReadOnlyList<ProductDto>)entities.Select(MapToDto).ToList().AsReadOnly();
                },
                cancellationToken);

            // An empty list is a successful read — never disguised as NotFound or an error
            return OperationResult<IReadOnlyList<ProductDto>>.Success(products ?? Array.Empty<ProductDto>());
        }
        catch (OperationCanceledException)
        {
            // Cancellation propagated without turning into an application error
            throw;
        }
        catch (PersistenceException ex)
        {
            // Diagnostic logging owned once by Service; returns safe failure without leaking DB internals
            _logger.LogError(ex, "Persistence error occurred while retrieving all products.");
            return OperationResult<IReadOnlyList<ProductDto>>.PersistenceFailure("A database error occurred while retrieving products.");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<ProductDto>> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return OperationResult<ProductDto>.ValidationFailure("Product ID must be greater than zero.");
        }

        try
        {
            // CachedLong mode: no time-based expiration for individual product details.
            // The AsyncDataFactory<ProductDto?> delegate callback loads the authoritative product by Id.
            // It runs only on a cache miss within the active request scope.
            // Missing products (null) are NOT cached by AppCache, preventing caching of 404 lookups.
            var product = await _cache.CachedLongAsync(
                CacheKeys.ProductById(id),
                async token =>
                {
                    var entity = await _productRepository.GetByIdAsync(id, token);
                    return entity is null ? null! : MapToDto(entity);
                },
                cancellationToken);

            if (product is null)
            {
                return OperationResult<ProductDto>.NotFound($"Product with ID {id} was not found.");
            }

            return OperationResult<ProductDto>.Success(product);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PersistenceException ex)
        {
            _logger.LogError(ex, "Persistence error occurred while retrieving product {ProductId}.", id);
            return OperationResult<ProductDto>.PersistenceFailure($"A database error occurred while retrieving product {id}.");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<ProductDto>> CreateProductAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Service-boundary validation ensures correctness independent of MVC action filters
        if (!ValidateProductInput(dto.Name, dto.Category, dto.Price, dto.Stock, out var validationErrors))
        {
            return OperationResult<ProductDto>.ValidationFailure("Validation failed.", validationErrors);
        }

        var trimmedName = dto.Name.Trim();
        var normalizedCategory = NormalizeCategory(dto.Category);

        // Map only permitted fields to domain entity
        var product = new Product
        {
            Name = trimmedName,
            Category = normalizedCategory,
            Price = dto.Price!.Value,
            Stock = dto.Stock!.Value
        };

        try
        {
            var writeResult = await _productRepository.CreateAsync(product, cancellationToken);

            if (writeResult.Outcome != WriteOutcome.Created || writeResult.Entity is null)
            {
                return OperationResult<ProductDto>.PersistenceFailure("Failed to create product in the database.");
            }

            var createdDto = MapToDto(writeResult.Entity);

            // Invalidate affected cache entries ONLY AFTER confirmed successful persistence commit
            TryInvalidateCache(createdDto.Id);

            return OperationResult<ProductDto>.Success(createdDto, "Product created successfully.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PersistenceException ex)
        {
            _logger.LogError(ex, "Persistence error occurred while creating product '{ProductName}'.", dto.Name);
            return OperationResult<ProductDto>.PersistenceFailure("A database error occurred while creating the product.");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            return OperationResult<ProductDto>.ValidationFailure("Product ID must be greater than zero.");
        }

        // Service-boundary validation
        if (!ValidateProductInput(dto.Name, dto.Category, dto.Price, dto.Stock, out var validationErrors))
        {
            return OperationResult<ProductDto>.ValidationFailure("Validation failed.", validationErrors);
        }

        var trimmedName = dto.Name.Trim();
        var normalizedCategory = NormalizeCategory(dto.Category);

        try
        {
            // Authoritative database read and update executed by repository
            var writeResult = await _productRepository.UpdateAsync(
                id,
                existing =>
                {
                    existing.Name = trimmedName;
                    existing.Category = normalizedCategory;
                    existing.Price = dto.Price!.Value;
                    existing.Stock = dto.Stock!.Value;
                },
                cancellationToken);

            switch (writeResult.Outcome)
            {
                case WriteOutcome.NotFound:
                    return OperationResult<ProductDto>.NotFound($"Product with ID {id} was not found.");

                case WriteOutcome.Unchanged:
                    // Return success for an unchanged update — no database write or cache invalidation was needed
                    _logger.LogInformation("Product {ProductId} update submitted with identical values. No changes made.", id);
                    return OperationResult<ProductDto>.Success(MapToDto(writeResult.Entity!), "No changes were needed.");

                case WriteOutcome.Updated:
                    var updatedDto = MapToDto(writeResult.Entity!);
                    // Invalidate affected cache entries ONLY AFTER confirmed successful persistence commit
                    TryInvalidateCache(id);
                    return OperationResult<ProductDto>.Success(updatedDto, "Product updated successfully.");

                case WriteOutcome.Conflict:
                    _logger.LogWarning("Concurrency conflict detected while updating product {ProductId}.", id);
                    return OperationResult<ProductDto>.Conflict($"A concurrency conflict occurred while updating product {id}. The record was modified by another operation.");

                default:
                    return OperationResult<ProductDto>.PersistenceFailure($"Unexpected outcome '{writeResult.Outcome}' while updating product {id}.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PersistenceException ex)
        {
            _logger.LogError(ex, "Persistence error occurred while updating product {ProductId}.", id);
            return OperationResult<ProductDto>.PersistenceFailure($"A database error occurred while updating product {id}.");
        }
    }

    /// <inheritdoc/>
    public async Task<OperationResult<DeleteProductResultDto>> DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return OperationResult<DeleteProductResultDto>.ValidationFailure("Product ID must be greater than zero.");
        }

        try
        {
            var writeResult = await _productRepository.DeleteAsync(id, cancellationToken);

            switch (writeResult.Outcome)
            {
                case WriteOutcome.NotFound:
                    return OperationResult<DeleteProductResultDto>.NotFound($"Product with ID {id} was not found.");

                case WriteOutcome.Deleted:
                    // Invalidate affected cache entries ONLY AFTER confirmed successful persistence commit
                    TryInvalidateCache(id);
                    return OperationResult<DeleteProductResultDto>.Success(new DeleteProductResultDto(id), "Product deleted successfully.");

                case WriteOutcome.Conflict:
                    _logger.LogWarning("Concurrency conflict detected while deleting product {ProductId}.", id);
                    return OperationResult<DeleteProductResultDto>.Conflict($"A concurrency conflict occurred while deleting product {id}.");

                default:
                    return OperationResult<DeleteProductResultDto>.PersistenceFailure($"Unexpected outcome '{writeResult.Outcome}' while deleting product {id}.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PersistenceException ex)
        {
            _logger.LogError(ex, "Persistence error occurred while deleting product {ProductId}.", id);
            return OperationResult<DeleteProductResultDto>.PersistenceFailure($"A database error occurred while deleting product {id}.");
        }
    }

    /// <summary>
    /// Category Normalization Policy:
    /// Trims leading and trailing whitespace, collapses multiple internal whitespace characters into single spaces,
    /// and standardizes to Title Case formatting (e.g., "  electronics  " -> "Electronics",
    /// "office furniture" -> "Office Furniture").
    /// This prevents casing/whitespace differences from causing duplicate or fragmented category
    /// groups in SQL Server stored procedure reports.
    /// Standardizes the "Stationary" alias to "Stationery" so existing database records and reports remain unified.
    /// </summary>
    public static string NormalizeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        var collapsed = string.Join(' ', category.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var titleCased = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(collapsed.ToLowerInvariant());

        if (string.Equals(titleCased, "Stationary", StringComparison.OrdinalIgnoreCase))
        {
            return "Stationery";
        }

        return titleCased;
    }

    /// <summary>
    /// Permitted product categories for inventory items.
    /// </summary>
    public static readonly string[] AllowedCategories =
    [
        "Accessories",
        "Electronics",
        "Office Furniture",
        "Stationery",
        "Stationary"
    ];

    /// <summary>
    /// Centralized input validation rules for product creation and modification.
    /// Matches the data annotations on DTOs and verifies boundary constraints.
    /// </summary>
    private static bool ValidateProductInput(
        string? name,
        string? category,
        decimal? price,
        int? stock,
        out IDictionary<string, string[]> errors)
    {
        var errorDict = new Dictionary<string, List<string>>();

        // Name validation: required, non-blank after trim, max 150 chars, no special characters
        if (string.IsNullOrWhiteSpace(name))
        {
            AddError(errorDict, nameof(Product.Name), "Product name is required and cannot be blank.");
        }
        else
        {
            var trimmed = name.Trim();
            if (trimmed.Length > 150)
            {
                AddError(errorDict, nameof(Product.Name), "Product name cannot exceed 150 characters.");
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[a-zA-Z0-9\s-]+$") ||
                     !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"[a-zA-Z0-9]"))
            {
                AddError(errorDict, nameof(Product.Name), "Product name cannot contain special characters. Only letters, numbers, spaces, and hyphens are allowed.");
            }
        }

        // Category validation: required, non-blank after trim, max 100 chars, must be in allowed categories
        if (string.IsNullOrWhiteSpace(category))
        {
            AddError(errorDict, nameof(Product.Category), "Category is required and cannot be blank.");
        }
        else
        {
            var trimmedCategory = category.Trim();
            if (trimmedCategory.Length > 100)
            {
                AddError(errorDict, nameof(Product.Category), "Category cannot exceed 100 characters.");
            }
            else if (!AllowedCategories.Any(c => string.Equals(c, trimmedCategory, StringComparison.OrdinalIgnoreCase)))
            {
                AddError(errorDict, nameof(Product.Category), "Category must be one of the allowed categories: Accessories, Electronics, Office Furniture, Stationary.");
            }
        }

        // Price validation: required, >= 0, reject excessive decimal precision (> 2 decimal places)
        if (!price.HasValue)
        {
            AddError(errorDict, nameof(Product.Price), "Price is required and must be a valid number without letters.");
        }
        else if (price.Value < 0)
        {
            AddError(errorDict, nameof(Product.Price), "Price must be zero or positive.");
        }
        else if (decimal.Round(price.Value, 2) != price.Value)
        {
            AddError(errorDict, nameof(Product.Price), "Price cannot have more than 2 decimal places.");
        }

        // Stock validation: required, >= 0
        if (!stock.HasValue)
        {
            AddError(errorDict, nameof(Product.Stock), "Stock quantity is required and must be a whole number without letters.");
        }
        else if (stock.Value < 0)
        {
            AddError(errorDict, nameof(Product.Stock), "Stock quantity must be zero or positive.");
        }

        errors = errorDict.ToDictionary(k => k.Key, v => v.Value.ToArray());
        return errors.Count == 0;
    }

    private static void AddError(Dictionary<string, List<string>> errorDict, string field, string message)
    {
        if (!errorDict.TryGetValue(field, out var list))
        {
            list = new List<string>();
            errorDict[field] = list;
        }
        list.Add(message);
    }

    /// <summary>
    /// Maps a domain entity to an immutable read-only ProductDto suitable for caching.
    /// Tracked EF entities never leak across the service boundary.
    /// </summary>
    private static ProductDto MapToDto(Product entity)
    {
        return new ProductDto(
            entity.Id,
            entity.Name,
            entity.Category,
            entity.Price,
            entity.Stock);
    }

    /// <summary>
    /// Executes cache invalidation for the shared product and report scope.
    /// Catches any secondary caching failure so it does not disguise a confirmed database commit.
    /// </summary>
    private void TryInvalidateCache(int? productId = null)
    {
        try
        {
            _cache.InvalidateProductAndReports(productId);
        }
        catch (Exception ex)
        {
            // Secondary maintenance failure logged — does not fail the primary persisted outcome
            _logger.LogError(ex, "Secondary cache invalidation failed after confirmed write for ProductId {ProductId}.", productId);
        }
    }
}
