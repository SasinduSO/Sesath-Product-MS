# WaterlilyInventory — Project Plan

---

## 1. Understanding of the Requirement

### Original Assessment Requirements

The Waterlily Labs assessment requires building an **ASP.NET Core MVC inventory management application** with:

- A **Product** entity with Id, Name, Category, Price, and Stock fields.
- **SQL Server** with **Entity Framework Core Code-First** migrations.
- **Two stored-procedure reports** explicitly mentioned in the original brief:
  1. Average price per category.
  2. Highest stock value category (ambiguous wording in the brief — see approved interpretation below).
- **MVC Razor views** with a clean, professional UI.
- Demonstration of: dependency injection, repository pattern, delegates, async/await, stored procedures, and caching.

### Approved Enhancements and Interpretations

The following are **approved enhancements** beyond the original brief, agreed upon before implementation:

1. **Three additional reports** (five total):
   - Inventory valuation by category: `SUM(Price * Stock)` per category with total units.
   - Low-stock alert: products where `Stock < threshold` (default 10).
   - Top 5 most expensive products: `TOP(5) ORDER BY Price DESC, Id ASC`.

2. **"Highest Stock Value" interpretation:** The brief's wording is ambiguous. The approved interpretation is **highest monetary inventory value**: `SUM(Price * Stock)` per category. All tied leading categories are returned with deterministic ordering.

3. **Explicit CRUD outcomes:** All Service CRUD methods return `OperationResult<T>` with success/failure, error codes, validation errors, and messages.

4. **Memory caching with two modes:**
   - `Cached`: Absolute expiration ≤ 5 minutes.
   - `CachedLong`: No time-based expiration (in-memory only, lost on restart).

5. **Delegate callback pattern:** An `AsyncDataFactory<T>` delegate invoked on cache miss to separate cache policy from data retrieval.

6. **Global exception middleware** with structured Serilog logging.

7. **SweetAlert2** delete confirmations, **Toastr** notifications, **Chart.js** bar chart.

8. **Antiforgery validation** on all mutation endpoints.

9. **Category normalization** (trim + consistent casing) to prevent confusing report groups.

### Deferred (Required Later)

- **MSTest/Moq unit tests** — explicitly deferred to a later stage. Interfaces are preserved now to make testing straightforward.

### Explicitly Excluded

- Authentication/authorization
- Extra business entities or Category table
- External AI integrations
- CI/CD pipelines
- CQRS, MediatR, microservices
- Real-time push updates (SignalR)
- Product-name uniqueness constraint

---

## 2. Proposed Solution

### Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                    Browser/Client                    │
│   jQuery AJAX  ·  Bootstrap  ·  Chart.js  ·  Toastr │
└─────────────┬───────────────────────────┬────────────┘
              │ HTTP (JSON/HTML)          │
┌─────────────▼───────────────────────────▼────────────┐
│              ASP.NET Core MVC Pipeline                │
│   GlobalExceptionMiddleware → Routing → Controllers  │
├──────────────────────────────────────────────────────┤
│   ProductsController          ReportsController      │
│   (IProductService)           (IReportService)       │
├──────────────────────────────────────────────────────┤
│              Service Layer                            │
│   ProductService              ReportService          │
│   ├─ Validation                                      │
│   ├─ OperationResult<T> outcomes                     │
│   ├─ IAppCache (Cached / CachedLong)                 │
│   ├─ AsyncDataFactory<T> delegate callbacks          │
│   └─ Cache invalidation after successful writes      │
├──────────────────────────────────────────────────────┤
│              Repository Layer                         │
│   IGenericRepository<T>  ←  GenericRepository<T>     │
│   IProductRepository     ←  ProductRepository        │
│   ├─ CRUD with RepositoryWriteResult<T>              │
│   └─ 5 stored-procedure report methods               │
├──────────────────────────────────────────────────────┤
│              EF Core / SQL Server                     │
│   ApplicationDbContext                               │
│   Code-First Migrations (schema + stored procedures) │
│   SQL Server 2022 Express                            │
└──────────────────────────────────────────────────────┘
```

### Technology Choices

| Layer | Technology | Justification |
|---|---|---|
| Runtime | .NET 10 LTS (SDK 10.0.401) | Latest LTS; installed on dev machine |
| Web Framework | ASP.NET Core MVC | Assessment requirement |
| ORM | EF Core 10.x (SQL Server provider) | Code-First migrations, SP execution |
| Database | SQL Server 2022 Express (local) | Available on dev machine; Windows Auth |
| Logging | Serilog | Structured rolling text-file logs |
| Cache | IMemoryCache via IAppCache | In-process; two expiration modes |
| Frontend | jQuery + Bootstrap + Chart.js | Assessment requirement; CDN-free local assets |
| Notifications | Toastr, SweetAlert2 | UX requirement |

### Key Design Decisions

1. **Single MVC project** — No class library split. Clean folder separation by responsibility.
2. **OperationResult<T>** — Every Service CRUD method returns this. Stable error codes: `Validation`, `NotFound`, `Conflict`, `PersistenceFailure`.
3. **RepositoryWriteResult<T>** — Repository writes return explicit outcomes: `Created`, `Updated`, `Unchanged`, `Deleted`, `NotFound`, `Conflict`.
4. **Cache generation tracking** — A singleton `CacheScope` with generation counter prevents stale-read repopulation after invalidation.
5. **Antiforgery** — Global MVC filter validates tokens on all POST mutations.
6. **Category normalization** — Trim + title-case normalization in Service layer.

---

## 3. Potential Issues and Mitigation

| # | Issue | Impact | Mitigation |
|---|---|---|---|
| 1 | SQL Server Express not always available on all dev machines | Cannot run migrations or verify procedures | Detect at startup; provide clear error. LocalDB fallback documented. |
| 2 | Stale cache after write | User sees outdated product data | Invalidate product list, detail, and all report cache entries after successful write. Generation-based guard prevents concurrent stale repopulation. |
| 3 | Concurrent DbContext access | EF Core DbContext is not thread-safe | Never use `Task.WhenAll` on shared DbContext. Sequential awaits for report queries sharing same context. |
| 4 | Decimal overflow in inventory valuation | `Price * Stock` aggregation could overflow | Use `CAST(Stock AS BIGINT)` in SQL; use `decimal` in C# DTOs. Document supported range. |
| 5 | Tied highest-value categories | Only one winner shown | Return all tied categories; the stored procedure ranks and filters ties. |
| 6 | Duplicate form submissions | Product created twice | Disable submit button during AJAX flight; re-enable on completion. |
| 7 | Delete confirmation bypassed | Accidental data loss | SweetAlert2 confirmation required before AJAX DELETE call. |
| 8 | Antiforgery token missing/expired | POST rejected with HTML error | Normalize to JSON error envelope via filter; handle in AJAX error path. |
| 9 | Large product dataset performance | Client-side filtering won't scale | Document as small-dataset design. Server pagination is a future enhancement. |
| 10 | Migration and EnsureCreated conflict | Schema inconsistency | Never use EnsureCreated. Migrations only. |
| 11 | Sensitive data in logs | Security risk | Never log connection strings, credentials, or request bodies. Disable EF sensitive data logging. |
| 12 | Cache entry for non-existent product | Negative caching | Never cache null/not-found results. Only cache successful DTO data. |
| 13 | Post-commit cache invalidation failure | Misleading error to user | Separate persistence outcome from cache maintenance. Log secondary failure; return truthful persisted outcome. |

---

## 4. High-Level Pseudocode

### 4.1 Product CRUD — Create

```
FUNCTION CreateProductAsync(input: CreateProductDto, token: CancellationToken)
  → OperationResult<ProductDto>

  // Service-layer validation (independent of controller)
  errors = ValidateCreateInput(input)
  IF errors NOT EMPTY
    RETURN OperationResult.ValidationFailure(errors)

  // Normalize category: trim + title-case
  normalizedCategory = NormalizeCategory(input.Category)

  // Map DTO to entity
  product = new Product {
    Name = input.Name.Trim(),
    Category = normalizedCategory,
    Price = input.Price,
    Stock = input.Stock
  }

  // Repository create (includes SaveChangesAsync)
  writeResult = AWAIT repository.CreateAsync(product, token)

  IF writeResult.Status == Created
    productDto = MapToDto(writeResult.Entity)

    // Invalidate caches AFTER confirmed commit
    InvalidateProductAndReportCaches()

    RETURN OperationResult.Success(productDto, "Product created.")

  IF writeResult.Status == PersistenceFailure
    LOG warning with writeResult details
    RETURN OperationResult.PersistenceFailure("Failed to save product.")
```

### 4.2 Product CRUD — Update

```
FUNCTION UpdateProductAsync(id: int, input: UpdateProductDto, token: CancellationToken)
  → OperationResult<ProductDto>

  IF id <= 0
    RETURN OperationResult.ValidationFailure("Invalid product ID.")

  errors = ValidateUpdateInput(input)
  IF errors NOT EMPTY
    RETURN OperationResult.ValidationFailure(errors)

  // Repository loads authoritative DB state, applies changes, detects no-change
  writeResult = AWAIT repository.UpdateAsync(id, mappedValues, token)

  SWITCH writeResult.Status
    CASE Updated:
      productDto = MapToDto(writeResult.Entity)
      InvalidateProductAndReportCaches()
      RETURN OperationResult.Success(productDto, "Product updated.")

    CASE Unchanged:
      productDto = MapToDto(writeResult.Entity)
      // No invalidation needed — data hasn't changed
      RETURN OperationResult.Success(productDto, "No changes were needed.")

    CASE NotFound:
      RETURN OperationResult.NotFound($"Product {id} not found.")

    CASE Conflict:
      RETURN OperationResult.Conflict("Concurrent modification detected.")
```

### 4.3 Product CRUD — Delete

```
FUNCTION DeleteProductAsync(id: int, token: CancellationToken)
  → OperationResult<DeleteProductResultDto>

  IF id <= 0
    RETURN OperationResult.ValidationFailure("Invalid product ID.")

  writeResult = AWAIT repository.DeleteAsync(id, token)

  SWITCH writeResult.Status
    CASE Deleted:
      InvalidateProductAndReportCaches()
      RETURN OperationResult.Success(new DeleteProductResultDto(id), "Product deleted.")

    CASE NotFound:
      RETURN OperationResult.NotFound($"Product {id} not found.")
```

### 4.4 Product CRUD — Read (List and Single)

```
FUNCTION GetAllProductsAsync(token: CancellationToken)
  → OperationResult<IReadOnlyList<ProductDto>>

  // Cached mode: absolute 5-minute expiry
  // AsyncDataFactory<T> delegate is the callback invoked on cache miss
  products = AWAIT cache.CachedAsync(
    key: "products:all",
    factory: ASYNC (ct) => {
      entities = AWAIT repository.GetAllAsync(ct)  // cache miss → DB query
      RETURN entities.Select(MapToDto).ToList()
    },
    token
  )

  RETURN OperationResult.Success(products)


FUNCTION GetProductByIdAsync(id: int, token: CancellationToken)
  → OperationResult<ProductDto>

  IF id <= 0
    RETURN OperationResult.ValidationFailure("Invalid product ID.")

  // CachedLong mode: no time expiry, survives until invalidation or restart
  product = AWAIT cache.CachedLongAsync(
    key: $"products:detail:{id}",
    factory: ASYNC (ct) => {
      entity = AWAIT repository.GetByIdAsync(id, ct)
      RETURN entity != null ? MapToDto(entity) : null
    },
    token
  )

  IF product == null
    // Do NOT cache null — the factory returns null, cache skips storage
    RETURN OperationResult.NotFound($"Product {id} not found.")

  RETURN OperationResult.Success(product)
```

### 4.5 Five Stored Procedures

```
-- 1. Average Price per Category
CREATE PROCEDURE dbo.usp_Product_AveragePriceByCategory
AS
  SET NOCOUNT ON
  SELECT Category, AVG(Price) AS AveragePrice
  FROM dbo.Products
  GROUP BY Category
  ORDER BY Category

-- 2. Highest Stock Value Category (all ties)
CREATE PROCEDURE dbo.usp_Product_HighestStockValueCategory
AS
  SET NOCOUNT ON
  ;WITH CategoryValues AS (
    SELECT Category,
           SUM(CAST(Stock AS BIGINT)) AS TotalStockUnits,
           SUM(Price * CAST(Stock AS BIGINT)) AS InventoryValue
    FROM dbo.Products
    GROUP BY Category
  )
  SELECT Category, TotalStockUnits, InventoryValue
  FROM CategoryValues
  WHERE InventoryValue = (SELECT MAX(InventoryValue) FROM CategoryValues)
  ORDER BY Category

-- 3. Inventory Valuation by Category
CREATE PROCEDURE dbo.usp_Product_InventoryValuationByCategory
AS
  SET NOCOUNT ON
  SELECT Category,
         SUM(CAST(Stock AS BIGINT)) AS TotalStockUnits,
         SUM(Price * CAST(Stock AS BIGINT)) AS InventoryValue
  FROM dbo.Products
  GROUP BY Category
  ORDER BY Category

-- 4. Low-Stock Alert
CREATE PROCEDURE dbo.usp_Product_LowStock
  @Threshold INT
AS
  SET NOCOUNT ON
  IF @Threshold < 0 SET @Threshold = 0
  SELECT Id, Name, Category, Price, Stock
  FROM dbo.Products
  WHERE Stock < @Threshold
  ORDER BY Stock ASC, Id ASC

-- 5. Top Five Most Expensive Products
CREATE PROCEDURE dbo.usp_Product_TopFiveMostExpensive
AS
  SET NOCOUNT ON
  SELECT TOP (5) Id, Name, Category, Price, Stock
  FROM dbo.Products
  ORDER BY Price DESC, Id ASC
```

### 4.6 Caching — Hit, Miss, Expiry, and Invalidation

```
FUNCTION CachedAsync<T>(key: string, factory: AsyncDataFactory<T>, token)
  → Task<T>

  // Capture current cache generation before lookup
  generationAtStart = cacheScope.CurrentGeneration

  IF cache.TryGetValue(key, out cachedEntry)
    IF cachedEntry.Generation == generationAtStart
      RETURN cachedEntry.Value   // ✓ Cache HIT

  // Cache MISS — invoke the delegate callback to fetch from DB
  freshData = AWAIT factory(token)

  // Only publish if generation hasn't been rotated during our fetch
  IF cacheScope.CurrentGeneration == generationAtStart
    cache.Set(key, new CacheEntry(freshData, generationAtStart),
              absoluteExpiration: 5 minutes)

  RETURN freshData


FUNCTION CachedLongAsync<T>(key: string, factory: AsyncDataFactory<T>, token)
  → Task<T>

  // Same pattern but with NO time-based expiration
  // Priority set to NeverRemove to prevent compaction eviction
  generationAtStart = cacheScope.CurrentGeneration

  IF cache.TryGetValue(key, out cachedEntry)
    IF cachedEntry.Generation == generationAtStart
      RETURN cachedEntry.Value

  freshData = AWAIT factory(token)

  IF freshData == null
    RETURN null   // Do NOT cache null results

  IF cacheScope.CurrentGeneration == generationAtStart
    cache.Set(key, new CacheEntry(freshData, generationAtStart),
              priority: NeverRemove, noExpiration: true)

  RETURN freshData


FUNCTION InvalidateProductAndReportCaches()
  // Called ONLY after confirmed successful write
  // Rotates generation so stale in-flight reads won't repopulate
  cacheScope.Rotate()   // Increments generation, cancels change token

  // Remove known product keys
  cache.Remove("products:all")
  cache.Remove("products:detail:*")   // All individual product entries

  // Remove all report cache entries (including all threshold variants)
  cache.Remove("reports:*")
```

### 4.7 Delegate Callback Flow

```
// Declaration: A delegate representing an async factory method that
// retrieves data of type T from a data source (e.g., repository).
// This separates cache policy from data retrieval logic.
public delegate Task<T> AsyncDataFactory<T>(CancellationToken cancellationToken);

// Usage in Service (cache miss triggers the callback):
var products = await _cache.CachedAsync<IReadOnlyList<ProductDto>>(
    key: CacheKeys.ProductList,
    // This callback runs ONLY on a cache miss.
    // It queries the repository and maps entities to immutable DTOs.
    factory: async (ct) =>
    {
        var entities = await _repository.GetAllAsync(ct);
        return entities.Select(e => e.ToDto()).ToList().AsReadOnly();
    },
    cancellationToken
);
```

### 4.8 Error Handling Flow

```
REQUEST ARRIVES
  │
  ▼
GlobalExceptionHandlingMiddleware.InvokeAsync()
  │
  TRY
  │  ▼
  │  MVC Pipeline → Controller → Service → Repository
  │
  │  Service handles expected errors:
  │    - Validation → OperationResult.ValidationFailure (→ HTTP 400)
  │    - Not found  → OperationResult.NotFound           (→ HTTP 404)
  │    - Conflict   → OperationResult.Conflict            (→ HTTP 409)
  │    - DB failure → OperationResult.PersistenceFailure  (→ HTTP 500)
  │      (logged once in Service)
  │
  │  Controller maps OperationResult → HTTP status + JSON envelope
  │
  CATCH (Exception ex)  // Unexpected programming errors only
  │  Generate traceId
  │  LOG.Error(ex, "Unhandled exception {TraceId}", traceId)
  │  IF response not started
  │    RETURN JSON { isSuccess: false, message: "An unexpected error occurred.",
  │                  traceId: traceId } with HTTP 500
  │  ELSE
  │    // Response already started; cannot rewrite
  │    LOG.Warning("Response already started for {TraceId}")
  │
  CATCH (OperationCanceledException)
  │  // Client disconnected — not an application error
  │  LOG.Information("Request cancelled")
  │  // Do not return 500
```

### 4.9 No-Change Update Path

```
FUNCTION Repository.UpdateAsync(id, values, token)
  entity = AWAIT dbContext.Products.FindAsync(id, token)
  IF entity == null
    RETURN RepositoryWriteResult.NotFound()

  // Apply permitted fields
  oldName = entity.Name
  oldCategory = entity.Category
  oldPrice = entity.Price
  oldStock = entity.Stock

  entity.Name = values.Name
  entity.Category = values.Category
  entity.Price = values.Price
  entity.Stock = values.Stock

  // Detect actual changes via EF change tracker
  IF dbContext.Entry(entity).State == EntityState.Unchanged
    RETURN RepositoryWriteResult.Unchanged(entity)

  rowsAffected = AWAIT dbContext.SaveChangesAsync(token)
  IF rowsAffected > 0
    RETURN RepositoryWriteResult.Updated(entity)
  ELSE
    // Changed in tracker but 0 rows affected — investigate
    RETURN RepositoryWriteResult.Conflict()
```

---

## Implementation Stages

| Stage | Prompt | Deliverables |
|---|---|---|
| 0 | Prompt 0 | AGENTS.md, ProjectPlan.md, Architecture.md, BuildStatus.md, environment detection |
| 1 | Prompt 1 | Solution, MVC project, Product entity, DTOs, result types, DbContext, initial migration |
| 2 | Prompt 2 | Repositories, 5 stored procedures, seed data, migration verification |
| 3 | Prompt 3 | Services, caching, delegates, Serilog, exception middleware |
| 4 | Prompt 4 | Controllers, routes, themed layout, Razor shells, asset loading |
| 5 | Prompt 5 | Full AJAX CRUD, all 5 report panels, Chart.js, complete frontend |
| 6 | Prompt 6 | Run instructions, smoke-check evidence, handoff documentation |
