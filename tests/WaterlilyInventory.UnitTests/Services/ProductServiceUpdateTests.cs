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
/// Unit tests for <see cref="ProductService.UpdateProductAsync"/>.
/// Exercises the real <see cref="ProductService"/> implementation using strict Moq mocks
/// for repository and cache boundaries, with NullLogger.
/// </summary>
[TestClass]
[TestCategory("Unit")]
[TestCategory("ServiceCrud")]
public sealed class ProductServiceUpdateTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly Mock<IAppCache> _cacheMock = new(MockBehavior.Strict);

    /// <summary>
    /// Verifies that submitting valid modified values results in a successful repository update,
    /// returns the updated DTO, preserves the authoritative route Id (does not change primary key),
    /// and invalidates the shared product and report cache scope.
    /// </summary>
    [TestMethod]
    public async Task UpdateProduct_ValidChanges_ReturnsSuccessAndInvalidatesCache()
    {
        // Arrange
        const int productId = 101;
        var dto = ProductTestBuilder.CreateValidUpdateDto(
            name: "Mechanical Keyboard Pro",
            category: "Electronics",
            price: 159.99m,
            stock: 35);
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .Callback<int, Action<Product>, CancellationToken>((id, applyChanges, _) =>
            {
                // Simulate authoritative entity loaded from database
                var existingEntity = ProductTestBuilder.CreateProduct(
                    id: id,
                    name: "Old Keyboard",
                    category: "Electronics",
                    price: 99.99m,
                    stock: 10);

                applyChanges(existingEntity);

                // Authoritative Id check: mutating property values must not alter the primary key
                Assert.AreEqual(productId, existingEntity.Id, "Input must not alter the authoritative primary key.");
                Assert.AreEqual(dto.Name, existingEntity.Name);
                Assert.AreEqual(dto.Category, existingEntity.Category);
                Assert.AreEqual(dto.Price!.Value, existingEntity.Price);
                Assert.AreEqual(dto.Stock!.Value, existingEntity.Stock);
            })
            .ReturnsAsync(RepositoryWriteResult<Product>.Updated(
                ProductTestBuilder.CreateProduct(
                    id: productId,
                    name: dto.Name,
                    category: dto.Category,
                    price: dto.Price!.Value,
                    stock: dto.Stock!.Value)));

        _cacheMock
            .Setup(c => c.InvalidateProductAndReports(productId));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(productId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, "Valid update must succeed.");
        Assert.IsNull(result.ErrorCode);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(productId, result.Data.Id);
        Assert.AreEqual(dto.Name, result.Data.Name);
        Assert.AreEqual(dto.Category, result.Data.Category);
        Assert.AreEqual(dto.Price!.Value, result.Data.Price);
        Assert.AreEqual(dto.Stock!.Value, result.Data.Stock);

        _repositoryMock.Verify(
            r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken),
            Times.Once);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(productId), Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that when the repository determines values are identical to current stored state,
    /// it returns WriteOutcome.Unchanged. The service reports success with "No changes were needed."
    /// and does NOT invalidate caches.
    /// </summary>
    [TestMethod]
    public async Task UpdateProduct_UnchangedValues_ReturnsSuccessWithoutInvalidation()
    {
        // Arrange
        const int productId = 101;
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        var cancellationToken = new CancellationTokenSource().Token;

        var existingEntity = ProductTestBuilder.CreateProduct(
            id: productId,
            name: dto.Name,
            category: dto.Category,
            price: dto.Price!.Value,
            stock: dto.Stock!.Value);

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .ReturnsAsync(RepositoryWriteResult<Product>.Unchanged(existingEntity));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(productId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, "Unchanged update must return success, not an error.");
        Assert.IsNull(result.ErrorCode);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(productId, result.Data.Id);
        Assert.IsNotNull(result.Message);
        Assert.Contains("No changes", result.Message);

        _repositoryMock.Verify(
            r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken),
            Times.Once);

        // Cache must NOT be invalidated when no database state changed
        _cacheMock.VerifyNoOtherCalls();
        _repositoryMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that updating a non-existent product returns NotFound without cache invalidation.
    /// </summary>
    [TestMethod]
    public async Task UpdateProduct_MissingProduct_ReturnsNotFoundWithoutInvalidation()
    {
        // Arrange
        const int nonExistentId = 999;
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                nonExistentId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .ReturnsAsync(RepositoryWriteResult<Product>.NotFoundResult());

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(nonExistentId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.NotFound, result.ErrorCode);
        Assert.IsNull(result.Data);

        _repositoryMock.Verify(
            r => r.UpdateAsync(nonExistentId, It.IsAny<Action<Product>>(), cancellationToken),
            Times.Once);

        _cacheMock.VerifyNoOtherCalls();
        _repositoryMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that representative invalid input (e.g. invalid Id or blank name) fails validation
    /// at the service boundary before contacting the repository or cache.
    /// </summary>
    [TestMethod]
    [DataRow(0, "Valid Name", DisplayName = "Id = 0")]
    [DataRow(-5, "Valid Name", DisplayName = "Id = -5")]
    [DataRow(101, "   ", DisplayName = "Blank Name")]
    public async Task UpdateProduct_InvalidInput_ReturnsValidationFailureWithoutDatabaseCallOrInvalidation(
        int id,
        string name)
    {
        // Arrange
        var dto = ProductTestBuilder.CreateValidUpdateDto(name: name);
        var cancellationToken = new CancellationTokenSource().Token;

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(id, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Validation, result.ErrorCode);
        Assert.IsNull(result.Data);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that a documented concurrency conflict outcome (e.g., DbUpdateConcurrencyException)
    /// maps specifically to Conflict (not success or generic NotFound), without cache invalidation.
    /// </summary>
    [TestMethod]
    public async Task UpdateProduct_ConcurrencyConflict_ReturnsConflictResultWithoutInvalidation()
    {
        // Arrange
        const int productId = 101;
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .ReturnsAsync(RepositoryWriteResult<Product>.ConflictResult());

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(productId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.Conflict, result.ErrorCode, "Outcome must be Conflict.");
        Assert.IsNull(result.Data);

        _repositoryMock.Verify(
            r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken),
            Times.Once);

        _cacheMock.VerifyNoOtherCalls();
        _repositoryMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies that an expected persistence exception during update maps to a safe PersistenceFailure
    /// without retrying the mutation or leaking database diagnostics.
    /// </summary>
    [TestMethod]
    public async Task UpdateProduct_PersistenceException_ReturnsSafeFailureWithoutRetryOrInvalidation()
    {
        // Arrange
        const int productId = 101;
        const string diagnosticMarker = "Deadlock encountered on index PK_Products_Id [Lock timeout 1222]";
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .ThrowsAsync(new PersistenceException(diagnosticMarker));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(productId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode);
        Assert.IsNull(result.Data);

        // Diagnostic suppression
        Assert.IsFalse(
            result.Message?.Contains(diagnosticMarker, StringComparison.OrdinalIgnoreCase) ?? false,
            "Internal diagnostic marker must not be leaked in public Message.");

        _repositoryMock.Verify(
            r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken),
            Times.Once);

        _cacheMock.VerifyNoOtherCalls();
        _repositoryMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Authoritative Outcomes: Verifies that write decisions use repository persistence outcomes
    /// rather than relying on cached detail data. If repository returns NotFound, the service
    /// reports NotFound even if cached data may have existed.
    /// </summary>
    [TestMethod]
    public async Task UpdateProduct_CachedDataExists_RejectsSuccessWhenRepositoryReportsNotFound()
    {
        // Arrange
        const int productId = 101;
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        var cancellationToken = new CancellationTokenSource().Token;

        // Repository authoritative decision: entity does not exist in database
        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .ReturnsAsync(RepositoryWriteResult<Product>.NotFoundResult());

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act
        var result = await service.UpdateProductAsync(productId, dto, cancellationToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess, "Must not report success based on cached assumptions.");
        Assert.AreEqual(ErrorCodes.NotFound, result.ErrorCode);
        Assert.IsNull(result.Data);

        _repositoryMock.Verify(
            r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken),
            Times.Once);
        _cacheMock.VerifyNoOtherCalls();
    }

    // =========================================================================
    // COMMIT ORDERING TESTS
    // =========================================================================

    /// <summary>
    /// Commit Ordering: Proves that cache invalidation cannot precede a successful write outcome.
    /// While the repository write is pending in-flight, invalidation must not have occurred.
    /// Once the write completes successfully, invalidation executes exactly once.
    /// </summary>
    [TestMethod]
    public async Task UpdateProduct_CommitOrdering_InvalidationCannotPrecedeSuccessfulWrite()
    {
        // Arrange
        const int productId = 101;
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        var cancellationToken = new CancellationTokenSource().Token;

        var writeEnteredSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeCompletionSource = new TaskCompletionSource<RepositoryWriteResult<Product>>(TaskCreationOptions.RunContinuationsAsynchronously);

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .Returns(async () =>
            {
                writeEnteredSignal.TrySetResult(true);
                return await writeCompletionSource.Task;
            });

        _cacheMock
            .Setup(c => c.InvalidateProductAndReports(productId));

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act - Start the update operation
        var serviceTask = service.UpdateProductAsync(productId, dto, cancellationToken);

        // Wait until repository write has been entered
        var entered = await writeEnteredSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(entered, "Service execution must reach the repository write.");

        // ASSERT: Invalidation must NOT precede the completed write
        _cacheMock.Verify(
            c => c.InvalidateProductAndReports(It.IsAny<int?>()),
            Times.Never,
            "Cache invalidation must never precede a successful persistence outcome.");

        // Complete the repository write successfully
        var updatedEntity = ProductTestBuilder.CreateProduct(productId, dto.Name, dto.Category, dto.Price!.Value, dto.Stock!.Value);
        writeCompletionSource.TrySetResult(RepositoryWriteResult<Product>.Updated(updatedEntity));

        // Await service completion
        var result = await serviceTask.WaitAsync(TimeSpan.FromSeconds(5));

        // ASSERT: Once write confirmed, success and invalidation are finalized
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        _cacheMock.Verify(c => c.InvalidateProductAndReports(productId), Times.Once);

        _repositoryMock.Verify(
            r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken),
            Times.Once);
        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Commit Ordering Failure Path: Proves that when an in-flight repository write fails or throws,
    /// cache invalidation is never invoked.
    /// </summary>
    [TestMethod]
    public async Task UpdateProduct_CommitOrdering_UnsuccessfulCompletionDoesNotInvalidate()
    {
        // Arrange
        const int productId = 101;
        var dto = ProductTestBuilder.CreateValidUpdateDto();
        var cancellationToken = new CancellationTokenSource().Token;

        var writeEnteredSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeCompletionSource = new TaskCompletionSource<RepositoryWriteResult<Product>>(TaskCreationOptions.RunContinuationsAsynchronously);

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                productId,
                It.IsAny<Action<Product>>(),
                cancellationToken))
            .Returns(async () =>
            {
                writeEnteredSignal.TrySetResult(true);
                return await writeCompletionSource.Task;
            });

        var service = new ProductService(
            _repositoryMock.Object,
            _cacheMock.Object,
            NullLogger<ProductService>.Instance);

        // Act - Start the update operation
        var serviceTask = service.UpdateProductAsync(productId, dto, cancellationToken);

        var entered = await writeEnteredSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(entered);

        // Complete write with an uncommitted failure (PersistenceException)
        writeCompletionSource.TrySetException(new PersistenceException("Disk I/O error during commit"));

        var result = await serviceTask.WaitAsync(TimeSpan.FromSeconds(5));

        // ASSERT: Result is failure, and cache was never invalidated
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ErrorCodes.PersistenceFailure, result.ErrorCode);

        _cacheMock.Verify(
            c => c.InvalidateProductAndReports(It.IsAny<int?>()),
            Times.Never,
            "Failed writes must never trigger cache invalidation.");

        _repositoryMock.Verify(
            r => r.UpdateAsync(productId, It.IsAny<Action<Product>>(), cancellationToken),
            Times.Once);

        _repositoryMock.VerifyNoOtherCalls();
        _cacheMock.VerifyNoOtherCalls();
    }
}
