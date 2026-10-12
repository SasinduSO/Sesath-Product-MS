using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Products;

namespace WaterlilyInventory.UnitTests.Common;

/// <summary>
/// Test data builders creating fresh, deterministic input DTOs, entities, and results.
/// Clones mutable objects per test to prevent shared-state side effects.
/// </summary>
public static class ProductTestBuilder
{
    public const int DefaultProductId = 101;
    public const string DefaultName = "Ergonomic Keyboard";
    public const string DefaultCategory = "Electronics";
    public const decimal DefaultPrice = 129.99m;
    public const int DefaultStock = 42;

    /// <summary>
    /// Creates a fresh valid <see cref="CreateProductDto"/> with fixed deterministic values.
    /// </summary>
    public static CreateProductDto CreateValidDto(
        string name = DefaultName,
        string category = DefaultCategory,
        decimal price = DefaultPrice,
        int stock = DefaultStock) => new()
    {
        Name = name,
        Category = category,
        Price = price,
        Stock = stock
    };

    /// <summary>
    /// Creates a fresh valid <see cref="UpdateProductDto"/> with fixed deterministic values.
    /// </summary>
    public static UpdateProductDto CreateValidUpdateDto(
        string name = "Updated Keyboard",
        string category = "Electronics",
        decimal price = 149.99m,
        int stock = 50) => new()
    {
        Name = name,
        Category = category,
        Price = price,
        Stock = stock
    };

    /// <summary>
    /// Creates a fresh valid <see cref="Product"/> domain entity with fixed values.
    /// </summary>
    public static Product CreateProduct(
        int id = DefaultProductId,
        string name = DefaultName,
        string category = DefaultCategory,
        decimal price = DefaultPrice,
        int stock = DefaultStock) => new()
    {
        Id = id,
        Name = name,
        Category = category,
        Price = price,
        Stock = stock
    };

    /// <summary>
    /// Creates a deterministic collection of domain entities for list queries.
    /// </summary>
    public static IReadOnlyList<Product> CreateProductEntities() =>
    [
        CreateProduct(101, "Ergonomic Keyboard", "Electronics", 129.99m, 42),
        CreateProduct(102, "Monitor Riser", "Office Furniture", 35.50m, 15)
    ];

    /// <summary>
    /// Creates a deterministic collection of output DTOs representing cached snapshots.
    /// </summary>
    public static IReadOnlyList<ProductDto> CreateProductDtos() =>
    [
        new ProductDto(101, "Ergonomic Keyboard", "Electronics", 129.99m, 42),
        new ProductDto(102, "Monitor Riser", "Office Furniture", 35.50m, 15)
    ];

    /// <summary>
    /// Creates a single output <see cref="ProductDto"/>.
    /// </summary>
    public static ProductDto CreateDto(
        int id = DefaultProductId,
        string name = DefaultName,
        string category = DefaultCategory,
        decimal price = DefaultPrice,
        int stock = DefaultStock) => new(id, name, category, price, stock);

    /// <summary>
    /// Creates a successful <see cref="RepositoryWriteResult{Product}"/> with <see cref="WriteOutcome.Created"/>.
    /// </summary>
    public static RepositoryWriteResult<Product> CreateSuccessResult(
        int id = DefaultProductId,
        string name = DefaultName,
        string category = DefaultCategory,
        decimal price = DefaultPrice,
        int stock = DefaultStock)
    {
        var entity = CreateProduct(id, name, category, price, stock);
        return RepositoryWriteResult<Product>.Created(entity);
    }

    /// <summary>
    /// Creates a successful <see cref="RepositoryWriteResult{Product}"/> with <see cref="WriteOutcome.Updated"/>.
    /// </summary>
    public static RepositoryWriteResult<Product> UpdateSuccessResult(
        int id = DefaultProductId,
        string name = DefaultName,
        string category = DefaultCategory,
        decimal price = DefaultPrice,
        int stock = DefaultStock)
    {
        var entity = CreateProduct(id, name, category, price, stock);
        return RepositoryWriteResult<Product>.Updated(entity);
    }

    /// <summary>
    /// Creates a successful <see cref="RepositoryWriteResult{Product}"/> with <see cref="WriteOutcome.Deleted"/>.
    /// </summary>
    public static RepositoryWriteResult<Product> DeleteSuccessResult(
        int id = DefaultProductId,
        string name = DefaultName,
        string category = DefaultCategory,
        decimal price = DefaultPrice,
        int stock = DefaultStock)
    {
        var entity = CreateProduct(id, name, category, price, stock);
        return RepositoryWriteResult<Product>.Deleted(entity);
    }
}
