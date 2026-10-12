# WaterlilyInventory — Testing Results & Execution Evidence

> **Execution Date:** 2026-10-12  
> **Target Framework:** .NET 10 LTS (`net10.0`, SDK `10.0.401` pinned in `global.json`)  
> **Testing Stack:** MSTest.Sdk `4.0.2`, Microsoft.Testing.Platform (MTP), Moq `4.20.72`  
> **Target Assembly:** `WaterlilyInventory.Web`  
> **Test Assembly:** `tests/WaterlilyInventory.UnitTests/bin/Debug/net10.0/WaterlilyInventory.UnitTests.dll`  
> **Machine-Readable TRX Report:** `TestResults/test_results.trx`  
> **Code Coverage Report:** `TestResults/coverage.cobertura.xml`

---

## 1. Executive Summary

| Metric | Result | Status |
|---|---|---|
| **Discovered Tests** | **157** | ✅ |
| **Executed Tests** | **157** | ✅ |
| **Passed Tests** | **157** | ✅ 100% Pass Rate |
| **Failed Tests** | **0** | ✅ |
| **Skipped / Ignored Tests** | **0** | ✅ |
| **Suite Execution Duration** | **2.41s** (3.80s with coverage & TRX) | ⚡ High Performance |
| **`ProductService` Line Coverage** | **100.0%** (113 / 113 lines) | 🎯 Full Service Coverage |
| **`ProductService` Branch Coverage**| **92.9%** (39 / 42 branches) | 🎯 |
| **`ReportService` Line Coverage**  | **100.0%** (9 / 9 lines) | 🎯 Full Service Coverage |
| **`ReportService` Branch Coverage** | **50.0%** (3 / 6 branches) | 🎯 (Default guard branches) |
| **`AppCache` Line Coverage**        | **75.6%** (31 / 41 lines) | 🎯 Functional Paths Covered |

---

## 2. Actual Run Commands & Output Evidence

### 2.1 Complete Test Suite Execution with TRX & Coverage Output

```powershell
dotnet test --project tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj `
  --report-trx --results-directory TestResults --report-trx-filename test_results.trx `
  --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

**Console Output:**
```
Running tests from C:\Users\Asus\Desktop\Free Projects\WaterlilyInventory\tests\WaterlilyInventory.UnitTests\bin\Debug\net10.0\WaterlilyInventory.UnitTests.dll (net10.0|x64)
C:\Users\Asus\Desktop\Free Projects\WaterlilyInventory\tests\WaterlilyInventory.UnitTests\bin\Debug\net10.0\WaterlilyInventory.UnitTests.dll (net10.0|x64) passed (2s 411ms)

  In process file artifacts produced:
    - C:\Users\Asus\Desktop\Free Projects\WaterlilyInventory\TestResults\coverage.cobertura.xml
    - C:\Users\Asus\Desktop\Free Projects\WaterlilyInventory\TestResults\test_results.trx

Test run summary: Passed!
  total: 157
  failed: 0
  succeeded: 157
  skipped: 0
  duration: 3s 802ms
```

### 2.2 Category-Filtered Verification Runs

```powershell
# 1. Real In-Memory Cache Component Tests
dotnet test --filter "TestCategory=CacheComponent"
# Output: Total: 12, Succeeded: 12, Failed: 0, Skipped: 0 (duration: 2.15s)

# 2. Report Service Aggregation Tests
dotnet test --filter "TestCategory=ReportService"
# Output: Total: 9, Succeeded: 9, Failed: 0, Skipped: 0 (duration: 2.48s)

# 3. CRUD Service Unit Tests
dotnet test --filter "TestCategory=ServiceCrud"
# Output: Total: 39, Succeeded: 39, Failed: 0, Skipped: 0 (duration: 2.21s)
```

---

## 3. Discovered Test Breakdown by Test File

| Test File | Category | Test Methods | Runner Executions | Primary Focus |
|---|---|---|---|---|
| `ProductServiceCreateTests.cs` | `Unit`, `ServiceCrud` | 5 | 5 | Creation success, blank name validation, safe persistence exceptions, non-Created outcomes, secondary cache failure resilience |
| `ProductServiceReadTests.cs` | `Unit`, `ServiceCrud` | 10 | 12 | List/detail cache misses, delegate execution, empty DB, persistence errors, cache hits, 404, invalid IDs, independent keys |
| `ProductServiceUpdateTests.cs` | `Unit`, `ServiceCrud` | 10 | 12 | Modified updates, unchanged no-op, 404, validation failure, conflicts, safe persistence errors, authoritative persistence, commit ordering |
| `ProductServiceDeleteTests.cs` | `Unit`, `ServiceCrud` | 8 | 10 | Deletion success with `DeletedId`, 404, invalid IDs, conflicts, safe persistence errors, commit ordering, secondary cache failure |
| `ProductServiceValidationTests.cs` | `Unit` | 21 | 90 | Data-driven matrix across Name (8), Category (10), Price (11), Stock (7), Id (5) for Create/Update/Read/Delete, defensive normalization |
| `ProductServiceCancellationTests.cs`| `Unit` | 7 | 7 | CancellationToken propagation across all operations, defect escape to middleware, sensitive credential suppression |
| `ReportServiceTests.cs` | `Unit`, `ReportService` | 8 | 9 | 5 distinct datasets, threshold 0, negative threshold rejection (-1, -10), empty lists, ties, persistence errors, sequential execution |
| `AppCacheComponentTests.cs` | `Unit`, `CacheComponent` | 12 | 12 | Real `AppCache` & `IMemoryCache`: miss/hit, key separation, empty list retention, null non-retention, TestClock expiration, in-flight race |
| **Total Suite** | | **81** | **157** | **157 Passed, 0 Failed, 0 Skipped** |

---

## 4. Code Coverage Analysis

### 4.1 Methodology & Collector
- **Collector:** Microsoft Testing Platform native coverage collector invoked via `--coverage`.
- **Output Format:** Cobertura XML format (`TestResults/coverage.cobertura.xml`).
- **Denominator / Target Scope:** The target domain and application services in `WaterlilyInventory.Web`.

### 4.2 Class-Level Coverage Summary

| Class | Covered Lines | Total Lines | Line Coverage | Covered Branches | Total Branches | Branch Coverage |
|---|---|---|---|---|---|---|
| `ProductService` | **113** | **113** | **100.0%** | **39** | **42** | **92.9%** |
| `ReportService` | **9** | **9** | **100.0%** | **3** | **6** | **50.0%** |
| `AppCache` | **31** | **41** | **75.6%** | **12** | **16** | **75.0%** |
| `CacheKeys` | **9** | **9** | **100.0%** | **0** | **0** | **N/A** |
| `RepositoryWriteResult<T>` | **12** | **12** | **100.0%** | **0** | **0** | **N/A** |
| `PersistenceException` | **2** | **2** | **100.0%** | **0** | **0** | **N/A** |

### 4.3 Why Assembly-Level Coverage is 33.1%
The overall assembly line rate is **33.1%** (460 covered / 1,389 valid lines). This is an **intentional and correct architectural outcome** of strict unit testing boundaries:
- **Excluded Infrastructure (0% by design in unit tests)**:
  - `GenericRepository<T>` and `ProductRepository`: Require SQL Server Express connection, EF Core SqlQuery execution, and migration schema.
  - `ProductsController`, `ReportsController`, `HomeController`: Require HTTP context, action execution, and MVC model binding.
  - Razor Views (`Views/Products/Index.cshtml`, `Views/Reports/Index.cshtml`, `_Layout.cshtml`): Precompiled Razor view documents executed in ASP.NET Core pipeline.
  - `Program.cs` & `DatabaseSeeder`: Application startup, dependency registration, and demo seed runner.
  - `AjaxAntiforgeryFilter` & `GlobalExceptionHandlingMiddleware`: HTTP request pipeline filters and middleware.
- Unit tests **deliberately isolate** Services and Caching so the test suite runs deterministically in < 3 seconds without external infrastructure dependencies.

---

## 5. Production Fixes Applied During Testing

1. **Price Upper-Bound Range Enforcement**:
   - **Defect Identified (Stage 7D)**: Submitting price values exceeding SQL Server's `decimal(18,2)` maximum (`999,999,999,999,999.99m`) would escape validation and trigger an unhandled database arithmetic overflow exception.
   - **Resolution**: Added range check (`price.Value > 999999999999999.99m`) to [ProductService.ValidateProductInput](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/src/WaterlilyInventory.Web/Services/Implementations/ProductService.cs), [CreateProductDto.Validate()](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/src/WaterlilyInventory.Web/DTOs/Products/CreateProductDto.cs), and [UpdateProductDto.Validate()](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/src/WaterlilyInventory.Web/DTOs/Products/UpdateProductDto.cs).
   - **Regression Verification**: Covered by parameterized boundary tests in `ProductServiceValidationTests.PriceInvalidBoundaryCases`.

---

## 6. System Boundaries & Remaining Integration Verification

Unit tests prove business rules, DTO mapping, caching policies, and error handling. The following aspects are verified through separate integration, database, and web smoke testing:

1. **Stored Procedure Mathematics**: Procedure execution (`dbo.usp_Product_AveragePriceByCategory`, `dbo.usp_Product_HighestStockValueCategory`, etc.) is verified in SQL Server via [Data/Migrations/20261011145854_AddStoredProcedures.cs](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/src/WaterlilyInventory.Web/Data/Migrations/20261011145854_AddStoredProcedures.cs) and [docs/Migrations_Idempotent.sql](file:///c:/Users/Asus/Desktop/Free%20Projects/WaterlilyInventory/docs/Migrations_Idempotent.sql).
2. **HTTP Status Code Mapping**: Controller JSON serialization and HTTP response code mapping (200, 201, 400, 404, 409, 500) are verified via MVC integration smoke checks ([docs/Verification.md](Verification.md)).
3. **Antiforgery Security**: Verification of CSRF cookie/header matching is handled by `AjaxAntiforgeryFilter` in integration tests.
4. **Client-Side UI**: jQuery AJAX calls, SweetAlert2 modals, Toastr notifications, and Chart.js rendering operate inside the web browser.
