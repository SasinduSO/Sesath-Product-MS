using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterlilyInventory.UnitTests.Common;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Common.Exceptions;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Products;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Implementations;

namespace WaterlilyInventory.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="ProductService.CreateProductAsync"/>.
/// Exercises the real <see cref="ProductService"/> implementation using strict Moq mocks
/// for repository and cache boundaries, with NullLogger.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("ServiceCrud")]
public sealed class ProductServiceCreateTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly Mock<IAppCache> _cacheMock = new(MockBehavior.Strict);

    /// <summary>
    /// Verifies that valid creation input results in a successful Repository write with mapped entity,
    /// returns an <see cref="OperationResult{ProductDto}"/> with the generated identity ID,
    /// and triggers cache invalidation for the shared product and report scope.
    /// </summary>
    [TestMethod]
    public async Task CreateProduct_ValidInput_ReturnsSuccessAndInvalidatesCache()
    {
        // Arrange
        const int generatedId = 101;
        var dto = ProductTestBuilder.CreateValidDto();
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.Is<Product>(p =>
                    p.Name == dto.Name &&
                    p.Category == dto.Category &&
                    p.Price == dto.Price!.Value &&
                    p.Stock == dto.Stock!.Value),
                cancellationToken))
            .ReturnsAsync(ProductTestBuilder.CreateSuccessResult(
                id: generatedId,
                name: dto.Name,
                category: dto.Category,
                price: dto.Price!.Value,
                stock: dto.Stock!.Value));

        _cacheMock
            .Setup(c => c.InvalidateProductAndReports(generatedId));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result, "Service must return a non-null OperationResult.");
        Assert.IsTrue(result.IsSuccess, "Creation with valid data must return IsSuccess = true.");
        Assert.IsNull(result.ErrorCode, "Successful operation must have null ErrorCode.");
        Assert.IsNull(result.ValidationErrors, "Successful operation must have null ValidationErrors.");
        Assert.IsNotNull(result.Data, "Successful operation must contain the created ProductDto.");

        Assert.AreEqual(generatedId, result.Data.Id, "Returned DTO must contain the generated identity key.");
        Assert.AreEqual(dto.Name, result.Data.Name, "Returned DTO must preserve the product name.");
        Assert.AreEqual(dto.Category, result.Data.Category, "Returned DTO must preserve the normalized category.");
        Assert.AreEqual(dto.Price!.Value, result.Data.Price, "Returned DTO must preserve the price value.");
        Assert.AreEqual(dto.Stock!.Value, result.Data.Stock, "Returned DTO must preserve the stock quantity.");

        // Verify that the repository write occurred exactly once with the intended parameters
        _repositoryMock.Verify(
            r => r.CreateAsync(
                It.Is<Product>(p =>
                    p.Name == dto.Name &&
                    p.Category == dto.Category &&
                    p.Price == dto.Price!.Value &&
                    p.Stock == dto.Stock!.Value),
                cancellationToken),
            Times.Once);

        // Verify that cache invalidation occurred exactly once for the persisted ID
        _cacheMock.Verify(c => c.InvalidateProductAndReports(generatedId), Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that representative invalid input (such as blank name) returns a validation failure
    /// at the service boundary without calling repository write or cache invalidation.
    /// </summary>
    [TestMethod]
    public async Task CreateProduct_BlankName_ReturnsValidationFailureWithoutRepositoryWriteOrInvalidation()
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidDto(name: "   ");
        var cancellationToken = new CancellationTokenSource().Token;

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess, "Creation with blank name must fail validation.");
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode, "ErrorCode must be Validation.");
        Assert.IsNull(result.Data, "Failed validation must not return data.");
        Assert.IsNotNull(result.ValidationErrors, "Validation errors dictionary must be populated.");
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(Product.Name)), "Validation errors must target the Name field.");

        // Strict mocks verify zero repository writes and zero cache invalidations occurred
        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that an expected persistence exception during creation returns the documented safe
    /// failure result without retrying the write, invalidating caches, or leaking internal diagnostics.
    /// </summary>
    [TestMethod]
    public async Task CreateProduct_PersistenceException_ReturnsSafeFailureWithoutInvalidationOrRetry()
    {
        // Arrange
        const string diagnosticMarker = "SQL Server connection timeout [0x80004005]: Cluster node 10.0.0.1 unreachable";
        var dto = ProductTestBuilder.CreateValidDto();
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Product>(), cancellationToken))
            .ThrowsAsync(new PersistenceException(diagnosticMarker));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess, "Persistence error must yield IsSuccess = false.");
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode, "ErrorCode must be PersistenceFailure.");
        Assert.IsNull(result.Data, "Failed operation must not return data.");
        Assert.IsNotNull(result.Message, "Safe human-readable failure message must be provided.");

        // Diagnostic suppression: internal marker must NOT appear in public result properties
        Assert.IsFalse(
            result.Message!.Contains(diagnosticMarker, StringComparison.OrdinalIgnoreCase),
            "Internal diagnostic marker must not be leaked in public Message.");
        Assert.IsFalse(
            result.ErrorCode!.Contains(diagnosticMarker, StringComparison.OrdinalIgnoreCase),
            "Internal diagnostic marker must not be leaked in ErrorCode.");

        // Verify single write attempt — service must not attempt an uncoordinated retry
        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<Product>(), cancellationToken), Times.Once);

        // Invalidation must not occur on failed write
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that when the repository write returns an unexpected non-Created outcome (e.g., Conflict),
    /// the service maps it to PersistenceFailure without invalidating caches.
    /// </summary>
    [TestMethod]
    public async Task CreateProduct_RepositoryReturnsFailureOutcome_ReturnsPersistenceFailureWithoutInvalidation()
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidDto();
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Product>(), cancellationToken))
            .ReturnsAsync(RepositoryWriteResult<Product>.ConflictResult());

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.CreateProductAsync(dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess, "Non-Created repository outcome must yield failure.");
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode, "ErrorCode must be PersistenceFailure.");
        Assert.IsNull(result.Data);

        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<Product>(), cancellationToken), Times.Once);
        _cacheMock.VerifyNoOtherCalls();
    }
}
