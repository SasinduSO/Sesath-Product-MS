# WaterlilyInventory — Enterprise Inventory Management System

An inventory management application built with **ASP.NET Core MVC (.NET 10 LTS)**, **Entity Framework Core**, and **Microsoft SQL Server 2022**.

The system provides catalog management, real-time client-side AJAX interactions, bulk spreadsheet imports (.CSV and Excel .XLSX) with pre-import validation previews, multi-tiered in-memory caching with delegate callbacks, structured rolling file logging, and SQL Server stored-procedure analytics.

---

## Key Features

- **Product Inventory Management**:
  - Full CRUD operations executed asynchronously via jQuery AJAX without full-page reloads.
  - Client-side live search, category filtering, stock status indicators (In Stock, Low Stock, Out of Stock), dynamic column sorting, and pagination.
  - Strict input validation enforcing boundary constraints, allowed categories, and decimal precision.
- **Bulk Spreadsheet Import (.CSV & Excel .XLSX)**:
  - Ingest product data in bulk via RFC 4180 CSV or OpenXML Excel (`.xlsx`) files.
  - Zero-dependency spreadsheet parser utilizing .NET BCL `ZipArchive` and `XDocument`.
  - Built-in sample CSV generator with demo data and blank templates.
  - Interactive validation preview: automatically segregates acceptable records from rejected records with field-level cell error highlighting.
  - User review and confirmation workflow prior to batch database persistence.
- **SQL Server Analytics & Reporting**:
  - Category Average Price (`AVG(Price)`).
  - Highest Inventory Value Category (evaluating cumulative valuation with deterministic ranking).
  - Total Category Valuation with Chart.js bar visualizations and accessible data tables.
  - Low-Stock Alerts with configurable unit thresholds (`Stock < @Threshold`).
  - Top 5 Most Expensive Products (`ORDER BY Price DESC, Id ASC`).
- **Resilient Caching Strategy**:
  - Multi-tier caching abstraction (`IAppCache`) utilizing `IMemoryCache`.
  - 5-minute absolute expiration for list and report queries; indefinite caching for detail queries.
  - Atomic generation tokens to eliminate stale concurrent reads.
  - Post-commit cache invalidation upon confirmed database writes.
- **Enterprise Observability & Security**:
  - Structured daily rolling file logging powered by Serilog with correlation `traceId`.
  - Global exception middleware returning uniform JSON envelopes or friendly error views without exposing database details.
  - Anti-forgery validation (`RequestVerificationToken`) across all state-mutating requests.

---

## Technology Stack

| Layer | Component | Technology / Version |
|---|---|---|
| **Runtime & Framework** | Target Framework | .NET 10 LTS (`net10.0`, C# 13) |
| **Web Presentation** | Server Framework | ASP.NET Core MVC with Razor Views |
| **ORM & Persistence** | Data Access | Entity Framework Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`) |
| **Database** | Database Engine | Microsoft SQL Server 2022 Express (`.\SQLEXPRESS`) |
| **Logging** | Structured Logging | Serilog (`Serilog.AspNetCore`, `Serilog.Sinks.File`) |
| **Frontend Utilities** | Client Libraries | jQuery 3.7.1, Bootstrap 5.3.3 |
| **UI Components** | Notifications & Modals | Toastr 2.1.4, SweetAlert2 11.14.5 |
| **Visualizations** | Charting Engine | Chart.js 4.4.6 |
| **Test Automation** | Unit & Component Tests | MSTest v4, Moq 4.20 |

---

## Architecture & Design Patterns

The application follows clean architectural principles with strict unidirectional data flow:

```
Browser Client (jQuery AJAX / HTML5)
  ↓
Controllers (HTTP routing, model validation, status code mapping)
  ↓
Service Layer (Domain validation, outcome mapping, caching policy, delegate callbacks)
  ↓
Repository Layer (Entity queries, writes, parameterized stored procedures)
  ↓
Entity Framework Core & SQL Server 2022
```

### Core Architecture Rules

1. **Strict Layer Separation**:
   - Controllers and Views never access `DbContext`, repositories, or memory cache directly.
   - Services never reference `HttpContext`, `IActionResult`, or EF query APIs.
2. **Asynchronous Call Chains**:
   - Every database I/O operation is asynchronous (`async/await`) and propagates a `CancellationToken`.
3. **Explicit Outcome Contracts**:
   - Service operations return `OperationResult<T>` with explicit outcomes (`Success`, `ValidationFailure`, `NotFound`, `Conflict`, `PersistenceFailure`).
   - Repository writes return `RepositoryWriteResult<T>` (`Created`, `Updated`, `Unchanged`, `Deleted`, `NotFound`, `Conflict`).
   - Web API endpoints return a standardized JSON envelope:
     ```json
     {
       "isSuccess": true,
       "data": { ... },
       "message": "Operation completed successfully.",
       "errorCode": null,
       "validationErrors": null,
       "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00"
     }
     ```
4. **Caching & Asynchronous Delegate Callbacks**:
   - Service layer passes an `AsyncDataFactory<T>` delegate callback to `IAppCache`.
   - Delegates execute only on cache misses within the active request scope, preventing singleton capture of scoped repository instances.
   - Affected cache entries are invalidated only after confirmed database commits.

---

## Business Logic & Data Specifications

### Product Entity

| Field | Type | Rules & Constraints |
|---|---|---|
| `Id` | `int` | Primary Key, Identity (auto-incrementing) |
| `Name` | `string` | Required, maximum 150 characters, non-blank after trimming, alphanumeric with spaces and hyphens only |
| `Category` | `string` | Required, maximum 100 characters, restricted to allowed catalog categories (`Accessories`, `Electronics`, `Office Furniture`, `Stationery`) |
| `Price` | `decimal(18,2)` | Required, zero or positive, maximum 2 decimal places |
| `Stock` | `int` | Required, non-negative whole integer |

### Category Normalization
- Category strings are trimmed, internal whitespaces collapsed, and formatted to Title Case.
- Historical alias *"Stationary"* is standardized to *"Stationery"* to prevent fragmented grouping in stored-procedure reports.

### Bulk Import Workflow
1. **Extraction**: Accepts `.csv` or `.xlsx` files up to 10MB. Strips currency symbols (`$`, `€`, `£`) and thousand separators.
2. **Validation**: Evaluates each row against domain rules. Identifies missing fields, special characters, excessive decimal precision, and negative values.
3. **Preview**: Displays summary counts (Total, Acceptable, Rejected) and highlights individual invalid cells.
4. **Approval**: Users review acceptable items and confirm import via SweetAlert2. Valid records are persisted in a single transaction via `CreateRangeAsync`, followed by cache invalidation.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (SDK `10.0.401` or later)
- [Microsoft SQL Server 2022](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (Express edition instance named `SQLEXPRESS` or LocalDB)
- `sqlcmd` utility (included with SQL Server command-line tools)

### Configuration

Connection strings and branding options are configured in `src/WaterlilyInventory.Web/appsettings.json`:

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

> **Note:** For LocalDB, set `"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WaterlilyInventoryDb;Trusted_Connection=True;MultipleActiveResultSets=true"`.

---

### Step-by-Step Installation

Execute the following commands from the repository root:

#### 1. Restore Tooling & Dependencies
```powershell
# Restore local dotnet-ef manifest
dotnet tool restore

# Restore NuGet packages
dotnet restore
```

#### 2. Build Solution
```powershell
dotnet build
```

#### 3. Apply Database Migrations
Creates the `WaterlilyInventoryDb` database, schema, and all five analytical stored procedures:
```powershell
dotnet ef database update --project src/WaterlilyInventory.Web
```

#### 4. Seed Demo Data (Optional)
Populate the database with initial catalog products:
```powershell
sqlcmd -S .\SQLEXPRESS -E -d WaterlilyInventoryDb -i src\WaterlilyInventory.Web\Data\Scripts\DemoSeedData.sql
```

#### 5. Launch Application
```powershell
dotnet run --project src/WaterlilyInventory.Web
```

Once started, navigate to:
- **Product Inventory**: `http://localhost:5267/products`
- **Analytics Dashboard**: `http://localhost:5267/reports`

---

## API & Route Reference

| Method | Route | Description | Response Type |
|---|---|---|---|
| `GET` | `/` | Root URL; redirects to `/products` | `302 Found` |
| `GET` | `/products` | Product inventory management page | HTML Razor View |
| `GET` | `/products/data` | Retrieves all catalog products | JSON (`ApiResponse<IReadOnlyList<ProductDto>>`) |
| `GET` | `/products/{id}/data` | Retrieves single product details | JSON (`ApiResponse<ProductDto>`) |
| `POST` | `/products` | Creates a new product | JSON (`ApiResponse<ProductDto>`), `201 Created` |
| `POST` | `/products/{id}/update` | Updates an existing product | JSON (`ApiResponse<ProductDto>`), `200 OK` |
| `POST` | `/products/{id}/delete` | Deletes a product by ID | JSON (`ApiResponse<DeleteProductResultDto>`), `200 OK` |
| `GET` | `/products/sample-csv` | Downloads sample CSV template (`?withDemoData=true\|false`) | File (`text/csv`) |
| `POST` | `/products/bulk-preview` | Validates uploaded spreadsheet | JSON (`ApiResponse<BulkProductPreviewDto>`) |
| `POST` | `/products/bulk-import` | Commits approved records | JSON (`ApiResponse<BulkProductImportResultDto>`) |
| `GET` | `/reports` | Analytics dashboard page | HTML Razor View |
| `GET` | `/reports/data?threshold=10` | Executes stored procedures and returns report datasets | JSON (`ApiResponse<ReportDashboardDto>`) |

---

## Automated Test Suite

The solution includes an automated test suite located in `tests/WaterlilyInventory.UnitTests`.

- **Framework**: MSTest with Moq mocking.
- **Coverage**: 167 unit and component tests verifying domain validation boundaries, CRUD operations, concurrency handling, cache generation guards, spreadsheet parsing (CSV/XLSX), and stored-procedure reporting logic.

Run the test suite from the repository root:
```powershell
dotnet test
```

---

## Observability & Logging

- **Logging Provider**: Serilog structured logging.
- **Destination**: Daily rolling text files located in `src/WaterlilyInventory.Web/Logs/log-.txt`.
- **Policy**: 14-day retention limit, 10MB file size ceiling with rollover.
- **Audit Points**:
  - HTTP requests with endpoint path, status codes, and latency in milliseconds.
  - Stored-procedure executions logging procedure name, parameter values, execution time, and record count.
  - Cache hits, misses, and post-commit invalidation scopes.
  - Masked errors accompanied by correlation `traceId`. Credentials and connection secrets are strictly suppressed.

---

## Project Structure

```
WaterlilyInventory/
├── WaterlilyInventory.slnx                   # Solution file
├── global.json                               # Pinned .NET SDK version
├── dotnet-tools.json                         # Local EF Core CLI tooling
├── README.md                                 # Documentation
├── src/
│   └── WaterlilyInventory.Web/
│       ├── Common/
│       │   ├── Exceptions/                   # Domain persistence exceptions
│       │   └── Results/                      # Standardized response envelopes & outcome types
│       ├── Data/
│       │   ├── ApplicationDbContext.cs       # EF Core DbContext
│       │   ├── Configurations/               # Entity type configurations & Fluent API mappings
│       │   ├── Migrations/                   # EF Core Code-First schema & stored procedure migrations
│       │   └── Scripts/                      # Idempotent demo database seeding script
│       ├── Domain/
│       │   └── Entities/                     # Core domain entities (Product, IEntity)
│       ├── DTOs/
│       │   ├── Products/                     # Product input, output, and bulk import DTOs
│       │   └── Reports/                      # Stored-procedure reporting DTOs
│       ├── Caching/                          # IAppCache implementation and cache policies
│       ├── Delegates/                        # Generic async data factory delegate callbacks
│       ├── Repositories/                     # Generic repository and product-specific data layer
│       ├── Services/                         # Business logic, validation, bulk import, and reporting
│       ├── Middleware/                       # Global exception handling middleware
│       ├── Filters/                          # Global AJAX anti-forgery filter
│       ├── Controllers/                      # MVC and AJAX API controllers
│       ├── Views/                            # Razor Views (Products, Reports, Shared layout)
│       └── wwwroot/                          # Client assets (CSS, JS, Bootstrap, Toastr, Chart.js)
└── tests/
    └── WaterlilyInventory.UnitTests/         # MSTest suite covering Services, Caching, and Parsing
```

---

## License

This project is licensed under the MIT License.
