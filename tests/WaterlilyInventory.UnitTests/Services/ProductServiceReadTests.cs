using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WaterlilyInventory.UnitTests.Common;
using WaterlilyInventory.Web.Caching;
using WaterlilyInventory.Web.Common.Exceptions;
using WaterlilyInventory.Web.Common.Results;
using WaterlilyInventory.Web.Domain.Entities;
using WaterlilyInventory.Web.DTOs.Products;
using WaterlilyInventory.Web.Delegates;
using WaterlilyInventory.Web.Repositories.Interfaces;
using WaterlilyInventory.Web.Services.Implementations;

namespace WaterlilyInventory.UnitTests.Services;

/// <summary>
/// Unit tests for product read operations:
/// <list type="bullet">
///   <item><description><see cref="ProductService.GetAllProductsAsync"/> (Cached mode with 5-minute absolute expiration)</description></item>
///   <item><description><see cref="ProductService.GetProductByIdAsync"/> (CachedLong mode with no time-based expiration)</description></item>
/// </list>
/// Exercises the real <see cref="ProductService"/> implementation using strict Moq mocks
/// for repository and cache boundaries, with NullLogger.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("ServiceCrud")]
public sealed class ProductServiceReadTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly Mock<IAppCache> _cacheMock = new(MockBehavior.Strict);

    // =========================================================================
    // PRODUCT LIST READ TESTS (GetAllProductsAsync)
    // =========================================================================

    /// <summary>
    /// Verifies that on a cache miss, the service's supplied factory delegate is executed
    /// against the repository mock, and domain entities are accurately mapped to read-only DTOs.
    /// </summary>
    [TestMethod]
    public async Task GetAllProducts_CacheMiss_ExecutesFactoryAgainstRepositoryAndReturnsMappedDtos()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource().Token;
        var entities = ProductTestBuilder.CreateProductEntities();

        _cacheMock.SetupCachedMiss<IReadOnlyList<ProductDto>>(CacheKeys.ProductList);

        _repositoryMock
            .Setup(r => r.GetAllAsync(cancellationToken))
            .ReturnsAsync(entities);

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetAllProductsAsync(cancellationToken);

        // Assert
        Assert.IsNotNull(result, "Service must return a non-null OperationResult.");
        Assert.IsTrue(result.IsSuccess, "Successful read must return IsSuccess = true.");
        Assert.IsNull(result.ErrorCode, "Successful read must have null ErrorCode.");
        Assert.IsNotNull(result.Data, "Returned collection must not be null.");
        Assert.HasCount(2, result.Data, "Returned collection must contain 2 mapped DTOs.");

        // Exact decimal and field value assertions
        var first = result.Data[0];
        Assert.AreEqual(101, first.Id);
        Assert.AreEqual("Ergonomic Keyboard", first.Name);
        Assert.AreEqual("Electronics", first.Category);
        Assert.AreEqual(129.99m, first.Price);
        Assert.AreEqual(42, first.Stock);

        var second = result.Data[1];
        Assert.AreEqual(102, second.Id);
        Assert.AreEqual("Monitor Riser", second.Name);
        Assert.AreEqual("Office Furniture", second.Category);
        Assert.AreEqual(35.50m, second.Price);
        Assert.AreEqual(15, second.Stock);

        // Verify cache interaction used the 5-minute CachedAsync method with ProductList key
        _cacheMock.Verify(
            c => c.CachedAsync(
                CacheKeys.ProductList,
                It.IsAny<AsyncDataFactory<IReadOnlyList<ProductDto>>>(),
                cancellationToken),
            Times.Once);

        // Verify repository data query was executed once through the factory delegate
        _repositoryMock.Verify(r => r.GetAllAsync(cancellationToken), Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that an empty database returns IsSuccess = true with an empty collection.
    /// An empty list is a legitimate successful query — it must never be disguised as NotFound or an error.
    /// </summary>
    [TestMethod]
    public async Task GetAllProducts_EmptyRepository_ReturnsSuccessfulEmptyListNotNotFound()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource().Token;

        _cacheMock.SetupCachedMiss<IReadOnlyList<ProductDto>>(CacheKeys.ProductList);

        _repositoryMock
            .Setup(r => r.GetAllAsync(cancellationToken))
            .ReturnsAsync(Array.Empty<Product>());

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetAllProductsAsync(cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, "Empty product list must be reported as successful.");
        Assert.IsNull(result.ErrorCode, "ErrorCode must be null, never NotFound.");
        Assert.IsNotNull(result.Data, "Data must be a non-null empty list.");
        Assert.IsEmpty(result.Data, "Data must contain zero items.");

        _repositoryMock.Verify(r => r.GetAllAsync(cancellationToken), Times.Once);
        _cacheMock.Verify(
            c => c.CachedAsync(
                CacheKeys.ProductList,
                It.IsAny<AsyncDataFactory<IReadOnlyList<ProductDto>>>(),
                cancellationToken),
            Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that a documented repository failure throws PersistenceException, which the service
    /// translates to a safe PersistenceFailure result instead of fabricating an empty list.
    /// </summary>
    [TestMethod]
    public async Task GetAllProducts_RepositoryThrowsPersistenceException_ReturnsSafeFailureNotFabricatedEmptyList()
    {
        // Arrange
        const string diagnosticMarker = "OLEDB/TDS network link broken [0x80004005]";
        var cancellationToken = new CancellationTokenSource().Token;

        _cacheMock.SetupCachedMiss<IReadOnlyList<ProductDto>>(CacheKeys.ProductList);

        _repositoryMock
            .Setup(r => r.GetAllAsync(cancellationToken))
            .ThrowsAsync(new PersistenceException(diagnosticMarker));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetAllProductsAsync(cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess, "Database failure must return IsSuccess = false.");
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode, "ErrorCode must be PersistenceFailure.");
        Assert.IsNull(result.Data, "Failed query must not return a fabricated empty list.");
        Assert.IsNotNull(result.Message, "Safe human-readable failure message must be present.");

        // Internal marker suppressed
        Assert.IsFalse(
            result.Message.Contains(diagnosticMarker, StringComparison.OrdinalIgnoreCase),
            "Internal diagnostic marker must not be leaked in public Message.");

        _repositoryMock.Verify(r => r.GetAllAsync(cancellationToken), Times.Once);
        _cacheMock.Verify(
            c => c.CachedAsync(
                CacheKeys.ProductList,
                It.IsAny<AsyncDataFactory<IReadOnlyList<ProductDto>>>(),
                cancellationToken),
            Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that on a cache hit, the service returns the pre-populated cached DTO snapshot
    /// immediately without querying the repository.
    /// </summary>
    [TestMethod]
    public async Task GetAllProducts_CacheHit_ReturnsCachedSnapshotWithoutRepositoryAccess()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource().Token;
        var cachedSnapshot = ProductTestBuilder.CreateProductDtos();

        _cacheMock.SetupCachedHit(CacheKeys.ProductList, cachedSnapshot);

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetAllProductsAsync(cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(cachedSnapshot, result.Data, "Returned DTO collection must be the cached instance.");

        // Strict mock verifies repository was NEVER queried
        _repositoryMock.VerifyNoOtherCalls();

        _cacheMock.Verify(
            c => c.CachedAsync(
                CacheKeys.ProductList,
                It.IsAny<AsyncDataFactory<IReadOnlyList<ProductDto>>>(),
                cancellationToken),
            Times.Once);
        _cacheMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // INDIVIDUAL PRODUCT DETAIL READ TESTS (GetProductByIdAsync)
    // =========================================================================

    /// <summary>
    /// Verifies that looking up an existing product by Id executes the factory on cache miss,
    /// queries the repository with the exact Id, and returns a fully mapped DTO.
    /// </summary>
    [TestMethod]
    public async Task GetProductById_CacheMiss_ExistingProduct_ReturnsSuccessWithAllFields()
    {
        // Arrange
        const int productId = 101;
        var cancellationToken = new CancellationTokenSource().Token;
        var entity = ProductTestBuilder.CreateProduct(productId);

        _cacheMock.SetupCachedLongMiss<ProductDto>(CacheKeys.ProductById(productId));

        _repositoryMock
            .Setup(r => r.GetByIdAsync(productId, cancellationToken))
            .ReturnsAsync(entity);

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetProductByIdAsync(productId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.ErrorCode);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(productId, result.Data.Id);
        Assert.AreEqual(entity.Name, result.Data.Name);
        Assert.AreEqual(entity.Category, result.Data.Category);
        Assert.AreEqual(entity.Price, result.Data.Price);
        Assert.AreEqual(entity.Stock, result.Data.Stock);

        _repositoryMock.Verify(r => r.GetByIdAsync(productId, cancellationToken), Times.Once);
        _cacheMock.Verify(
            c => c.CachedLongAsync(
                CacheKeys.ProductById(productId),
                It.IsAny<AsyncDataFactory<ProductDto>>(),
                cancellationToken),
            Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that when the repository returns null for a requested Id, the service returns
    /// the approved NotFound result with ErrorCode = NotFound.
    /// </summary>
    [TestMethod]
    public async Task GetProductById_CacheMiss_MissingProduct_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 999;
        var cancellationToken = new CancellationTokenSource().Token;

        _cacheMock.SetupCachedLongMiss<ProductDto>(CacheKeys.ProductById(nonExistentId));

        _repositoryMock
            .Setup(r => r.GetByIdAsync(nonExistentId, cancellationToken))
            .ReturnsAsync((Product?)null);

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetProductByIdAsync(nonExistentId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess, "Missing product must return IsSuccess = false.");
        Assert.AreEqual(ErrorCodes.NotFound, result.ErrorCode, "ErrorCode must be NotFound.");
        Assert.IsNull(result.Data, "Missing product must not return data.");
        Assert.IsNotNull(result.Message);
        Assert.Contains(nonExistentId.ToString(), result.Message, "Message should mention the missing ID.");

        _repositoryMock.Verify(r => r.GetByIdAsync(nonExistentId, cancellationToken), Times.Once);
        _cacheMock.Verify(
            c => c.CachedLongAsync(
                CacheKeys.ProductById(nonExistentId),
                It.IsAny<AsyncDataFactory<ProductDto>>(),
                cancellationToken),
            Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that an invalid Id (zero or negative) fails validation before attempting any cache
    /// or repository data access.
    /// </summary>
    [TestMethod]
    [DataRow(0, DisplayName = "Id = 0")]
    [DataRow(-1, DisplayName = "Id = -1")]
    [DataRow(-999, DisplayName = "Id = -999")]
    public async Task GetProductById_InvalidId_ZeroOrNegative_ReturnsValidationFailureWithoutDataAccess(int invalidId)
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource().Token;

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetProductByIdAsync(invalidId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNull(result.Data);

        // Strict mocks verify zero calls were made to cache or repository
        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that an expected persistence exception during individual product retrieval
    /// follows the documented safe failure policy and does not expose internal diagnostics.
    /// </summary>
    [TestMethod]
    public async Task GetProductById_PersistenceException_ReturnsSafeFailureWithoutLeakage()
    {
        // Arrange
        const int productId = 101;
        const string diagnosticMarker = "Deadlock victim Process ID 52 killed by SQL Server engine";
        var cancellationToken = new CancellationTokenSource().Token;

        _cacheMock.SetupCachedLongMiss<ProductDto>(CacheKeys.ProductById(productId));

        _repositoryMock
            .Setup(r => r.GetByIdAsync(productId, cancellationToken))
            .ThrowsAsync(new PersistenceException(diagnosticMarker));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetProductByIdAsync(productId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode);
        Assert.IsNull(result.Data);

        // Internal diagnostic suppressed
        Assert.IsFalse(
            result.Message?.Contains(diagnosticMarker, StringComparison.OrdinalIgnoreCase) ?? false,
            "Internal diagnostic marker must not be leaked in public Message.");

        _repositoryMock.Verify(r => r.GetByIdAsync(productId, cancellationToken), Times.Once);
        _cacheMock.Verify(
            c => c.CachedLongAsync(
                CacheKeys.ProductById(productId),
                It.IsAny<AsyncDataFactory<ProductDto>>(),
                cancellationToken),
            Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that the service uses the long-lived cache method (CachedLongAsync) for individual
    /// product detail reads and specifies the Id-specific cache key, distinguishing it from the
    /// 5-minute expiring CachedAsync method used for product lists.
    /// </summary>
    [TestMethod]
    public async Task GetProductById_UsesLongLivedCacheModeAndIdSpecificKey()
    {
        // Arrange
        const int productId = 101;
        var cancellationToken = new CancellationTokenSource().Token;
        var cachedDto = new ProductDto(productId, "Ergonomic Keyboard", "Electronics", 129.99m, 42);

        // Arrange a cache hit directly on CachedLongAsync
        _cacheMock.SetupCachedLongHit(CacheKeys.ProductById(productId), cachedDto);

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.GetProductByIdAsync(productId, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(cachedDto, result.Data);

        // Verify that CachedLongAsync was specifically invoked with the Id-specific key
        _cacheMock.Verify(
            c => c.CachedLongAsync(
                CacheKeys.ProductById(productId),
                It.IsAny<AsyncDataFactory<ProductDto>>(),
                cancellationToken),
            Times.Once);

        // Verify that the short-lived CachedAsync method was NEVER invoked for detail reads
        _cacheMock.Verify(
            c => c.CachedAsync(
                It.IsAny<string>(),
                It.IsAny<AsyncDataFactory<It.IsAnyType>>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "Product detail queries must use CachedLongAsync, not CachedAsync.");

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that detail queries for distinct product Ids produce independent, isolated cache keys.
    /// </summary>
    [TestMethod]
    public async Task GetProductById_DifferentProductIds_ProduceIndependentCacheKeys()
    {
        // Arrange
        const int product1Id = 101;
        const int product2Id = 102;
        var dto1 = new ProductDto(product1Id, "Product One", "Electronics", 50m, 10);
        var dto2 = new ProductDto(product2Id, "Product Two", "Accessories", 20m, 5);

        _cacheMock.SetupCachedLongHit(CacheKeys.ProductById(product1Id), dto1);
        _cacheMock.SetupCachedLongHit(CacheKeys.ProductById(product2Id), dto2);

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result1 = await service.GetProductByIdAsync(product1Id);
        var result2 = await service.GetProductByIdAsync(product2Id);

        // Assert
        Assert.IsNotNull(result1);
        Assert.IsTrue(result1.IsSuccess);
        Assert.AreEqual(product1Id, result1.Data!.Id);

        Assert.IsNotNull(result2);
        Assert.IsTrue(result2.IsSuccess);
        Assert.AreEqual(product2Id, result2.Data!.Id);

        _cacheMock.Verify(
            c => c.CachedLongAsync(
                CacheKeys.ProductById(product1Id),
                It.IsAny<AsyncDataFactory<ProductDto>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheMock.Verify(
            c => c.CachedLongAsync(
                CacheKeys.ProductById(product2Id),
                It.IsAny<AsyncDataFactory<ProductDto>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }
}
