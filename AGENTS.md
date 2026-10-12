# WaterlilyInventory — Project Rules (AGENTS.md)

> This file persists the approved project requirements and working rules.
> Every stage must read this file and docs/BuildStatus.md before making changes.
> Do not record secrets in this file.

---

## Project Identity

- **Code/Project Name:** WaterlilyInventory
- **Display Brand:** SESATH PRODUCTS (configurable in appsettings)
- **Currency:** USD (configurable; single display currency for all products)
- **Solution Root:** this workspace
- **Application Project:** `src/WaterlilyInventory.Web`

---

## Architecture Rules

### Data-Access Direction

```
Controller → Service Interface → Repository Interface → EF Core / SQL Server
```

- Controllers and Views must **never** access DbContext, repositories, SQL, or memory cache directly.
- Services own business validation, results, caching decisions, and callback coordination.
- Repositories own all runtime product database access.
- Application startup may register infrastructure and perform explicit setup tasks.

### Async/Await

- Use `async/await` for every actual database I/O operation (queries, stored-procedure execution, commits).
- Pass `CancellationToken` through the full call chain.
- **Prohibited:** `.Result`, `.Wait()`, `async void`, `Task.Run` to disguise synchronous I/O.
- EF tracking operations (`Add`, `Update`, `Remove`) may be synchronous — they don't perform I/O.
- Do not run concurrent queries on one `DbContext`.

### Repository Pattern

- Use `IGenericRepository<T>` plus `IProductRepository`.
- Keep the generic interface small; materialize queries inside repositories.
- Do not expose `IQueryable` or `DbContext` across the repository boundary.
- Repository writes return `RepositoryWriteResult<T>` with explicit outcomes.

### Service Layer

- All Service CRUD methods return `OperationResult<T>`.
- **Prohibited:** `void`, `Task` without outcome, bare `bool` for CRUD contracts.
- Framework-required signatures (`OnModelCreating`, migration `Up`/`Down`) keep required return types.
- Services handle validation, not-found, persistence, and conflict outcomes.
- Unexpected defects reach global exception middleware.

### Caching

- Use `IMemoryCache` in a reusable `IAppCache` abstraction consumed by Services.
- **Cached mode:** Absolute expiration ≤ 5 minutes. Repeated hits do not extend.
- **CachedLong mode:** No time-based expiration within the running cache.
- Neither mode survives application restart.
- Use both modes in real application paths.
- Invalidate affected product and report entries only after confirmed successful writes.

### Delegates

- Use an explicitly named generic async data-factory delegate (`AsyncDataFactory<T>`) invoked on cache miss.
- Must be an actual invoked callback, not decorative.

---

## Technology Stack

| Component | Choice |
|---|---|
| Framework | .NET 10 LTS (SDK 10.0.401) |
| Web | ASP.NET Core MVC with Razor Views |
| ORM | Entity Framework Core (Code-First migrations) |
| Database | SQL Server 2022 Express (`(.\SQLEXPRESS)`) |
| Logging | Serilog (structured rolling text-file logs) |
| Frontend JS | jQuery AJAX |
| UI | Bootstrap, custom CSS |
| Notifications | Toastr |
| Confirmations | SweetAlert2 |
| Charts | Chart.js |

---

## Entity: Product

| Field | Type | Constraints |
|---|---|---|
| Id | int | Identity PK |
| Name | string | Required, max 150, nonblank after trim |
| Category | string | Required, max 100, nonblank after trim |
| Price | decimal(18,2) | ≥ 0, reject excessive precision |
| Stock | int | ≥ 0 |

- Category is a plain string — no Category table.
- No unique-product-name requirement.

---

## Five Stored-Procedure Reports

1. **Average Price per Category:** `AVG(Price)` — not stock-weighted.
2. **Highest Stock Value Category:** Greatest `SUM(Price * Stock)` per category. Return all tied leaders with deterministic ordering. *Approved interpretation of the brief's ambiguous wording.*
3. **Inventory Valuation:** `SUM(Price * Stock)` for every category, plus total stock units.
4. **Low-Stock Alert:** Products where `Stock < @Threshold` (default 10). Stock = threshold is **not** low stock.
5. **Top 5 Most Expensive:** `TOP(5)`, `ORDER BY Price DESC, Id ASC`. Return fewer than 5 when fewer exist.

All calculations run in SQL stored procedures. Report presentation may format/sort returned data and sum category valuations for a grand total but must **not** replace stored-procedure logic.

---

## Pages

Exactly two main pages:
1. **Product List** — CRUD via jQuery AJAX, search, category filter, stock badges.
2. **Report** — All five report panels, Chart.js bar chart, accessible table alternatives.

Initial page load and navigation between pages use normal MVC navigation. All CRUD interactions use AJAX.

---

## Error Handling

- `GlobalExceptionHandlingMiddleware` in `Middleware/` — registered in `Program.cs`.
- Returns safe errors; never exposes stack traces, SQL, or connection strings.
- JSON endpoints receive consistent failure envelope; page requests get error page.
- Give each exception one logging owner — do not duplicate full-exception logs.

---

## Project Organization

```
src/WaterlilyInventory.Web/
├── Domain/Entities/
├── Data/Configurations/
├── Data/Migrations/
├── Repositories/Interfaces/
├── Repositories/Implementations/
├── Services/Interfaces/
├── Services/Implementations/
├── DTOs/Products/
├── DTOs/Reports/
├── Common/Results/
├── Common/Exceptions/
├── Caching/
├── Delegates/
├── Middleware/
├── Controllers/
├── ViewModels/
├── Views/Products/
├── Views/Reports/
├── Views/Shared/
└── wwwroot/
```

Add `Filters/` or `Extensions/` only when there is real code for them.

---

## Commenting Rules

- Concise XML `<summary>` comments for public application methods, interfaces, and important infrastructure.
- Normal comments only for non-obvious business logic, ordering constraints, design choices.
- Identify concepts at point of use: DI, repository, delegate, stored procedure, async I/O, cache expiration/invalidation.
- Use `<inheritdoc/>` where repeating a contract adds no information.
- Do not comment every obvious assignment or loop.

---

## Working Rules

- Do not overwrite unrelated work, drop a database, delete user data, or store passwords in committed configuration.
- Use user-secrets/environment variables for credentials.
- Do not use `EnsureCreated` as a migration replacement or alongside the migration workflow.
- Do not auto-apply migrations on every web request.
- **Not needed:** Authentication, extra entities, AI integrations, CI/CD, CQRS, MediatR, microservices.
- **Deferred:** MSTest/Moq tests (a later required stage). Preserve testable interfaces now.

---

## Theme Direction

From the inspected reference image:
- Pale cool-grey background (`#F2F5F9`), white cards, navy primary (`#123F8A`)
- Blue charts (`#2DA9E1`), subtle borders/shadows, white top header
- Narrow left navigation rail, rounded panels, clear table rows
- Green/amber/red stock badges (restrained)
- Do not copy the reference's placeholder data, incorrect badges, or decorative non-functional controls.
- Do not label the application "real-time" unless real-time updates exist.
