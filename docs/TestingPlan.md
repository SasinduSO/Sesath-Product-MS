# WaterlilyInventory — Testing Plan & Contract Map

> **Status:** Active (Authorized for Stages 7A–7F)  
> **Target Framework:** .NET 10 LTS (`net10.0`)  
> **Testing Stack:** MSTest.Sdk, Microsoft.Testing.Platform, Moq  
> **Scope:** Service-layer unit tests (`ProductService`, `ReportService`), in-memory cache component tests (`AppCache`).

---

## 1. Architectural Scope & Unit-Test Boundaries

The unit-testing phase strictly enforces the boundary between service-level orchestration and external infrastructure:

| Component Under Test | Tested Dependencies | Mocked / Controlled Dependencies | Excluded / Out of Scope for Unit Tests |
|---|---|---|---|
| **ProductService** | Real `ProductService` logic, business validation, DTO mapping, cache coordination | Mocked `IProductRepository`, Mocked `IAppCache`, `NullLogger<ProductService>` | SQL Server, EF Core DbContext, HTTP/Controller layer, browser UI |
| **ReportService** | Real `ReportService` aggregation logic, threshold validation, sequential execution | Mocked `IProductRepository`, Mocked `IAppCache`, `NullLogger<ReportService>` | SQL Server stored procedure execution, EF Core DbContext, Chart.js, HTTP/Controller layer |
| **AppCache** (Component Tests) | Real `AppCache` instance, in-process `IMemoryCache`, real generation tracking | Test delegate factories, controlled time/clocks | SQL Server, repositories, network, persistent storage |

### Separation of Responsibilities

- **Service Results vs. HTTP Status Codes**: Services return domain-level `OperationResult<T>` with `ErrorCode` strings (`Validation`, `NotFound`, `Conflict`, `PersistenceFailure`). Mapping to HTTP 200, 201, 400, 404, 409, 500 happens exclusively in MVC Controllers.
- **Service Orchestration vs. Stored-Procedure Mathematics**: Service unit tests verify that repository methods are invoked with correct parameters and sequential order, and that returned data is mapped properly into DTOs. They do **not** prove the mathematical correctness of SQL Server procedures (`AVG(Price)`, `SUM(Price * Stock)`, `TOP(5)`). Procedure correctness is proven by SQL/database integration tests and migrations.
- **Client Disconnection / Cancellation**: The Services propagate `OperationCanceledException` directly when `CancellationToken` is triggered.

---

## 2. Core Approved Semantics Matrix

| Semantic Rule | Source Implementation Behavior | Test Expectation |
|---|---|---|
| **Zero Price & Zero Stock** | `price.Value < 0` and `stock.Value < 0` are rejected; `0.00m` and `0` are accepted. | Boundary test verifies `Price = 0.00m` and `Stock = 0` succeed without validation failure. |
| **Blank Fields Invalid** | `string.IsNullOrWhiteSpace` check on `Name` and `Category`. | Tests verify empty string `""` and whitespace `"   "` return `ErrorCode.Validation`. |
| **Missing Products are NotFound** | `GetByIdAsync`, `UpdateAsync`, `DeleteAsync` return `WriteOutcome.NotFound` / `null` -> `OperationResult.NotFound`. | Returns `OperationResult<T>.NotFound` with `ErrorCode.NotFound`; no cache invalidation. |
| **Empty List is Successful** | `GetAllProductsAsync` returns `OperationResult.Success` with `IReadOnlyList<ProductDto>` (empty list). | Assert `IsSuccess == true`, `Data.Count == 0`, never `NotFound`. |
| **Unchanged Update Succeeds** | `UpdateAsync` with matching values returns `WriteOutcome.Unchanged` -> `OperationResult.Success` with `"No changes were needed."`. | Assert `IsSuccess == true`, message contains `"No changes"`, **no** cache invalidation performed. |
| **Failed Writes Do Not Invalidate Cache** | Invalidation occurs strictly following confirmed `WriteOutcome.Created`, `Updated`, or `Deleted`. | On `NotFound`, `Conflict`, validation failure, or exception, assert `_cache.InvalidateProductAndReports` was **never** called. |
| **Committed Writes Remain Committed on Cache Failure** | `TryInvalidateCache` catches exceptions from `_cache.InvalidateProductAndReports` and logs warning. | Service returns `IsSuccess == true` with persisted DTO even if `InvalidateProductAndReports` throws. |
| **Expected Persistence Failures Safe** | `catch (PersistenceException ex)` returns `OperationResult.PersistenceFailure` with generic message. | Assert `ErrorCode.PersistenceFailure`, message does not leak internal DB/SQL diagnostics. |
| **Cancellation Propagation** | `catch (OperationCanceledException) { throw; }`. | Awaited method throws `OperationCanceledException` cleanly; no partial commit or invalidation. |
| **Unexpected Defects Escape** | General exceptions (e.g., `NullReferenceException`) are not swallowed by the Service. | Unhandled exceptions propagate out to `GlobalExceptionHandlingMiddleware`. |

---

## 3. Service Contract Map

### 3.1 ProductService Contract Map

| Operation | Input Types | Output Type | Dependency Interactions | Expected Outcomes | Test Categories |
|---|---|---|---|---|---|
| **CreateProductAsync** | `CreateProductDto`, `CancellationToken` | `OperationResult<ProductDto>` | - Validates input fields<br>- Calls `IProductRepository.CreateAsync`<br>- Calls `IAppCache.InvalidateProductAndReports` on success | - Valid: `Success`, mapped `ProductDto`, ID assigned, cache invalidated<br>- Invalid input: `ValidationFailure`, repo not called<br>- Persistence error: `PersistenceFailure`, safe message, cache not invalidated | `Unit`, `ServiceCrud`, `Create` |
| **GetAllProductsAsync** | `CancellationToken` | `OperationResult<IReadOnlyList<ProductDto>>` | - Calls `IAppCache.CachedAsync(CacheKeys.ProductList, ...)`<br>- On miss: delegate calls `IProductRepository.GetAllAsync` | - Cache Hit: returns cached DTOs, repo not called<br>- Cache Miss: invokes factory, maps entities to DTOs<br>- Empty DB: returns `Success` with empty list | `Unit`, `ServiceCrud`, `Read` |
| **GetProductByIdAsync** | `int id`, `CancellationToken` | `OperationResult<ProductDto>` | - Validates `id > 0`<br>- Calls `IAppCache.CachedLongAsync(CacheKeys.ProductById(id), ...)`<br>- On miss: delegate calls `IProductRepository.GetByIdAsync` | - Valid ID, found: `Success` with mapped `ProductDto`<br>- Missing ID: `NotFound`<br>- ID <= 0: `ValidationFailure`<br>- Null returned by factory: not cached in AppCache | `Unit`, `ServiceCrud`, `Read` |
| **UpdateProductAsync** | `int id`, `UpdateProductDto`, `CancellationToken` | `OperationResult<ProductDto>` | - Validates `id > 0` and fields<br>- Calls `IProductRepository.UpdateAsync`<br>- Calls `IAppCache.InvalidateProductAndReports` on update | - Modified: `Success`, updated DTO, cache invalidated<br>- Unchanged: `Success`, message `"No changes were needed."`, no cache invalidation<br>- Missing: `NotFound`, no invalidation<br>- Concurrency conflict: `Conflict`, no invalidation | `Unit`, `ServiceCrud`, `Update` |
| **DeleteProductAsync** | `int id`, `CancellationToken` | `OperationResult<DeleteProductResultDto>` | - Validates `id > 0`<br>- Calls `IProductRepository.DeleteAsync`<br>- Calls `IAppCache.InvalidateProductAndReports` on delete | - Existing: `Success`, returns `DeleteProductResultDto(id)`, cache invalidated<br>- Missing: `NotFound`, no invalidation<br>- Concurrency conflict: `Conflict`, no invalidation | `Unit`, `ServiceCrud`, `Delete` |

### 3.2 ReportService Contract Map

| Operation | Input Types | Output Type | Dependency Interactions | Expected Outcomes | Test Categories |
|---|---|---|---|---|---|
| **GetDashboardAsync** | `int lowStockThreshold`, `CancellationToken` | `OperationResult<ReportDashboardDto>` | - Validates `lowStockThreshold >= 0`<br>- Sequentially calls `_cache.CachedAsync` for 5 report stored procs<br>- Passes threshold to `usp_Product_LowStock` | - Valid: `Success` with all 5 aggregated datasets<br>- Negative threshold: `ValidationFailure`, no procs called<br>- Zero threshold: `Success`<br>- Any proc failure: `PersistenceFailure`<br>- Sequential execution verified (no concurrent DB access) | `Unit`, `ReportService` |

---

## 4. Test Stage Mapping (7A–7F)

| Stage | Focus Area | Deliverables |
|---|---|---|
| **7A** | Plan, Test Project Configuration, Discovery Verification | ✅ COMPLETE (`CreateProduct_ValidInput_ReturnsSuccessAndInvalidatesCache`, initial runner verified) |
| **7B** | Create & Read Operations | ✅ COMPLETE (15 tests verified: 4 create tests + 9 read tests with parameter expansion) |
| **7C** | Update & Delete Operations | ✅ COMPLETE (20 tests verified: 9 update tests + 7 delete tests with commit ordering and parameter expansion; 35 cumulative tests) |
| **7D** | Validation Matrix, Error Policy, Cancellation | ✅ COMPLETE (94 tests verified: 87 validation matrix executions across Name, Category, Price, Stock, Id for Create/Update/Read/Delete + 7 cancellation/error propagation tests; 129 cumulative tests) |
| **7E** | Cache Infrastructure & Report Service | ✅ COMPLETE (25 tests verified: 4 cache contract/secondary failure tests across CRUD, 12 AppCache component tests with deterministic expiration and in-flight read race guard, 9 ReportService tests; 154 cumulative tests) |
| **7F** | Full Run, Evidence & Finalization | ✅ COMPLETE (Full suite execution with 157 passed tests, machine-readable TRX report generated, native MTP Cobertura code coverage analyzed with 100% service line coverage, finalized test README and evidence record) |

---

## 5. Implementation vs. Plan Observations

1. **Allowed Categories**:
   - The original specification mentions: *"Category is a plain string — no Category table. No unique-product-name requirement."*
   - In `ProductService.cs` and `CreateProductDto.cs`, category input is validated against `AllowedCategories` ("Accessories", "Electronics", "Office Furniture", "Stationery", "Stationary") and normalized.
   - Tests adhere to this implemented validation contract so tests pass truthfully without weakening validation.
2. **Category Normalization**:
   - `ProductService.NormalizeCategory` automatically handles title-casing and maps `"Stationary"` -> `"Stationery"`. Tests verify this behavior across create and update.
3. **Price Precision & Range**:
   - `decimal.Round(price.Value, 2) != price.Value` enforces maximum 2 decimal places.
   - Genuine defect fixed during 7D: Enforced `decimal(18,2)` upper range limit (`price.Value > 999999999999999.99m`) in `ProductService.ValidateProductInput`, `CreateProductDto.Validate`, and `UpdateProductDto.Validate` to protect SQL Server persistence from unhandled arithmetic overflow.
4. **Omission vs. Intentional Zero**:
   - `CreateProductDto` and `UpdateProductDto` define `decimal? Price` and `int? Stock`. Nullable numeric properties allow distinguishing an omitted field (`null`) from an intentional zero (`0.00m` / `0`), both of which are valid domain values.
5. **Deterministic Expiration Testing via `MemoryCacheOptions.Clock`**:
   - `Microsoft.Extensions.Caching.Memory.MemoryCacheOptions` in ASP.NET Core 10 exposes `Clock: Microsoft.Extensions.Internal.ISystemClock`.
   - Implemented `TestClock : ISystemClock` inside `AppCacheComponentTests.cs` to advance time deterministically without `Thread.Sleep`, `Task.Delay`, altering production timeouts, or private-reflection hacking.
6. **Sequential Execution of Stored Procedures in `ReportService`**:
   - Scoped EF Core `DbContext` instances do not support concurrent operations on the same context. `ReportService.GetDashboardAsync` awaits the five stored-procedure queries sequentially rather than issuing parallel tasks with `Task.WhenAll`.
   - Tested using atomic `Interlocked.Increment` and `Interlocked.Decrement` concurrency tracking, proving `maxConcurrentCalls == 1`.

