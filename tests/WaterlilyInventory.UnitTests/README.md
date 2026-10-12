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
│   └── ProductServiceCreateTests.cs# First verified test: CreateProduct_ValidInput_ReturnsSuccessAndInvalidatesCache
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

## 5. First Meaningful Test: Checkpoint 7A

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

## 6. Troubleshooting

- **CS8619 / Nullability Warnings**: When configuring `IAppCache.CachedAsync<T>`, the return type is `Task<T?>`. The helper in `CacheMockExtensions.cs` uses `async (key, factory, token) => (T?)await factory(token)` to ensure type safety.
- **Microsoft.Testing.Platform Runner**: Configured in `global.json` under `"test": { "runner": "Microsoft.Testing.Platform" }`. Use `dotnet test --project <path>` rather than combining with legacy VSTest switches.
