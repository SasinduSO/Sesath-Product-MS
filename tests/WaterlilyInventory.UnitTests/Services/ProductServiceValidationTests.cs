using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterlilyInventory.UnitTests.Common;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Products;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Implementations;

namespace WaterlilyInventory.UnitTests.Services;

/// <summary>
/// Comprehensive data-driven validation matrix for product operations.
/// Exercises the real <see cref="ProductService"/> boundary validation across Name, Category,
/// Price, Stock, and Id constraints, verifying both invalid rejection and valid boundary acceptance.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("ServiceCrud")]
public sealed class ProductServiceValidationTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly Mock<IAppCache> _cacheMock = new(MockBehavior.Strict);

    // =========================================================================
    // 1. NAME VALIDATION MATRIX (Create & Update)
    // =========================================================================

    public static IEnumerable<object?[]> NameInvalidCases()
    {
        yield return new object?[] { null, "Product name is required and cannot be blank." };
        yield return new object?[] { "", "Product name is required and cannot be blank." };
        yield return new object?[] { "   ", "Product name is required and cannot be blank." };
        yield return new object?[] { new string('A', 151), "Product name cannot exceed 150 characters." };
        yield return new object?[] { "Invalid@Name#", "Product name cannot contain special characters." };
    }

    [TestMethod]
    [DynamicData(nameof(NameInvalidCases))]
    public async Task CreateProduct_InvalidName_ReturnsValidationFailure(string? name, string expectedMessageFragment)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidDto(name: name!);
        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNull(result.Data);
        Assert.IsNotNull(result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(Product.Name)));
        Assert.IsTrue(result.ValidationErrors[nameof(Product.Name)].Any(m => m.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase)));

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DynamicData(nameof(NameInvalidCases))]
    public async Task UpdateProduct_InvalidName_ReturnsValidationFailure(string? name, string expectedMessageFragment)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidUpdateDto(name: name!);
        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(101, dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNull(result.Data);
        Assert.IsNotNull(result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(Product.Name)));
        Assert.IsTrue(result.ValidationErrors[nameof(Product.Name)].Any(m => m.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase)));

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> NameValidBoundaryCases()
    {
        yield return new object[] { new string('A', 150), new string('A', 150) };
        yield return new object[] { "  Trimmed Product  ", "Trimmed Product" };
        yield return new object[] { "Item-123 with Spaces", "Item-123 with Spaces" };
    }

    [TestMethod]
    [DynamicData(nameof(NameValidBoundaryCases))]
    public async Task CreateProduct_ValidNameBoundaries_AcceptedAndTrimmed(string inputName, string expectedPersistedName)
    {
        // Arrange
        const int generatedId = 201;
        var dto = ProductTestBuilder.CreateValidDto(name: inputName);
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.Is<Product>(p => p.Name == expectedPersistedName),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.CreateSuccessResult(id: generatedId, name: expectedPersistedName));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(generatedId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expectedPersistedName, result.Data!.Name);

        _repositoryMock.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Name == expectedPersistedName), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(generatedId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(NameValidBoundaryCases))]
    public async Task UpdateProduct_ValidNameBoundaries_AcceptedAndTrimmed(string inputName, string expectedPersistedName)
    {
        // Arrange
        const int productId = 201;
        var dto = ProductTestBuilder.CreateValidUpdateDto(name: inputName);
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.UpdateSuccessResult(id: productId, name: expectedPersistedName));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(productId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(productId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expectedPersistedName, result.Data!.Name);

        _repositoryMock.Verify(r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(productId), Times.Once);
    }

    // =========================================================================
    // 2. CATEGORY VALIDATION MATRIX (Create & Update)
    // =========================================================================

    public static IEnumerable<object?[]> CategoryInvalidCases()
    {
        yield return new object?[] { null, "Category is required and cannot be blank." };
        yield return new object?[] { "", "Category is required and cannot be blank." };
        yield return new object?[] { "   ", "Category is required and cannot be blank." };
        yield return new object?[] { new string('C', 101), "Category cannot exceed 100 characters." };
        yield return new object?[] { "Unsupported Category", "Category must be one of the allowed categories" };
    }

    [TestMethod]
    [DynamicData(nameof(CategoryInvalidCases))]
    public async Task CreateProduct_InvalidCategory_ReturnsValidationFailure(string? category, string expectedMessageFragment)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidDto(category: category!);
        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNotNull(result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(Product.Category)));
        Assert.IsTrue(result.ValidationErrors[nameof(Product.Category)].Any(m => m.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase)));

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DynamicData(nameof(CategoryInvalidCases))]
    public async Task UpdateProduct_InvalidCategory_ReturnsValidationFailure(string? category, string expectedMessageFragment)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidUpdateDto(category: category!);
        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(101, dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNotNull(result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(Product.Category)));
        Assert.IsTrue(result.ValidationErrors[nameof(Product.Category)].Any(m => m.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase)));

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> CategoryValidCases()
    {
        yield return new object[] { "Electronics", "Electronics" };
        yield return new object[] { "  electronics  ", "Electronics" };
        yield return new object[] { "office furniture", "Office Furniture" };
        yield return new object[] { "Stationary", "Stationery" }; // Normalized alias
        yield return new object[] { "Accessories", "Accessories" };
    }

    [TestMethod]
    [DynamicData(nameof(CategoryValidCases))]
    public async Task CreateProduct_ValidCategoryNormalization_AcceptedAndStandardized(string inputCategory, string expectedNormalizedCategory)
    {
        // Arrange
        const int generatedId = 202;
        var dto = ProductTestBuilder.CreateValidDto(category: inputCategory);
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.Is<Product>(p => p.Category == expectedNormalizedCategory),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.CreateSuccessResult(id: generatedId, category: expectedNormalizedCategory));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(generatedId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expectedNormalizedCategory, result.Data!.Category);

        _repositoryMock.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Category == expectedNormalizedCategory), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(generatedId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(CategoryValidCases))]
    public async Task UpdateProduct_ValidCategoryNormalization_AcceptedAndStandardized(string inputCategory, string expectedNormalizedCategory)
    {
        // Arrange
        const int productId = 202;
        var dto = ProductTestBuilder.CreateValidUpdateDto(category: inputCategory);
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.UpdateSuccessResult(id: productId, category: expectedNormalizedCategory));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(productId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(productId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expectedNormalizedCategory, result.Data!.Category);

        _repositoryMock.Verify(r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(productId), Times.Once);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void NormalizeCategory_NullOrWhitespace_ReturnsEmptyString(string? input)
    {
        var result = ProductService.NormalizeCategory(input!);
        Assert.AreEqual(string.Empty, result);
    }

    // =========================================================================
    // 3. PRICE VALIDATION MATRIX (Create & Update)
    // =========================================================================

    public static IEnumerable<object?[]> PriceInvalidCases()
    {
        yield return new object?[] { null, "Price is required and must be a valid number without letters." };
        yield return new object?[] { -0.01m, "Price must be zero or positive." };
        yield return new object?[] { -150.00m, "Price must be zero or positive." };
        yield return new object?[] { 1.234m, "Price cannot have more than 2 decimal places." };
        yield return new object?[] { 0.001m, "Price cannot have more than 2 decimal places." };
        yield return new object?[] { 99.999m, "Price cannot have more than 2 decimal places." };
        yield return new object?[] { 1000000000000000.00m, "Price cannot exceed 999,999,999,999,999.99." };
    }

    [TestMethod]
    [DynamicData(nameof(PriceInvalidCases))]
    public async Task CreateProduct_InvalidPrice_ReturnsValidationFailure(decimal? price, string expectedMessageFragment)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidDto();
        dto.Price = price;

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNotNull(result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(Product.Price)));
        Assert.IsTrue(result.ValidationErrors[nameof(Product.Price)].Any(m => m.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase)));

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DynamicData(nameof(PriceInvalidCases))]
    public async Task UpdateProduct_InvalidPrice_ReturnsValidationFailure(decimal? price, string expectedMessageFragment)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        dto.Price = price;

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(101, dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNotNull(result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(Product.Price)));
        Assert.IsTrue(result.ValidationErrors[nameof(Product.Price)].Any(m => m.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase)));

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> PriceValidBoundaryCases()
    {
        yield return new object[] { 0.00m };             // Zero price is explicitly valid!
        yield return new object[] { 0.01m };             // Minimum positive penny
        yield return new object[] { 129.99m };           // Standard decimal
        yield return new object[] { 999999999999999.99m }; // Maximum decimal(18,2) boundary
    }

    [TestMethod]
    [DynamicData(nameof(PriceValidBoundaryCases))]
    public async Task CreateProduct_ValidPriceBoundaries_Accepted(decimal price)
    {
        // Arrange
        const int generatedId = 203;
        var dto = ProductTestBuilder.CreateValidDto(price: price);
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.Is<Product>(p => p.Price == price),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.CreateSuccessResult(id: generatedId, price: price));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(generatedId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, $"Price {price} should be valid.");
        Assert.AreEqual(price, result.Data!.Price);

        _repositoryMock.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Price == price), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(generatedId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(PriceValidBoundaryCases))]
    public async Task UpdateProduct_ValidPriceBoundaries_Accepted(decimal price)
    {
        // Arrange
        const int productId = 203;
        var dto = ProductTestBuilder.CreateValidUpdateDto(price: price);
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.UpdateSuccessResult(id: productId, price: price));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(productId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(productId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, $"Price {price} should be valid.");
        Assert.AreEqual(price, result.Data!.Price);

        _repositoryMock.Verify(r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(productId), Times.Once);
    }

    // =========================================================================
    // 4. STOCK VALIDATION MATRIX (Create & Update)
    // =========================================================================

    public static IEnumerable<object?[]> StockInvalidCases()
    {
        yield return new object?[] { null, "Stock quantity is required and must be a whole number without letters." };
        yield return new object?[] { -1, "Stock quantity must be zero or positive." };
        yield return new object?[] { -100, "Stock quantity must be zero or positive." };
    }

    [TestMethod]
    [DynamicData(nameof(StockInvalidCases))]
    public async Task CreateProduct_InvalidStock_ReturnsValidationFailure(int? stock, string expectedMessageFragment)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidDto();
        dto.Stock = stock;

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNotNull(result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(Product.Stock)));
        Assert.IsTrue(result.ValidationErrors[nameof(Product.Stock)].Any(m => m.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase)));

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DynamicData(nameof(StockInvalidCases))]
    public async Task UpdateProduct_InvalidStock_ReturnsValidationFailure(int? stock, string expectedMessageFragment)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        dto.Stock = stock;

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(101, dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNotNull(result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(Product.Stock)));
        Assert.IsTrue(result.ValidationErrors[nameof(Product.Stock)].Any(m => m.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase)));

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> StockValidBoundaryCases()
    {
        yield return new object[] { 0 };       // Zero stock is explicitly valid!
        yield return new object[] { 1 };
        yield return new object[] { 500 };
        yield return new object[] { 1000000 };
    }

    [TestMethod]
    [DynamicData(nameof(StockValidBoundaryCases))]
    public async Task CreateProduct_ValidStockBoundaries_Accepted(int stock)
    {
        // Arrange
        const int generatedId = 204;
        var dto = ProductTestBuilder.CreateValidDto(stock: stock);
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.Is<Product>(p => p.Stock == stock),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.CreateSuccessResult(id: generatedId, stock: stock));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(generatedId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, $"Stock {stock} should be valid.");
        Assert.AreEqual(stock, result.Data!.Stock);

        _repositoryMock.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Stock == stock), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(generatedId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(StockValidBoundaryCases))]
    public async Task UpdateProduct_ValidStockBoundaries_Accepted(int stock)
    {
        // Arrange
        const int productId = 204;
        var dto = ProductTestBuilder.CreateValidUpdateDto(stock: stock);
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.UpdateSuccessResult(id: productId, stock: stock));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(productId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(productId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, $"Stock {stock} should be valid.");
        Assert.AreEqual(stock, result.Data!.Stock);

        _repositoryMock.Verify(r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(productId), Times.Once);
    }

    // =========================================================================
    // 5. ID VALIDATION MATRIX (Read, Update, Delete)
    // =========================================================================

    public static IEnumerable<object[]> IdInvalidCases()
    {
        yield return new object[] { 0 };
        yield return new object[] { -1 };
        yield return new object[] { -999 };
    }

    [TestMethod]
    [DynamicData(nameof(IdInvalidCases))]
    public async Task GetProductById_InvalidId_ReturnsValidationFailure(int invalidId)
    {
        // Arrange
        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetProductByIdAsync(invalidId);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsTrue(result.Message?.Contains("Product ID must be greater than zero.", StringComparison.OrdinalIgnoreCase) ?? false);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DynamicData(nameof(IdInvalidCases))]
    public async Task UpdateProduct_InvalidId_ReturnsValidationFailure(int invalidId)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(invalidId, dto);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsTrue(result.Message?.Contains("Product ID must be greater than zero.", StringComparison.OrdinalIgnoreCase) ?? false);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DynamicData(nameof(IdInvalidCases))]
    public async Task DeleteProduct_InvalidId_ReturnsValidationFailure(int invalidId)
    {
        // Arrange
        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.DeleteProductAsync(invalidId);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsTrue(result.Message?.Contains("Product ID must be greater than zero.", StringComparison.OrdinalIgnoreCase) ?? false);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> IdValidBoundaryCases()
    {
        yield return new object[] { 1 };   // Smallest valid identity PK
        yield return new object[] { 101 }; // Typical positive ID
    }

    [TestMethod]
    [DynamicData(nameof(IdValidBoundaryCases))]
    public async Task GetProductById_ValidPositiveIdBoundary_Accepted(int validId)
    {
        // Arrange
        var expectedDto = ProductTestBuilder.CreateDto(id: validId);
        _cacheMock.SetupCachedLongHit(CacheKeys.ProductById(validId), expectedDto);

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetProductByIdAsync(validId);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(validId, result.Data!.Id);

        _repositoryMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DynamicData(nameof(IdValidBoundaryCases))]
    public async Task UpdateProduct_ValidPositiveIdBoundary_Accepted(int validId)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.UpdateAsync(validId, It.IsAny<Action<Product>>(), cancellationToken))
            .ReturnsAsync(ProductTestBuilder.UpdateSuccessResult(id: validId));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(validId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(validId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(validId, result.Data!.Id);

        _repositoryMock.Verify(r => r.UpdateAsync(validId, It.IsAny<Action<Product>>(), cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(validId), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(IdValidBoundaryCases))]
    public async Task DeleteProduct_ValidPositiveIdBoundary_Accepted(int validId)
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        _repositoryMock
            .Setup(r => r.DeleteAsync(validId, cancellationToken))
            .ReturnsAsync(ProductTestBuilder.DeleteSuccessResult(validId));

        _cacheMock.Setup(c => c.InvalidateProductAndReports(validId));

        var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

        // Act
        var result = await service.DeleteProductAsync(validId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(validId, result.Data!.DeletedId);

        _repositoryMock.Verify(r => r.DeleteAsync(validId, cancellationToken), Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(validId), Times.Once);
    }
}
