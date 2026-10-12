# WaterlilyInventory — Unit Tests (`WaterlilyInventory.UnitTests`)

> **Location:** `tests/WaterlilyInventory.UnitTests/`  
> **Target Framework:** `net10.0` (LTS SDK `10.0.401`)  
> **Project Sdk:** `MSTest.Sdk/4.0.2`  
> **Test Platform / Runner:** `Microsoft.Testing.Platform`  
> **Mocking Library:** `Moq` (Version 4.20.72)  
> **Parallelization:** `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`

---

## 1. Overview & Isolation Boundaries

This test suite exercises the core application services (`ProductService`, `ReportService`) and infrastructure components (`AppCache`) in complete isolation.

### Isolation Rules
- **No SQL Server / Database Dependency**: Tests run without connecting to SQL Server Express or running EF Core migrations.
- **No Web Server / Kestrel Required**: Tests do not start Kestrel or require HTTP endpoints to be reachable.
- **No EF In-Memory / SQLite Substitutes**: Strict repository interface mocks (`IProductRepository`, `IGenericRepository<T>`) supply controlled persistence outcomes.
- **Strict Mocking for Data Boundaries**: Repository mocks use `MockBehavior.Strict` to immediately surface any unplanned database queries.
- **Cache Isolation**: `IAppCache` is mocked with controlled miss/hit setups. On miss, the mock executes the application's genuine `AsyncDataFactory<T>` delegate callback.
- **Logging**: `NullLogger<T>.Instance` is provided by default.

---

## 2. Verified Run Commands

Execute these commands from the **workspace root** in PowerShell or bash:

### Run Entire Solution Test Suite
```powershell
dotnet test
```

### Run Unit Tests Project Directly
```powershell
dotnet test --project tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj
```

### Restore Dependencies
```powershell
dotnet restore tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj
```

---

## 3. Project Structure

```
tests/WaterlilyInventory.UnitTests/
├── Common/
│   ├── CacheMockExtensions.cs      # SetupCachedMiss, SetupCachedHit helpers using AsyncDataFactory<T>
│   └── ProductTestBuilder.cs       # Deterministic builders for DTOs, entities, and write results
├── Services/
│   ├── ProductServiceCreateTests.cs# Creation workflow, validation failure, persistence exception, safe error mapping
│   ├── ProductServiceReadTests.cs  # List/detail reads, cache hits/misses, delegate execution, empty collections, 404
│   ├── ProductServiceUpdateTests.cs# Modified updates, unchanged no-ops, conflicts, commit ordering, authoritative outcomes
│   └── ProductServiceDeleteTests.cs# Deletion workflows, 404, invalid IDs, conflicts, commit ordering guards
├── MSTestSettings.cs               # Assembly parallelization policy (MethodLevel)
├── WaterlilyInventory.UnitTests.csproj
└── README.md                       # This documentation file
```

---

## 4. Test Categories

| Category | Purpose |
|---|---|
| `Unit` | Tests exercising isolated units of logic without external I/O |
| `ServiceCrud` | Tests verifying CRUD operation workflows on Service implementations |
| `CacheComponent` | (Stage 7E) Component tests for real in-memory cache helper |
| `ReportService` | (Stage 7E) Tests verifying report aggregation and threshold logic |

---

## 5. Checkpoint 7A — First Meaningful Test

- **Test Class:** `ProductServiceCreateTests`
- **Test Method:** `CreateProduct_ValidInput_ReturnsSuccessAndInvalidatesCache`
- **Scenario:** Valid `CreateProductDto` (Name: `"Ergonomic Keyboard"`, Category: `"Electronics"`, Price: `129.99m`, Stock: `42`).
- **Arrangement:** Strict mock `IProductRepository` returns `RepositoryWriteResult<Product>.Created` with generated ID `101`. Strict mock `IAppCache` expects `InvalidateProductAndReports(101)`.
- **Assertions:**
  1. `result.IsSuccess` is `true`.
  2. `result.ErrorCode` and `result.ValidationErrors` are `null`.
  3. `result.Data` has ID `101`, matching Name, normalized Category, Price, and Stock.
  4. Repository `CreateAsync` called exactly once with matching parameters.
  5. Cache `InvalidateProductAndReports(101)` called exactly once.
  6. No unexpected calls were made to dependencies.

---

## 6. Checkpoint 7B — Create & Read Service Tests (15 Tests Verified)

### ProductServiceCreateTests (4 tests)
- `CreateProduct_ValidInput_ReturnsSuccessAndInvalidatesCache`: Verified creation success, entity mapping, repository write, and cache invalidation.
- `CreateProduct_BlankName_ReturnsValidationFailureWithoutRepositoryWriteOrInvalidation`: Verified field validation rejection, null data, no repository write, and no cache invalidation.
- `CreateProduct_PersistenceException_ReturnsSafeFailureWithoutInvalidationOrRetry`: Verified database exception suppression, generic safe message, diagnostic marker suppression, single write attempt, and no cache invalidation.
- `CreateProduct_RepositoryReturnsFailureOutcome_ReturnsPersistenceFailureWithoutInvalidation`: Verified non-Created write outcome (Conflict) maps to PersistenceFailure without cache invalidation.

### ProductServiceReadTests (9 tests / 11 executions)
- `GetAllProducts_CacheMiss_ExecutesFactoryAgainstRepositoryAndReturnsMappedDtos`: Cache miss on `product:list` executes factory delegate against repository mock, maps entities to DTOs, returns success.
- `GetAllProducts_EmptyRepository_ReturnsSuccessfulEmptyListNotNotFound`: Empty database returns `IsSuccess = true` with 0 items (never NotFound).
- `GetAllProducts_RepositoryThrowsPersistenceException_ReturnsSafeFailureNotFabricatedEmptyList`: Exception during read returns safe PersistenceFailure, suppressing internal diagnostics and avoiding fake empty lists.
- `GetAllProducts_CacheHit_ReturnsCachedSnapshotWithoutRepositoryAccess`: Cache hit returns pre-stored DTOs; zero database calls made (verified via strict mock).
- `GetProductById_CacheMiss_ExistingProduct_ReturnsSuccessWithAllFields`: Detail read on cache miss executes factory, queries repository by Id, returns mapped DTO with exact fields.
- `GetProductById_CacheMiss_MissingProduct_ReturnsNotFound`: Repository returning null maps to `OperationResult.NotFound` with descriptive message.
- `GetProductById_InvalidId_ZeroOrNegative_ReturnsValidationFailureWithoutDataAccess` (3 test cases): `id = 0`, `-1`, `-999` fail validation before cache or repository interaction.
- `GetProductById_PersistenceException_ReturnsSafeFailureWithoutLeakage`: Read failure maps to safe PersistenceFailure, diagnostic marker suppressed.
- `GetProductById_UsesLongLivedCacheModeAndIdSpecificKey`: Verifies detail reads specifically invoke `CachedLongAsync` with Id-specific key and NEVER invoke 5-minute `CachedAsync`.

---

## 7. Checkpoint 7C — Update & Delete Service Tests (20 Tests / 35 Cumulative)

### ProductServiceUpdateTests (9 tests / 11 executions)
- `UpdateProduct_ValidChanges_ReturnsSuccessAndInvalidatesCache`: Verified valid modified update, updated DTO returned, authoritative Id preserved (primary key immutable), single repository write, and cache invalidation for updated Id.
- `UpdateProduct_UnchangedValues_ReturnsSuccessWithoutInvalidation`: Verified repository `Unchanged` outcome maps to success with `"No changes were needed."` without invalidating caches.
- `UpdateProduct_MissingProduct_ReturnsNotFoundWithoutInvalidation`: Verified missing product update returns `NotFound` without cache invalidation.
- `UpdateProduct_InvalidInput_ReturnsValidationFailureWithoutDatabaseCallOrInvalidation` (3 test cases): `id = 0`, `-5`, and blank name fail validation at service boundary before repository call.
- `UpdateProduct_ConcurrencyConflict_ReturnsConflictResultWithoutInvalidation`: Verified concurrency conflict outcome maps to `Conflict` (not generic NotFound or success) without cache invalidation.
- `UpdateProduct_PersistenceException_ReturnsSafeFailureWithoutRetryOrInvalidation`: Verified expected persistence exception maps to safe `PersistenceFailure`, suppressing internal diagnostics and preventing retries.
- `UpdateProduct_CachedDataExists_RejectsSuccessWhenRepositoryReportsNotFound`: Verified authoritative persistence decision: service reports `NotFound` when repository reports missing record, even if cached detail data existed.
- `UpdateProduct_CommitOrdering_InvalidationCannotPrecedeSuccessfulWrite`: Verified that while repository write is in-flight (`TaskCompletionSource`), cache invalidation is NOT called; after write completion, invalidation executes once.
- `UpdateProduct_CommitOrdering_UnsuccessfulCompletionDoesNotInvalidate`: Verified that if repository write fails or throws, cache invalidation is never invoked.

### ProductServiceDeleteTests (7 tests / 9 executions)
- `DeleteProduct_ExistingId_ReturnsSuccessAndInvalidatesCache`: Verified deletion success with `DeleteProductResultDto(DeletedId)` and single cache invalidation for the deleted Id.
- `DeleteProduct_MissingProduct_ReturnsNotFoundWithoutInvalidation`: Verified missing product deletion returns `NotFound` without cache invalidation.
- `DeleteProduct_InvalidId_ZeroOrNegative_ReturnsValidationFailureWithoutDataAccess` (3 test cases): `id = 0`, `-1`, `-100` fail validation at service boundary before data mutation.
- `DeleteProduct_ConcurrencyConflict_ReturnsConflictWithoutInvalidation`: Verified deletion conflict maps to `Conflict` without invalidating caches.
- `DeleteProduct_PersistenceException_ReturnsSafeFailureWithoutRetryOrInvalidation`: Verified expected persistence exception returns safe `PersistenceFailure` without leaking diagnostics.
- `DeleteProduct_CommitOrdering_InvalidationCannotPrecedeSuccessfulDeletion`: Verified that during in-flight deletion, cache invalidation has not occurred; upon confirmed completion, invalidation executes once.
- `DeleteProduct_CommitOrdering_UnsuccessfulDeletionDoesNotInvalidate`: Verified that failed deletion never triggers cache invalidation.

---

## 8. Troubleshooting

- **CS8619 / Nullability Warnings**: When configuring `IAppCache.CachedAsync<T>`, the return type is `Task<T?>`. The helper in `CacheMockExtensions.cs` uses `async (key, factory, token) => (T?)await factory(token)` to ensure type safety.
- **MSTEST0037 Analyzer Compliance**: Use MSTest's specialized assertion methods (`Assert.HasCount`, `Assert.IsEmpty`, `Assert.Contains`) for collection and string assertions rather than generic `Assert.AreEqual` / `Assert.IsTrue`.
- **Microsoft.Testing.Platform Runner**: Configured in `global.json` under `"test": { "runner": "Microsoft.Testing.Platform" }`. Use `dotnet test --project <path>` rather than combining with legacy VSTest switches.
- **Mock Verification for Completed Writes**: When using `VerifyNoOtherCalls()`, ensure all invoked mock methods (including writes that throw expected exceptions) are explicitly verified with `Times.Once`.
