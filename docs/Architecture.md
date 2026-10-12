# WaterlilyInventory — Architecture Document

---

## Solution Structure

```
WaterlilyInventory/                          ← Solution root
├── WaterlilyInventory.sln
├── global.json
├── AGENTS.md                                ← Project rules (read before every stage)
├── README.md
├── .gitignore
├── docs/
│   ├── ProjectPlan.md
│   ├── Architecture.md                      ← This file
│   ├── BuildStatus.md
│   └── reference/
│       ├── assessment.pdf                   ← Original assessment (preserved)
│       └── ui-theme.jpeg                    ← Frontend reference image (preserved)
├── Waterlily_Antigravity_Prompt_Pack.md      ← Implementation prompts (preserved)
└── src/
    └── WaterlilyInventory.Web/              ← Single MVC application project
        ├── WaterlilyInventory.Web.csproj
        ├── Program.cs
        ├── appsettings.json
        ├── appsettings.Development.json
        │
        ├── Domain/
        │   └── Entities/
        │       ├── IEntity.cs               ← Minimal int-Id contract for generic repo
        │       └── Product.cs               ← Product entity (Id, Name, Category, Price, Stock)
        │
        ├── Data/
        │   ├── ApplicationDbContext.cs       ← DbSet<Product>, applies configurations
        │   ├── Configurations/
        │   │   └── ProductConfiguration.cs   ← IEntityTypeConfiguration<Product>
        │   └── Migrations/
        │       ├── InitialCreate             ← Product table, constraints, index
        │       └── AddStoredProcedures       ← Five report stored procedures
        │
        ├── Repositories/
        │   ├── Interfaces/
        │   │   ├── IGenericRepository.cs      ← Small async CRUD contract
        │   │   └── IProductRepository.cs      ← Extends generic + 5 report methods
        │   └── Implementations/
        │       ├── GenericRepository.cs       ← EF Core implementation
        │       └── ProductRepository.cs       ← SP execution + generic CRUD
        │
        ├── Services/
        │   ├── Interfaces/
        │   │   ├── IProductService.cs         ← CRUD returning OperationResult<T>
        │   │   └── IReportService.cs          ← Dashboard returning OperationResult<ReportDashboardDto>
        │   └── Implementations/
        │       ├── ProductService.cs          ← Validation, caching, delegates, invalidation
        │       └── ReportService.cs           ← Report orchestration with caching
        │
        ├── DTOs/
        │   ├── Products/
        │   │   ├── CreateProductDto.cs        ← Input: Name, Category, Price?, Stock?
        │   │   ├── UpdateProductDto.cs        ← Input: Name, Category, Price?, Stock?
        │   │   ├── ProductDto.cs              ← Output: Id, Name, Category, Price, Stock
        │   │   └── DeleteProductResultDto.cs  ← Output: deleted Id
        │   └── Reports/
        │       ├── CategoryAveragePriceDto.cs
        │       ├── HighestStockValueCategoryDto.cs
        │       ├── CategoryInventoryValuationDto.cs
        │       ├── LowStockProductDto.cs
        │       ├── TopExpensiveProductDto.cs
        │       └── ReportDashboardDto.cs      ← Aggregates all 5 report collections
        │
        ├── Common/
        │   ├── Results/
        │   │   ├── OperationResult.cs         ← Service-layer outcome
        │   │   └── RepositoryWriteResult.cs   ← Repository persistence outcome
        │   └── Exceptions/
        │       └── PersistenceException.cs    ← Wraps EF/SQL failures for Service layer
        │
        ├── Caching/
        │   ├── IAppCache.cs                  ← CachedAsync<T>, CachedLongAsync<T>
        │   ├── AppCache.cs                   ← IMemoryCache implementation
        │   └── CacheKeys.cs                  ← Centralized typed/namespaced keys
        │   └── CacheScope.cs                 ← Generation-based stale-data guard
        │
        ├── Delegates/
        │   └── AsyncDataFactory.cs           ← Named generic async delegate
        │
        ├── Middleware/
        │   └── GlobalExceptionHandlingMiddleware.cs
        │
        ├── Controllers/
        │   ├── ProductsController.cs          ← Product page + AJAX data endpoints
        │   └── ReportsController.cs           ← Report page + AJAX data endpoint
        │
        ├── ViewModels/                        ← View-specific models if needed
        │
        ├── Views/
        │   ├── _ViewImports.cshtml
        │   ├── _ViewStart.cshtml
        │   ├── Shared/
        │   │   ├── _Layout.cshtml             ← Themed shell: header, nav rail, content
        │   │   └── Error.cshtml
        │   ├── Products/
        │   │   └── Index.cshtml               ← Product list page (AJAX-driven)
        │   └── Reports/
        │       └── Index.cshtml               ← Report dashboard page
        │
        └── wwwroot/
            ├── css/
            │   └── site.css                  ← Theme styles
            ├── js/
            │   ├── site.js                   ← Shared utilities (AJAX helpers, antiforgery)
            │   ├── products.js                ← Product CRUD logic
            │   └── reports.js                ← Report loading + Chart.js
            └── lib/
                ├── jquery/
                ├── bootstrap/
                ├── sweetalert2/
                ├── toastr/
                └── chart.js/
```

---

## Data Flow

### Read Flow (e.g., GET /products/data)

```
Browser
  → GET /products/data
  → ProductsController.GetProductData()
  → IProductService.GetAllProductsAsync(ct)
  → IAppCache.CachedAsync(key, factory, ct)
     ├─ Cache HIT (generation matches): return cached DTOs
     └─ Cache MISS: invoke AsyncDataFactory<T> callback
        → IProductRepository.GetAllAsync(ct)
        → EF Core AsNoTracking query → SQL Server
        → Materialize to List<Product>
        → Map to IReadOnlyList<ProductDto>
        → Store in cache with 5-min absolute expiry
  → OperationResult<IReadOnlyList<ProductDto>>.Success(data)
  → Controller maps to HTTP 200 + JSON envelope
  → Browser receives { isSuccess: true, data: [...] }
```

### Write Flow (e.g., POST /products — Create)

```
Browser
  → POST /products (form data + antiforgery token)
  → ProductsController.Create(dto)
  → IProductService.CreateProductAsync(dto, ct)
     1. Validate input (Service boundary)
     2. Normalize category
     3. Map DTO → Product entity
     4. IProductRepository.CreateAsync(entity, ct)
        → context.Products.Add(entity)          // sync — no I/O
        → await context.SaveChangesAsync(ct)     // async — actual I/O
        → return RepositoryWriteResult.Created(entity)
     5. Map entity → ProductDto
     6. Invalidate caches (AFTER confirmed commit)
        → cacheScope.Rotate() — increment generation
        → remove product list, detail, report entries
     7. Return OperationResult.Success(productDto)
  → Controller maps to HTTP 201 + JSON envelope + Location header
  → Browser shows Toastr success, refetches product list via AJAX
```

---

## Result Conventions

### OperationResult<T> (Service → Controller)

```csharp
public class OperationResult<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Message { get; }
    public string? ErrorCode { get; }        // Validation, NotFound, Conflict, PersistenceFailure
    public IDictionary<string, string[]>? ValidationErrors { get; }

    // Factory methods
    public static OperationResult<T> Success(T data, string? message = null);
    public static OperationResult<T> ValidationFailure(string message, IDictionary<string, string[]>? errors = null);
    public static OperationResult<T> NotFound(string message);
    public static OperationResult<T> Conflict(string message);
    public static OperationResult<T> PersistenceFailure(string message);
}
```

The Service result **never** contains HTTP status codes or MVC types.

### RepositoryWriteResult<T> (Repository → Service)

```csharp
public class RepositoryWriteResult<T>
{
    public WriteOutcome Outcome { get; }     // Created, Updated, Unchanged, Deleted, NotFound, Conflict
    public T? Entity { get; }

    // Factory methods for each outcome
}
```

Unexpected persistence errors throw `PersistenceException` (wrapping the original EF/SQL exception as inner).

### HTTP Response Envelope (Controller → Browser)

```json
{
  "isSuccess": true|false,
  "data": { ... } | [ ... ] | null,
  "message": "Human-readable outcome",
  "errorCode": null | "Validation" | "NotFound" | "Conflict" | "PersistenceFailure",
  "validationErrors": null | { "fieldName": ["Error message"] },
  "traceId": "optional-correlation-id"
}
```

| Service Outcome | HTTP Status |
|---|---|
| Success (create) | 201 Created |
| Success (read/update/delete/unchanged) | 200 OK |
| ValidationFailure | 400 Bad Request |
| NotFound | 404 Not Found |
| Conflict | 409 Conflict |
| PersistenceFailure | 500 Internal Server Error |
| Unhandled exception (middleware) | 500 Internal Server Error |

---

## Dependency Lifetimes

| Registration | Lifetime | Rationale |
|---|---|---|
| `ApplicationDbContext` | **Scoped** | One context per request; EF Core is not thread-safe |
| `IGenericRepository<T>` / `IProductRepository` | **Scoped** | Depends on scoped DbContext |
| `IProductService` / `IReportService` | **Scoped** | Depends on scoped repositories |
| `IAppCache` | **Singleton** | Wraps singleton IMemoryCache; shares cache across requests |
| `CacheScope` | **Singleton** | Generation counter must be globally visible |
| `IMemoryCache` | **Singleton** | Framework default; in-process memory |
| `GlobalExceptionHandlingMiddleware` | **Transient** (middleware) | Instantiated per request by the pipeline |
| `Serilog.ILogger` | **Singleton** | Serilog's static logger; DI provides `ILogger<T>` wrappers |

> **Critical rule:** Singleton dependencies must **never** capture scoped services (DbContext, repositories) in their constructors. The `IAppCache` receives the `AsyncDataFactory<T>` delegate *at call time* from the scoped Service; it does not store scoped dependencies.

---

## Caching Strategy

### Two Modes

| Mode | Method | Expiration | Used For |
|---|---|---|---|
| **Cached** | `CachedAsync<T>` | Absolute 5 min | Product list, all report datasets |
| **CachedLong** | `CachedLongAsync<T>` | None (until invalidation or restart) | Individual product detail by Id |

### Cache Key Structure

```
product:list                           → IReadOnlyList<ProductDto>
product:id:{id}                        → ProductDto
report:avg-price                       → IReadOnlyList<CategoryAveragePriceDto>
report:highest-value                   → IReadOnlyList<HighestStockValueCategoryDto>
report:valuation                       → IReadOnlyList<CategoryInventoryValuationDto>
report:low-stock:{threshold}           → IReadOnlyList<LowStockProductDto>
report:top-expensive                   → IReadOnlyList<TopExpensiveProductDto>
```

### Invalidation Rules

- Shared product and report invalidation scope invoked via `IAppCache.InvalidateProductAndReports(productId)`.
- Triggered **only after** confirmed successful write (`Created`, `Updated`, `Deleted`).
- **Unchanged** updates do **not** invalidate.
- **Failed** writes do **not** invalidate.
- Invalidation advances an atomic `_generation` counter via `Interlocked.Increment` and evicts product list, product detail, and all report entries (including every threshold variant).
- **Generation Guard**: Each cache lookup captures the current generation before invoking the `AsyncDataFactory<T>` delegate. Freshly loaded data is committed to the cache only if the generation has not changed during loading, preventing a slow concurrent read started before a write from repopulating the cache with stale data.
- **Post-Commit Isolation**: If a secondary cache invalidation throws, it is caught and logged; the primary confirmed persistence outcome is preserved and never disguised as a save failure.

---

## Stored Procedure Mapping

| Procedure | DTO | Execution Method |
|---|---|---|
| `dbo.usp_Product_AveragePriceByCategory` | `CategoryAveragePriceDto` | `Database.SqlQuery<TDto>` |
| `dbo.usp_Product_HighestStockValueCategory` | `HighestStockValueCategoryDto` | `Database.SqlQuery<TDto>` |
| `dbo.usp_Product_InventoryValuationByCategory` | `CategoryInventoryValuationDto` | `Database.SqlQuery<TDto>` |
| `dbo.usp_Product_LowStock` | `LowStockProductDto` | `Database.SqlQuery<TDto>` with `@Threshold` parameter |
| `dbo.usp_Product_TopFiveMostExpensive` | `TopExpensiveProductDto` | `Database.SqlQuery<TDto>` |

All procedures use `SET NOCOUNT ON`, explicit column aliases matching DTO properties, schema-qualified table names (`dbo.Products`), and deterministic `ORDER BY`.

---

## Error Handling Ownership

```
Layer              │ Handles                              │ Logs
───────────────────┼──────────────────────────────────────┼──────────────────
Repository         │ Translates EF/SQL → PersistenceEx    │ Debug-level context
                   │ Translates concurrency → Conflict    │
───────────────────┼──────────────────────────────────────┼──────────────────
Service            │ Validation errors                    │ Warning for
                   │ Not-found                            │ persistence failures
                   │ Persistence failures (from repo)     │ (logged once here)
                   │ Conflict (from repo)                 │
───────────────────┼──────────────────────────────────────┼──────────────────
Controller         │ Maps OperationResult → HTTP status   │ None (Service logged)
───────────────────┼──────────────────────────────────────┼──────────────────
Middleware         │ Unhandled exceptions only             │ Error with traceId
                   │ Returns safe JSON/error page          │ (single log point)
```

Each exception gets **one logging owner**. The Service logs persistence failures. The middleware logs unhandled exceptions. Controllers do not re-log Service outcomes.
