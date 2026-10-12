# WaterlilyInventory — Deferred Automated Testing Backlog (MSTest & Moq)

> **Status:** ⏸️ **DEFERRED (Required Next Development Stage)**  
> This document specifies the comprehensive suite of automated unit tests to be implemented in the designated testing phase using **MSTest** and **Moq**.  
> The application's interfaces, dependency injection setup, and operation result contracts were deliberately designed to ensure seamless testability without refactoring.

---

## Testing Strategy & Target Framework

- **Test Project**: `tests/WaterlilyInventory.Tests`
- **Framework**: .NET 10 LTS (`net10.0`)
- **Test Runner**: MSTest v3 (`MSTest.TestFramework`, `MSTest.TestAdapter`)
- **Mocking Library**: Moq (`Moq`)
- **Assertions**: Standard MSTest assertions or FluentAssertions
- **Scope**: Focus primarily on the **Service Layer** (`ProductService`, `ReportService`) and infrastructure abstractions (`AppCache`), validating business logic, cache boundaries, delegate callback invocations, error handling, and cancellation propagation without requiring a running SQL Server instance.

---

## 1. ProductService Unit Tests (`ProductServiceTests`)

### 1.1 Read Operations (`GetAllProductsAsync`, `GetProductByIdAsync`)

| Test Case | Scenario / Condition | Mock Setup | Expected Assertion |
|---|---|---|---|
| `GetAllProducts_CacheHit_ReturnsCachedList` | Products list exists in `IAppCache` | Mock `IAppCache.CachedAsync` returns pre-populated `IReadOnlyList<ProductDto>` | Returns `OperationResult.Success` with cached items; `IProductRepository.GetAllAsync` is **never** invoked. |
| `GetAllProducts_CacheMiss_InvokesRepositoryViaDelegate` | Products list not in cache; factory delegate invoked | Mock `IAppCache.CachedAsync` executes the passed `AsyncDataFactory<IReadOnlyList<ProductDto>>` | `IProductRepository.GetAllAsync` is called once; returns mapped `ProductDto` list. |
| `GetAllProducts_EmptyRepository_ReturnsSuccessfulEmptyList` | Database contains 0 products | Repository returns empty list | Returns `OperationResult.Success` with an empty collection (empty list is not an error). |
| `GetProductById_CacheHit_ReturnsCachedProduct` | Product detail exists in `IAppCache` | Mock `IAppCache.CachedLongAsync` returns cached `ProductDto` | Returns `OperationResult.Success`; Repository is **never** invoked. |
| `GetProductById_CacheMiss_ExistingProduct_ReturnsSuccess` | Cache miss; product found in repository | Factory delegate calls `IProductRepository.GetByIdAsync(id)`, returns entity | Returns `OperationResult.Success` with mapped `ProductDto`. |
| `GetProductById_NonExistent_ReturnsNotFound` | Cache miss; repository returns `null` | Repository `GetByIdAsync` returns `null` | Returns `OperationResult.Failure` with `ErrorCode.NotFound` and appropriate message. |
| `GetProductById_InvalidId_ZeroOrNegative_ReturnsValidationFailure` | Id parameter is `<= 0` | None (rejected before cache/repo call) | Returns `OperationResult.Failure` with `ErrorCode.Validation`; Repository is **never** called. |

### 1.2 Create Operation (`CreateProductAsync`)

| Test Case | Scenario / Condition | Mock Setup | Expected Assertion |
|---|---|---|---|
| `CreateProduct_ValidInput_ReturnsCreatedResultAndInvalidatesCache` | DTO contains valid Name, Category, Price, Stock | Repository `CreateAsync` returns `RepositoryWriteResult.Created` with new entity | Returns `OperationResult.Success` with created `ProductDto`; calls `IAppCache.InvalidateByPrefix(CacheKeys.ProductPrefix)` and `IAppCache.InvalidateByPrefix(CacheKeys.ReportPrefix)`. |
| `CreateProduct_BlankOrWhitespaceName_ReturnsValidationFailure` | `Name = "   "` or `""` | None | Returns `OperationResult.Failure` with `ErrorCode.Validation`; repository `CreateAsync` is **never** called. |
| `CreateProduct_NameExceeds150Chars_ReturnsValidationFailure` | `Name` length > 150 | None | Returns validation error for `Name`; repository is **never** called. |
| `CreateProduct_BlankCategory_ReturnsValidationFailure` | `Category = "   "` or `""` | None | Returns validation error for `Category`; repository is **never** called. |
| `CreateProduct_NegativePrice_ReturnsValidationFailure` | `Price = -10.00m` | None | Returns validation error for `Price`; repository is **never** called. |
| `CreateProduct_ExcessivePricePrecision_ReturnsValidationFailure` | `Price = 19.999m` (3 decimal places) | None | Returns validation error for `Price`; repository is **never** called. |
| `CreateProduct_NegativeStock_ReturnsValidationFailure` | `Stock = -5` | None | Returns validation error for `Stock`; repository is **never** called. |
| `CreateProduct_RepositoryThrowsPersistenceException_ReturnsFailure` | Repository write encounters database exception | Repository `CreateAsync` throws `PersistenceException` | Returns `OperationResult.Failure` with `ErrorCode.PersistenceFailure`; does not leak SQL exception. |

### 1.3 Update Operation (`UpdateProductAsync`)

| Test Case | Scenario / Condition | Mock Setup | Expected Assertion |
|---|---|---|---|
| `UpdateProduct_ValidChanges_ReturnsSuccessAndInvalidatesCache` | Values differ from current database state | Repository `UpdateAsync` returns `RepositoryWriteResult.Updated` with updated entity | Returns `OperationResult.Success`; invokes `IAppCache.InvalidateByPrefix` for both products and reports. |
| `UpdateProduct_IdenticalValues_ReturnsUnchangedWithoutInvalidation` | Name, Category, Price, Stock unchanged | Repository `UpdateAsync` returns `RepositoryWriteResult.Unchanged` | Returns `OperationResult.Success` with message `"No changes were needed."`; cache is **not** invalidated. |
| `UpdateProduct_NonExistentId_ReturnsNotFound` | Product does not exist in database | Repository `UpdateAsync` returns `RepositoryWriteResult.NotFound` | Returns `OperationResult.Failure` with `ErrorCode.NotFound`. |
| `UpdateProduct_ConcurrencyConflict_ReturnsConflictResult` | Concurrent modification by another transaction | Repository `UpdateAsync` returns `RepositoryWriteResult.Conflict` | Returns `OperationResult.Failure` with `ErrorCode.Conflict`. |
| `UpdateProduct_InvalidInput_ReturnsValidationFailureWithoutDatabaseCall` | Negative price, negative stock, or blank name | None | Returns `OperationResult.Failure` with `ErrorCode.Validation`; repository is **never** called. |

### 1.4 Delete Operation (`DeleteProductAsync`)

| Test Case | Scenario / Condition | Mock Setup | Expected Assertion |
|---|---|---|---|
| `DeleteProduct_ExistingId_ReturnsSuccessAndInvalidatesCache` | Product exists in database | Repository `DeleteAsync` returns `RepositoryWriteResult.Deleted` with deleted entity | Returns `OperationResult.Success` with `DeleteProductResultDto(id)`; invalidates both product and report cache scopes. |
| `DeleteProduct_NonExistentId_ReturnsNotFound` | Product does not exist in database | Repository `DeleteAsync` returns `RepositoryWriteResult.NotFound` | Returns `OperationResult.Failure` with `ErrorCode.NotFound`. |
| `DeleteProduct_InvalidId_ZeroOrNegative_ReturnsValidationFailure` | `id <= 0` | None | Returns validation error; repository is **never** called. |

---

## 2. ReportService Unit Tests (`ReportServiceTests`)

### 2.1 Dashboard Aggregation (`GetDashboardAsync`)

| Test Case | Scenario / Condition | Mock Setup | Expected Assertion |
|---|---|---|---|
| `GetDashboard_ValidThreshold_AggregatesAllFiveReports` | `lowStockThreshold = 10` | Mock `IAppCache.CachedAsync` executes factory delegates returning mock report DTOs for each procedure | Returns `ReportDashboardDto` containing populated lists for all 5 reports; verified sequential execution. |
| `GetDashboard_NegativeThreshold_ReturnsValidationFailure` | `lowStockThreshold = -1` | None | Returns `OperationResult.Failure` with `ErrorCode.Validation` and message `"Low-stock threshold must be zero or positive."`; no repository methods called. |
| `GetDashboard_ZeroThreshold_Succeeds` | `lowStockThreshold = 0` | Repository procedures return valid datasets | Returns `OperationResult.Success` with threshold 0. |
| `GetDashboard_SingleReportFailure_ReturnsFailureResult` | One repository procedure throws `PersistenceException` | One procedure mock throws exception | Returns clear failure result rather than fabricating zeroes. |
| `GetDashboard_PropagatesCancellationToken` | `CancellationToken` supplied | Mock verification | All repository procedure calls receive the exact same token. |

---

## 3. Caching & Delegate Unit Tests (`AppCacheTests`)

| Test Case | Scenario / Condition | Mock Setup | Expected Assertion |
|---|---|---|---|
| `CachedAsync_CacheMiss_InvokesFactoryDelegate` | Key not in `IMemoryCache` | Real `AppCache` with `MemoryCache` instance; mock delegate | Factory delegate is invoked exactly once; result is stored in cache with 5-minute absolute expiration. |
| `CachedAsync_CacheHit_DoesNotInvokeFactoryDelegate` | Key already in `IMemoryCache` | Pre-stored key in cache; mock delegate | Cached item returned; factory delegate is **never** invoked. |
| `CachedLongAsync_StoresWithNeverRemovePriority` | Key stored via `CachedLongAsync` | Real `AppCache` | Stored entry has `CacheItemPriority.NeverRemove` and no absolute expiration. |
| `InvalidateByPrefix_RemovesAllMatchingKeysAndAdvancesGeneration` | Multiple keys with prefix `product:` | Cache contains `product:list`, `product:id:1`, `report:avg-price` | `InvalidateByPrefix("product:")` removes both product keys while preserving `report:avg-price`; advances internal generation counter. |
| `StaleReadPrevention_ConcurrentInvalidationDiscardsSlowRead` | Invalidation occurs while factory delegate is running | Factory delegate triggers `InvalidateByPrefix` before returning data | Slow read data is **not** committed to cache because generation changed during loading. |

---

## 4. Cancellation & Concurrency Tests

| Test Case | Scenario / Condition | Expected Behavior |
|---|---|---|
| `ProductService_CancellationRequested_ThrowsOperationCanceledException` | Token cancelled prior to repository call | Method propagates `OperationCanceledException` cleanly without logging error. |
| `ReportService_SequentialExecution_DoesNotRunConcurrentDbContextQueries` | Sequential awaits on shared DbContext | Verifies that calls to `IProductRepository` for each of the 5 reports are awaited sequentially. |

---

## 5. Implementation Roadmap for Next Stage

1. **Create Test Project**:
   ```powershell
   dotnet new mstest -n WaterlilyInventory.Tests -o tests/WaterlilyInventory.Tests --framework net10.0
   dotnet sln WaterlilyInventory.slnx add tests/WaterlilyInventory.Tests/WaterlilyInventory.Tests.csproj
   ```
2. **Add Dependencies**:
   ```powershell
   dotnet add tests/WaterlilyInventory.Tests/WaterlilyInventory.Tests.csproj reference src/WaterlilyInventory.Web/WaterlilyInventory.Web.csproj
   dotnet add tests/WaterlilyInventory.Tests/WaterlilyInventory.Tests.csproj package Moq --version 4.20.72
   ```
3. **Organize Test Classes**:
   - `tests/WaterlilyInventory.Tests/Services/ProductServiceTests.cs`
   - `tests/WaterlilyInventory.Tests/Services/ReportServiceTests.cs`
   - `tests/WaterlilyInventory.Tests/Caching/AppCacheTests.cs`
4. **Execution & CI Validation**:
   ```powershell
   dotnet test tests/WaterlilyInventory.Tests/WaterlilyInventory.Tests.csproj --logger "console;verbosity=normal"
   ```
