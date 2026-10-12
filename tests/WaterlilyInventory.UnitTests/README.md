# WaterlilyInventory — Unit & Component Tests (`WaterlilyInventory.UnitTests`)

> **Location:** `tests/WaterlilyInventory.UnitTests/`  
> **Target Framework:** `net10.0` (.NET 10 LTS, SDK `10.0.401` pinned in `global.json`)  
> **Project Sdk:** `MSTest.Sdk/4.0.2`  
> **Test Platform / Runner:** `Microsoft.Testing.Platform` (MTP)  
> **Mocking Library:** `Moq` (Version 4.20.72)  
> **Parallelization:** `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`  
> **Latest Verified Execution:** **157 Tests Discovered, 157 Passed, 0 Failed, 0 Skipped** (Duration: ~2.4s)  
> **Detailed Evidence Record:** [docs/TestingResults.md](../../docs/TestingResults.md)

---

## 1. Purpose & Relation to the Waterlily Assessment

This test project fulfills Stage 7 (Checkpoints 7A–7F) of the **WaterlilyInventory** assessment. Its primary mission is to provide rigorous, automated verification of:
1. **Core Service Business Rules & Orchestration**: Real [ProductService](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/src/WaterlilyInventory.Web/Services/Implementations/ProductService.cs) and [ReportService](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/src/WaterlilyInventory.Web/Services/Implementations/ReportService.cs) implementations, executing CRUD business validation, DTO mapping, outcome packaging into `OperationResult<T>`, error policies, and safe diagnostic suppression.
2. **In-Memory Caching & Delegate Infrastructure**: Real [AppCache](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/src/WaterlilyInventory.Web/Caching/AppCache.cs) component behavior with `IMemoryCache`, deterministic absolute expiration via clock seams, cache mode separation (`Cached` vs. `CachedLong`), shared invalidation scopes, and generation-based race condition protection.
3. **Database Independence**: The suite isolates domain orchestration from external infrastructure. Unit tests run completely in-process without requiring a live SQL Server instance, EF Core database migrations, a running web server (Kestrel), browser automation, or external network connections.

---

## 2. Real vs. Mocked Boundaries

| Component Under Test | What Is Real | What Is Mocked / Controlled |
|---|---|---|
| **ProductService Unit Tests** | Real `ProductService` business logic, validation rules, category normalization, DTO mapping, exception handling | Mocked `IProductRepository` (`MockBehavior.Strict`), Mocked `IAppCache` (`MockBehavior.Strict`), `NullLogger<ProductService>.Instance` |
| **ReportService Unit Tests** | Real `ReportService` aggregation logic, threshold validation, sequential execution coordination | Mocked `IProductRepository` (`MockBehavior.Strict`), Mocked `IAppCache` (`MockBehavior.Strict`), `NullLogger<ReportService>.Instance` |
| **AppCache Component Tests** | Real `AppCache` instance, real `IMemoryCache` (via `MemoryCache`), real generation change-token tracking | `TestClock : ISystemClock` (deterministic clock seam), test-controlled asynchronous data factory delegates |

### What Unit Tests Do NOT Prove (System Boundaries)
- **Stored-Procedure SQL Logic**: Unit tests verify that stored procedure methods on `IProductRepository` are called with correct arguments and mapped into DTOs; they do **not** execute SQL Server procedures or validate database-side calculations (`AVG`, `DENSE_RANK`, `SUM`, `TOP 5`). Procedure correctness is verified separately via SQL migrations and database smoke scripts (`Data/Migrations/20261011145854_AddStoredProcedures.cs`).
- **HTTP / Controller Mapping**: Mapping domain `OperationResult<T>` outcomes into HTTP status codes (200, 201, 400, 404, 409, 500) and JSON envelopes is owned by MVC Controllers.
- **Security & UI Middleware**: Antiforgery validation, Serilog rolling file sinks, and browser JavaScript/DOM interactions remain integration and web checks.

---

## 3. Application Delegate Callback vs. Moq Callbacks

A crucial architectural distinction exists in this codebase between application delegates and Moq callbacks:

1. **Application Data Factory (`AsyncDataFactory<T>`)**:
   - The application defines `public delegate Task<T?> AsyncDataFactory<T>(CancellationToken cancellationToken)`.
   - On cache miss, `IAppCache.CachedAsync` and `CachedLongAsync` invoke and await this real callback to load fresh data within the active request scope without capturing scoped dependencies into singletons.
   - In unit tests, our `CacheMockExtensions.SetupCachedMiss` configures Moq to actually execute and await this application delegate:
     ```csharp
     mock.Setup(c => c.CachedAsync(key, It.IsAny<AsyncDataFactory<T>>(), token))
         .Returns<string, AsyncDataFactory<T>, CancellationToken>(
             async (k, factory, ct) => await factory(ct));
     ```
2. **Moq Observation Callbacks (`.Callback(...)`)**:
   - Moq's `.Callback(...)` feature is used strictly for synchronous observation of mock invocations (e.g., capturing passed parameters, signaling `TaskCompletionSource` events).
   - Moq callbacks never execute uncontrolled asynchronous work or fire-and-forget tasks.

---

## 4. Verified Run Commands

Execute all commands from the **workspace root** (`c:\Users\Asus\Desktop\Free Projects\WaterlilyInventory`):

### 4.1 Run Entire Solution Test Suite
```powershell
dotnet test
```

### 4.2 Run Test Project Directly
```powershell
dotnet test --project tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj
```

### 4.3 Run by Test Category
```powershell
# Run only CRUD service unit tests
dotnet test --filter "TestCategory=ServiceCrud"

# Run only real in-memory cache component tests
dotnet test --filter "TestCategory=CacheComponent"

# Run only report service aggregation tests
dotnet test --filter "TestCategory=ReportService"
```

### 4.4 Run a Specific Test Class
```powershell
dotnet test --filter "FullyQualifiedName~ProductServiceValidationTests"
```

### 4.5 Generate Machine-Readable TRX Test Results
```powershell
dotnet test --project tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj --report-trx --results-directory TestResults --report-trx-filename test_results.trx
```
*Generates `TestResults/test_results.trx` conforming to Visual Studio / Azure DevOps schema with all 157 passed test entries.*

### 4.6 Generate Cobertura Code Coverage Report
```powershell
dotnet test --project tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj --coverage --coverage-output-format cobertura --results-directory TestResults --coverage-output coverage.cobertura.xml
```
*Produces `TestResults/coverage.cobertura.xml` collecting exact line and branch hits via Microsoft Testing Platform's native coverage collector.*

### 4.7 Combined Execution (TRX Report + Cobertura Coverage)
```powershell
dotnet test --project tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj --report-trx --results-directory TestResults --report-trx-filename test_results.trx --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

---

## 5. Arrange / Act / Assert Example

Here is a representative test from [ProductServiceCreateTests.cs](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/tests/WaterlilyInventory.UnitTests/Services/ProductServiceCreateTests.cs#L143-L177) demonstrating strict mocking, commit-before-invalidation ordering, and secondary failure resilience:

```csharp
[TestMethod]
public async Task CreateProduct_CacheInvalidationThrows_PreservesPersistenceSuccessWithoutRetry()
{
    // Arrange: Valid product input
    var dto = ProductTestBuilder.CreateValidDto();
    const int generatedId = 999;
    var cancellationToken = CancellationToken.None;

    // Strict repository mock: confirms the write succeeded and committed
    _repositoryMock
        .Setup(r => r.CreateAsync(It.IsAny<Product>(), cancellationToken))
        .ReturnsAsync(ProductTestBuilder.CreateSuccessResult(id: generatedId));

    // Strict cache mock: secondary cache invalidation fails unexpectedly
    _cacheMock
        .Setup(c => c.InvalidateProductAndReports(generatedId))
        .Throws(new InvalidOperationException("Cache service temporarily unavailable."));

    var service = new ProductService(_repositoryMock.Object, _cacheMock.Object, NullLogger<ProductService>.Instance);

    // Act: Invoke the service operation
    var result = await service.CreateProductAsync(dto, cancellationToken);

    // Assert: Truthful persistence outcome is preserved; write is never retried
    Assert.IsNotNull(result);
    Assert.IsTrue(result.IsSuccess, "Persistence was confirmed by the repository; service must return success.");
    Assert.IsNotNull(result.Data);
    Assert.AreEqual(generatedId, result.Data.Id);

    // Verify exactly one write attempt was made (no retries)
    _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<Product>(), cancellationToken), Times.Once);
    _cacheMock.Verify(c => c.InvalidateProductAndReports(generatedId), Times.Once);
    _repositoryMock.VerifyNoOtherCalls();
    _cacheMock.VerifyNoOtherCalls();
}
```

---

## 6. Cache Expiration & Invalidation Testing Technique

### Deterministic Clock Seam (`TestClock : ISystemClock`)
To verify that `CachedAsync` enforces an absolute 5-minute deadline and that cache hits do not extend it, we avoid fragile `Thread.Sleep`, `Task.Delay`, or private reflection.
- In ASP.NET Core 10, `MemoryCacheOptions` exposes the `Clock` property typed as `Microsoft.Extensions.Internal.ISystemClock`.
- [AppCacheComponentTests.cs](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/tests/WaterlilyInventory.UnitTests/Caching/AppCacheComponentTests.cs) provides an in-test `TestClock : ISystemClock` implementing `UtcNow`.
- The test advances time instantly (`clock.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(50)))`), asserts a hit before the 5-minute mark, advances past 5 minutes, and asserts an expiration miss requiring loader re-execution.
- For `CachedLongAsync`, the clock is advanced by 24 hours, proving retention without time-based eviction, while confirming eviction immediately upon explicit invalidation.

### Stale In-Flight Read Protection (`TaskCompletionSource`)
When a write invalidates the cache while an earlier slow read is in-flight, `AppCache`'s internal generation token prevents the slow read from overwriting fresh new-generation data upon completion.
- Tested using coordinated `TaskCompletionSource` instances with `RunContinuationsAsynchronously`.
- Proves that after generation advancement, the paused old loader's delayed completion is discarded and subsequent reads continue to see the new generation's published value.

---

## 7. Test Suite Structure & Responsibilities

```
tests/WaterlilyInventory.UnitTests/
├── Common/
│   ├── CacheMockExtensions.cs           # SetupCachedMiss / SetupCachedHit helpers with AsyncDataFactory<T>
│   └── ProductTestBuilder.cs            # Deterministic builders for DTOs, domain entities, and write results
├── Services/
│   ├── ProductServiceCreateTests.cs     # 5 tests: creation success, blank name, safe persistence errors, secondary invalidation
│   ├── ProductServiceReadTests.cs       # 10 tests (12 executions): list/detail reads, cache hits/misses, independent keys, 404
│   ├── ProductServiceUpdateTests.cs     # 10 tests (12 executions): modified updates, unchanged no-ops, conflicts, commit ordering
│   ├── ProductServiceDeleteTests.cs     # 8 tests (10 executions): deletion workflows, not found, invalid IDs, commit ordering
│   ├── ProductServiceValidationTests.cs # 21 tests (90 executions): Name, Category, Price, Stock, Id boundary matrix & normalization
│   ├── ProductServiceCancellationTests.cs# 7 tests: cancellation token propagation and sensitive credential suppression
│   └── ReportServiceTests.cs            # 8 tests (9 executions): 5 reports, threshold bounds, ties, non-overlapping DB calls
├── Caching/
│   └── AppCacheComponentTests.cs        # 12 component tests: miss/hit, key separation, null non-retention, TestClock, in-flight race
├── MSTestSettings.cs                    # MethodLevel parallelization configuration
├── WaterlilyInventory.UnitTests.csproj
└── README.md                            # This documentation file
```

---

## 8. Summary of Covered Test Scenarios

### Core CRUD Outcomes
- **Create**: Success with ID assignment; field validation rejection; conflict write outcome mapping; persistence exception suppression; secondary cache failure preservation.
- **Read (List)**: Cache miss executes repository delegate; empty repository returns successful empty collection (not 404); repository failure returns safe failure (not fabricated empty collection); cache hit completely bypasses repository.
- **Read (Detail)**: Cache miss queries repository by ID; non-existent ID returns `NotFound`; ID `<= 0` rejected before data access; persistence failure handled safely; detail lookups strictly use `CachedLongAsync` with ID-specific keys; independent product IDs have isolated cache keys.
- **Update**: Modified update commits new fields, preserves immutable PK, and invalidates cache; unchanged update returns `"No changes were needed."` without invalidating cache; non-existent ID returns `NotFound`; invalid input rejected before data access; concurrency conflict returns `Conflict`; persistence failure handled safely; authoritative repository takes precedence over cached snapshot; commit-ordering guards verify invalidation cannot precede write; secondary invalidation failure preserves write.
- **Delete**: Existing ID returns `DeleteProductResultDto(DeletedId)` and invalidates cache; non-existent ID returns `NotFound`; ID `<= 0` rejected; concurrency conflict returns `Conflict`; safe persistence exception handling; commit-ordering guards; secondary invalidation failure preserves write.

### Validation Matrix (Create, Update, Read, Delete)
- **Name**: Null, empty, whitespace-only, 151 chars overlong, and special characters rejected; 150 chars max, whitespace trimming, and valid hyphens/spaces accepted.
- **Category**: Null, empty, whitespace-only, 101 chars overlong, and unsupported categories rejected; allowed categories, title-casing normalization, whitespace collapsing, and `"Stationary"` -> `"Stationery"` alias standardization accepted; defensive `NormalizeCategory` null/whitespace guards verified.
- **Price**: Null, negative (-0.01m, -150.00m), excessive decimal precision (> 2 decimal places), and over-range values (> 999,999,999,999,999.99m) rejected; 0.00m zero price, 0.01m minimum penny, 129.99m standard decimal, and 999,999,999,999,999.99m max accepted.
- **Stock**: Null and negative (-1, -100) rejected; 0 zero stock, 1, 500, 1,000,000 positive integers accepted.
- **Id**: 0, negative (-1, -999) rejected before data access; 1 and 101 accepted.

### Error Policies & Cancellation
- **Cancellation**: CancellationToken passed to GetAll, GetById, Create, Update, Delete, and GetDashboard propagates directly as `OperationCanceledException` without disguise as PersistenceFailure, retrying, or invalidating caches.
- **Defect Escape**: General unhandled runtime exceptions (`InvalidOperationException`) escape unswallowed for middleware handling.
- **Credential Suppression**: Sensitive connection strings and database credentials inside inner exceptions are strictly stripped from public response messages, errors, and data.

### Report Service Aggregation
- **Five Datasets**: Returns all 5 distinct reports (Average Price, Highest Stock Value, Valuation, Low Stock, Top 5 Expensive) and preserves low-stock threshold.
- **Threshold Boundaries**: Accepts threshold `0`; rejects negative thresholds (`-1`, `-10`) before data access.
- **Tied Categories**: Preserves all tied highest-value categories in the output.
- **Sequential DbContext Coordination**: Concurrency tracking confirms `maxConcurrentCalls == 1`, proving stored procedures are awaited sequentially to prevent DbContext concurrency violations.

---

## 9. Troubleshooting & Known Gotchas

| Issue | Cause | Resolution |
|---|---|---|
| **Zero Tests Discovered** | Runner misconfiguration or missing package reference. | Verify `global.json` defines `"test": { "runner": "Microsoft.Testing.Platform" }`. Ensure `MSTest.Sdk` is used in `.csproj`. |
| **Package / SDK Mismatch** | Building with an unexpected SDK version. | Check `dotnet --version` matches `10.0.401` as pinned in `global.json`. |
| **Stale Build Artifacts** | Previous compilation artifacts lingering in `bin/` or `obj/`. | Run `dotnet clean` followed by `dotnet build`. |
| **Runner Flag Mismatch** | Using legacy VSTest flags like `--logger "trx"` with MTP. | Use MTP native flags: `--report-trx` (for TRX results) and `--coverage` (for code coverage). |
| **Strict-Mock Failures (`MockBehavior.Strict`)** | An unexpected invocation was made to a dependency mock, or an invocation was not verified before `VerifyNoOtherCalls()`. | Inspect stack trace for the exact unexpected method call. Ensure all expected calls (including failure-path writes) have explicit setups and verifications. |
| **Unexpected Database Requirement** | A test accidentally invoked a concrete DbContext or repository. | Ensure tests construct Services using mocked `IProductRepository` and `IAppCache`. Never instantiate `ApplicationDbContext` in unit tests. |

---

## 10. How to Add a New Test

1. **Identify the Target**: Choose the appropriate test file in `Services/` or `Caching/`.
2. **Use Test Builders**: Use [ProductTestBuilder.cs](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/tests/WaterlilyInventory.UnitTests/Common/ProductTestBuilder.cs) for deterministic DTOs and entities.
3. **Follow AAA Pattern**:
   - **Arrange**: Set up strict mocks with explicit return types and matching cancellation tokens.
   - **Act**: Call the service method with `await`.
   - **Assert**: Assert result properties (`IsSuccess`, `Data`, `ErrorCode`), verify mock calls with `Times.Once`, and call `VerifyNoOtherCalls()`.
4. **Run and Verify**:
   ```powershell
   dotnet test --filter "FullyQualifiedName~YourNewTestName"
   ```

---

## 11. Resuming From Interruption

If test execution or authoring is interrupted:
1. Consult [docs/TestingStatus.md](../../docs/TestingStatus.md) to inspect the latest verified checkpoint.
2. Run `dotnet test` to confirm baseline suite health.
3. Check [docs/TestingPlan.md](../../docs/TestingPlan.md) for remaining contracts and acceptance criteria.
