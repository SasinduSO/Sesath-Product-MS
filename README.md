# WaterlilyInventory — Inventory Management Application

An enterprise-ready inventory management system built with **ASP.NET Core MVC (.NET 10 LTS)**, **Entity Framework Core Code-First**, and **Microsoft SQL Server 2022 Express**.  
Features a responsive, modern UI inspired by SESATH PRODUCTS branding, real-time client-side AJAX CRUD, in-memory caching with asynchronous delegate callbacks, structured rolling file logging via Serilog, and five SQL Server stored-procedure analytics reports.

---

## Technology Stack

| Layer | Component | Choice & Version |
|---|---|---|
| **Runtime & Framework** | Target Framework | .NET 10 LTS (`net10.0`, SDK `10.0.401`) |
| **Web Presentation** | Server Web Framework | ASP.NET Core MVC with Razor Views |
| **ORM & Data Access** | Database Provider | EF Core 10.0.12 (`Microsoft.EntityFrameworkCore.SqlServer`) |
| **Tooling** | EF Core CLI Tools | Local `dotnet-ef` 10.0.12 (pinned in `dotnet-tools.json`) |
| **Database** | Database Engine | Microsoft SQL Server 2022 Express (`.\SQLEXPRESS`) |
| **Logging** | Structured Logging | Serilog (`Serilog.AspNetCore` 9.0.0, `Serilog.Sinks.File` 6.0.0) |
| **Frontend Libraries** | Client Utilities | jQuery 3.7.1, Bootstrap 5.3.3 |
| **Notifications & Dialogs** | Toastr & SweetAlert2 | Toastr 2.1.4, SweetAlert2 11.14.5 |
| **Data Visualization** | Charting Engine | Chart.js 4.4.6 (UMD bundle) |

---

## Architectural Principles & Strict Separation

The system strictly enforces a unidirectional data-access hierarchy:
```
Browser (jQuery AJAX) 
  → Controller (Web presentation & status code mapping)
    → Service Interface (Business validation, caching policies, outcome contracts)
      → Repository Interface (Data materialization, writes, stored procedures)
        → EF Core DbContext & SQL Server 2022
```

- **Zero Boundary Leaks**: Controllers and Views **never** access DbContext, Repositories, SQL queries, or the memory cache directly.
- **Async/Await Throughout**: Every actual database I/O operation (queries, stored-procedure executions, commits) is purely asynchronous and propagates `CancellationToken`.
- **Explicit Outcome Contracts**:
  - Services return `OperationResult<T>` with typed outcomes (`Success`, `ValidationFailure`, `NotFound`, `Conflict`, `PersistenceFailure`). Bare `bool`, `void`, or raw entities are forbidden.
  - Repositories return `RepositoryWriteResult<T>` (`Created`, `Updated`, `Unchanged`, `Deleted`, `NotFound`, `Conflict`).
  - Web endpoints return a uniform camelCase JSON envelope: `{ isSuccess, data, message, errorCode, validationErrors, traceId }`.
- **In-Memory Caching & Delegates**:
  - `IAppCache` abstraction backed by `IMemoryCache`.
  - **Cached Mode**: 5-minute absolute expiration (repeated hits do not extend). Used for product lists and all report datasets.
  - **CachedLong Mode**: Long-lived entries with `CacheItemPriority.NeverRemove` (no time expiry; lives until mutation or application restart). Used for product detail lookups.
  - **Async Delegate Callback (`AsyncDataFactory<T>`)**: Invoked only on cache misses; resolves fresh data within the active request scope without capturing scoped dependencies into singletons.
  - **Atomic Generation Guard**: Generation counter advanced on invalidation prevents concurrent slow reads from storing stale data.
  - **Post-Commit Invalidation**: Clears affected cache keys only after confirmed successful database commits.
- **Exception Ownership**:
  - Expected domain/validation failures are resolved by Services and mapped to HTTP status codes by Controllers.
  - Unexpected infrastructure defects escape to `GlobalExceptionHandlingMiddleware`, which returns safe HTTP 500 JSON envelopes or MVC error pages without exposing stack traces, SQL, or credentials.

---

## Application Configuration & Branding

Configured in `src/WaterlilyInventory.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=WaterlilyInventoryDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "AppConfig": {
    "BrandName": "SESATH PRODUCTS",
    "Currency": "USD"
  }
}
```

- **Branding**: Displays `SESATH PRODUCTS` with a navy angular brandmark in the top header.
- **Currency**: Configured globally as `USD` (`$`), formatted client-side using `Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })`.

---

## Stored-Procedure Reports & Approved Calculations

All analytics calculations execute directly inside SQL Server stored procedures managed via EF Core Code-First migrations (`AddStoredProcedures`):

1. **Average Price per Category (`dbo.usp_Product_AveragePriceByCategory`)**:
   - Calculates non-weighted arithmetic mean: `AVG(Price)` per category.
2. **Highest Stock Value Category (`dbo.usp_Product_HighestStockValueCategory`)**:
   - **Approved Interpretation**: Evaluates greatest cumulative monetary value: `SUM(Price * CAST(Stock AS BIGINT))`.
   - Uses `DENSE_RANK() OVER (ORDER BY InventoryValue DESC)` to return **all tied leading categories** with deterministic ordering.
3. **Inventory Valuation by Category (`dbo.usp_Product_InventoryValuationByCategory`)**:
   - Computes `SUM(Price * Stock)` and total units `SUM(Stock)` per category.
   - Frontend renders a Chart.js bar chart and an accessible table with Grand Total summation.
4. **Low-Stock Alert (`dbo.usp_Product_LowStock`)**:
   - Accepts `@Threshold INT` (default 10). Filters strictly by `Stock < @Threshold`.
   - Products with stock equal to the threshold are strictly **not** low-stock items.
5. **Top 5 Most Expensive Products (`dbo.usp_Product_TopFiveMostExpensive`)**:
   - Returns `TOP (5)` ordered by `Price DESC, Id ASC`.

---

## Seed Dataset & Verification Baseline

The database is seeded with 13 representative products across 4 categories:

| Category | Seeded Products | Total Units | Inventory Valuation | Average Price |
|---|---|---|---|---|
| **Electronics** | Laptop Stand ($50, stock 20), USB-C Cable ($20, stock 50), Wireless Mouse ($25, stock 40) | 110 | $3,000.00 | $31.67 |
| **Office Furniture** | Ergonomic Chair ($200, stock 10), Desk Lamp ($50, stock 11), Footrest ($50, stock 9) | 30 | $3,000.00 | $100.00 |
| **Stationery** | Notebook Set ($15, stock 0), Premium Pen ($30, stock 30), Sticky Notes Pack ($6, stock 100) | 130 | $1,500.00 | $17.00 |
| **Accessories** | Desk Mat ($25, stock 10), Cable Organizer ($10, stock 20), Monitor Wipe ($5, stock 10), Mechanical Keyboard ($150, stock 0) | 40 | $500.00 | $47.50 |
| **Baseline Total** | **13 Products** | **310 Units** | **$8,000.00** | — |

### Baseline Expected Metrics
- **Highest Stock Value**: `Electronics` ($3,000.00) and `Office Furniture` ($3,000.00) are **tied leaders**.
- **Low-Stock Alert (Threshold = 10)**: Returns 3 products: `Notebook Set` (stock 0), `Mechanical Keyboard` (stock 0), and `Footrest` (stock 9). Items with stock 10 (`Ergonomic Chair`, `Desk Mat`, `Monitor Wipe`) are strictly excluded.
- **Top 5 Most Expensive**: `Ergonomic Chair` ($200), `Mechanical Keyboard` ($150), `Laptop Stand` ($50), `Desk Lamp` ($50), `Footrest` ($50).

---

## Reproducible Setup & Run Instructions

### 1. Prerequisites
- **.NET 10 SDK** (v10.0.401 installed)
- **SQL Server 2022 Express** instance named `SQLEXPRESS` (or SQL Server LocalDB)

### 2. Connection String Configuration
- **For SQL Server Express (`.\SQLEXPRESS`)**:
  ```json
  "Server=.\\SQLEXPRESS;Database=WaterlilyInventoryDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  ```
- **For Windows LocalDB (`(localdb)\mssqllocaldb`)**:
  ```json
  "Server=(localdb)\\mssqllocaldb;Database=WaterlilyInventoryDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  ```

### 3. Step-by-Step CLI Commands

```powershell
# Step A: Restore pinned local dotnet-ef tool manifest
dotnet tool restore

# Step B: Restore NuGet dependencies
dotnet restore

# Step C: Build the solution
dotnet build

# Step D: Apply EF Core migrations (creates database, tables, and 5 stored procedures)
dotnet ef database update --project src/WaterlilyInventory.Web

# Step E: Seed demo dataset (idempotent — skips if records exist)
sqlcmd -S .\SQLEXPRESS -E -d WaterlilyInventoryDb -i src\WaterlilyInventory.Web\Data\Scripts\DemoSeedData.sql

# Step F: Run the application
dotnet run --project src/WaterlilyInventory.Web --urls "http://127.0.0.1:5126"
```

Open your browser to: **`http://127.0.0.1:5126`** (automatically redirects to `/products`).

---

## Application Routes & Endpoints

| Method | Route | Description | Expected HTTP Status |
|---|---|---|---|
| `GET` | `/` | Root redirect to `/products` | `302 Found` → `200 OK` |
| `GET` | `/products` | Product Inventory management page | `200 OK` (Razor View) |
| `GET` | `/products/data` | Product collection as JSON | `200 OK` (Cached 5 min) |
| `GET` | `/products/{id}/data` | Single product by ID | `200 OK` (CachedLong) or `404` |
| `POST` | `/products` | Create product from form data | `201 Created` with Location |
| `POST` | `/products/{id}/update` | Update existing product | `200 OK` (or `400`/`404`/`409`) |
| `POST` | `/products/{id}/delete` | Delete product | `200 OK` with `{ deletedId }` |
| `GET` | `/reports` | Analytics Reporting Dashboard page | `200 OK` (Razor View) |
| `GET` | `/reports/data?threshold=10`| All 5 report datasets as JSON | `200 OK` (Cached 5 min) |

---

## Structured Logging & Observability

- **Sink**: Structured rolling file logs written to `src/WaterlilyInventory.Web/Logs/log-.txt`.
- **Policy**: Daily rolling intervals, 14-day retention, 10MB per file limit, rolling file naming.
- **Log Contents**:
  - Request logging with HTTP method, path, status, and duration in ms.
  - Stored-procedure executions logging procedure name, parameters, execution duration in ms, and row count.
  - Cache invalidation events logging affected product IDs and cleared scopes.
  - Safe error details with correlation `traceId`. No credentials or passwords ever logged.

Inspect live logs in PowerShell:
```powershell
Get-Content -Tail 25 src/WaterlilyInventory.Web/Logs/log-*.txt
```

---

## Project Structure

```
WaterlilyInventory/
├── WaterlilyInventory.slnx                   # Solution file
├── global.json                               # Pinned .NET SDK 10.0.401
├── dotnet-tools.json                         # Pinned dotnet-ef 10.0.12
├── AGENTS.md                                 # Persistent project rules and constraints
├── README.md                                 # This documentation file
├── .gitignore                                # Standard exclusions (bin, obj, logs)
├── docs/
│   ├── ProjectPlan.md                        # Four-section architectural plan
│   ├── Architecture.md                       # Comprehensive design, lifecycles, and data flows
│   ├── BuildStatus.md                        # Stage-by-stage implementation tracker
│   ├── Verification.md                       # Complete live verification evidence
│   ├── TestingBacklog.md                     # Specification for deferred MSTest/Moq tests
│   ├── Migrations_Idempotent.sql             # Standalone SQL migration script
│   └── reference/
│       ├── assessment.pdf                    # Preserved original assessment
│       └── ui-theme.jpeg                     # Preserved frontend reference image
└── src/
    └── WaterlilyInventory.Web/
        ├── WaterlilyInventory.Web.csproj     # MVC Web Project (.NET 10)
        ├── Program.cs                        # Startup, DI registration, pipeline configuration
        ├── appsettings.json                  # Connection strings, Serilog, AppConfig
        ├── appsettings.Development.json
        ├── Common/
        │   ├── Exceptions/
        │   │   └── PersistenceException.cs   # Safe domain wrapper for persistence failures
        │   └── Results/
        │       ├── ApiResponse.cs            # Uniform JSON envelope
        │       ├── ApiResponseExtensions.cs  # Controller mapping extensions
        │       ├── OperationResult.cs        # Service outcome contract
        │       └── RepositoryWriteResult.cs  # Repository write outcome contract
        ├── Data/
        │   ├── ApplicationDbContext.cs       # Scoped EF Core context
        │   ├── DatabaseSeeder.cs             # Migration & seed helper
        │   ├── Configurations/
        │   │   └── ProductConfiguration.cs   # Fluent API mapping and check constraints
        │   ├── Migrations/
        │   │   ├── 20261011144950_InitialCreate.cs
        │   │   └── 20261011145854_AddStoredProcedures.cs
        │   └── Scripts/
        │       └── DemoSeedData.sql          # Idempotent demo dataset
        ├── Domain/
        │   └── Entities/
        │       ├── IEntity.cs                # Entity contract with integer identity PK
        │       └── Product.cs                # Product domain entity
        ├── DTOs/
        │   ├── Products/
        │   │   ├── CreateProductDto.cs       # Validated create input DTO
        │   │   ├── UpdateProductDto.cs       # Validated update input DTO
        │   │   ├── ProductDto.cs             # Immutable read-only product DTO
        │   │   └── DeleteProductResultDto.cs # Deletion outcome DTO
        │   └── Reports/
        │       ├── CategoryAveragePriceDto.cs
        │       ├── HighestStockValueCategoryDto.cs
        │       ├── CategoryInventoryValuationDto.cs
        │       ├── LowStockProductDto.cs
        │       ├── TopExpensiveProductDto.cs
        │       └── ReportDashboardDto.cs     # Dashboard aggregation DTO
        ├── Repositories/
        │   ├── Interfaces/
        │   │   ├── IGenericRepository.cs     # Generic async CRUD contract
        │   │   └── IProductRepository.cs     # Stored-procedure analytics contract
        │   └── Implementations/
        │       ├── GenericRepository.cs      # EF Core async generic implementation
        │       └── ProductRepository.cs      # Parameterized SP execution and logging
        ├── Services/
        │   ├── Interfaces/
        │   │   ├── IProductService.cs        # Product CRUD service contract
        │   │   └── IReportService.cs         # Reporting dashboard service contract
        │   └── Implementations/
        │       ├── ProductService.cs         # Validation, caching, delegates, invalidation
        │       └── ReportService.cs          # Sequential SP execution, threshold validation
        ├── Caching/
        │   ├── IAppCache.cs                  # Cache abstraction interface
        │   ├── AppCache.cs                   # IMemoryCache implementation with generation guard
        │   └── CacheKeys.cs                  # Typed, namespaced cache keys
        ├── Delegates/
        │   └── AsyncDataFactory.cs           # Generic async delegate callback
        ├── Filters/
        │   └── AjaxAntiforgeryFilter.cs      # Global CSRF filter returning JSON 400
        ├── Middleware/
        │   └── GlobalExceptionHandlingMiddleware.cs # Fallback unhandled error middleware
        ├── Controllers/
        │   ├── HomeController.cs             # Redirects GET / to /products
        │   ├── ProductsController.cs         # Thin product MVC and AJAX controller
        │   └── ReportsController.cs          # Thin reports MVC and AJAX controller
        ├── Views/
        │   ├── _ViewImports.cshtml
        │   ├── _ViewStart.cshtml
        │   ├── Shared/
        │   │   ├── _Layout.cshtml            # Themed layout shell (header, nav rail)
        │   │   └── Error.cshtml              # Safe MVC error view
        │   ├── Products/
        │   │   └── Index.cshtml              # Product table, search, filters, modals
        │   └── Reports/
        │       └── Index.cshtml              # 5 report panels, Chart.js, accessible tables
        └── wwwroot/
            ├── css/
            │   └── site.css                  # Custom theme variables and card styling
            ├── js/
            │   ├── site.js                   # Antiforgery AJAX setup, currency formatting
            │   ├── products.js               # Client filtering, sorting, pagination, CRUD
            │   └── reports.js                # Report rendering, Chart.js, race condition guard
            └── lib/                          # Pinned local client libraries
                ├── bootstrap/
                ├── jquery/
                ├── toastr/
                ├── sweetalert2/
                └── chartjs/
```

---

## Known Limitations & Next Steps

1. **Client-Side Filtering on Small Catalog**:
   - For this assessment dataset (13 products), search, category filtering, sorting, and pagination operate on the collection loaded via `GET /products/data`. On enterprise catalogs (> 10,000 items), server-side pagination with SQL `OFFSET/FETCH` would be introduced.
2. **Single Display Currency**:
   - Products are priced in a single configurable display currency (`USD`). Multi-currency conversion is not implemented.
3. **Automated Testing Suite (MSTest & Moq)**:
   - Full unit and component test suite implemented and verified in [`tests/WaterlilyInventory.UnitTests/`](tests/WaterlilyInventory.UnitTests/README.md). Covers real `ProductService`, `ReportService`, in-memory `AppCache`, deterministic clock expiration, and generation race protection (157 passed tests, 100% service line coverage). See [docs/TestingResults.md](docs/TestingResults.md) for full execution evidence.
