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
| **7C** | Update & Delete Operations | - `ProductServiceUpdateTests` (changed, unchanged, not found, conflict)<br>- `ProductServiceDeleteTests` (delete, not found, ordering guard) |
| **7D** | Validation Matrix, Error Policy, Cancellation | - Parameterized validation boundary tests (names, categories, price precision, stock)<br>- Exception masking & diagnostic suppression<br>- `CancellationToken` propagation tests |
| **7E** | Cache Infrastructure & Report Service | - 7E.1: Service cache mode contracts (`Cached` vs. `CachedLong`)<br>- 7E.2: `AppCache` component tests with in-memory cache and generation tracking<br>- 7E.3: `ReportService` dashboard aggregation, ties, and sequential execution |
| **7F** | Full Run, Evidence & Finalization | - Complete suite execution, TRX report, coverage analysis, test README finalization |

---

## 5. Implementation vs. Plan Observations

1. **Allowed Categories**:
   - The original specification mentions: *"Category is a plain string — no Category table. No unique-product-name requirement."*
   - In `ProductService.cs` and `CreateProductDto.cs`, category input is validated against `AllowedCategories` ("Accessories", "Electronics", "Office Furniture", "Stationery", "Stationary") and normalized.
   - Tests will adhere to this actual implemented validation contract so tests pass truthfully without weakening validation.
2. **Category Normalization**:
   - `ProductService.NormalizeCategory` automatically handles title-casing and maps `"Stationary"` -> `"Stationery"`. Tests will verify this behavior.
3. **Price Precision**:
   - `decimal.Round(price.Value, 2) != price.Value` enforces maximum 2 decimal places. Test boundaries will explicitly verify this check.
