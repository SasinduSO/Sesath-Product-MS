# Waterlily Inventory — Antigravity implementation prompt pack

Target model: Claude Opus 4.6, where available in your Antigravity account.

This pack implements the approved application from an empty folder in seven controlled stages. It includes the frontend direction from your uploaded reference. Automated MSTest/Moq tests are deliberately deferred, not removed from the assessment requirements.

## How to use this pack

1. Create a folder named `WaterlilyInventory` and open that folder in Antigravity.
2. Make the assessment PDF and your reference JPEG available in that workspace, or attach them to the agent conversation. ChatGPT workspace paths are not paths on your computer. The agent will organize copies under `docs/reference/`.
3. Select Claude Opus 4.6 if it is available. Use the agent conversation that can edit workspace files and run terminal commands.
4. Paste **Prompt 0** first. After it finishes, paste **Prompt 1**, then continue in numerical order through **Prompt 6**.
5. Copy only the text inside each prompt block. Do not send all seven prompts together. The agent should implement the current stage, verify it, update the status document, and stop at that stage boundary.
6. Keep the same conversation when practical. If you start a new conversation, use the recovery prompt near the end of this document before continuing.
7. Let the agent finish its build and verification work. If a stage reports a failure, use the repair prompt before advancing. Supply only information it cannot discover, such as your SQL Server connection details or a prerequisite installation requiring your interaction.

| Send | Purpose | Required outcome |
| --- | --- | --- |
| Prompt 0 | Establish project rules, plan, and reference interpretation | Saved project rules, approved-scope plan, environment findings |
| Prompt 1 | Scaffold MVC and implement Domain/Data contracts | Buildable project, Product model, DTOs, DbContext, initial migration |
| Prompt 2 | Implement repositories and SQL reports | Async persistence, five stored procedures, reproducible database setup |
| Prompt 3 | Implement application behaviour | Services, results, delegates, memory caching, logging, exception handling |
| Prompt 4 | Connect MVC endpoints and visual layout | Controller routes, consistent JSON responses, styled two-page shell |
| Prompt 5 | Finish the frontend | Working AJAX CRUD and all five real-data reports |
| Prompt 6 | Run and verify the application | Documented run instructions, smoke-check evidence, accurate handoff |

## Approved decisions

- ASP.NET Core MVC with controller-served Razor views; do not replace MVC with Razor Pages handlers.
- Two main pages: Product List and Report. The four panels in the reference image are visual inspiration, not a requirement for four routes.
- SQL Server with EF Core Code-First migrations.
- Highest stock value means **highest monetary inventory value: `SUM(Price * Stock)` per category**. This is an explicit interpretation of the brief's ambiguous wording.
- Implement the two required calculations plus the three requested report additions.
- In-memory caching in the Service layer, with no time expiry for CachedLong and an absolute five-minute maximum for Cached.
- Minimal automatic product/report cache invalidation after a successful write is approved.
- Explicit CRUD outcomes, Service-layer handling of expected errors, and middleware for unexpected errors.
- A single well-organized MVC project initially; separate class libraries are not required.
- MSTest/Moq implementation comes later. Build checks and application smoke checks are included now.

## Prompt 0 — Establish the rules and implementation plan

```text
Act as a Senior .NET Architect and full-stack developer. You are implementing my Waterlily Labs assessment directly in the currently opened workspace, using Claude Opus 4.6. I have created the initial folder. Work in logical, buildable stages, and execute the work requested in each prompt rather than only describing what I could do.

THIS STAGE
Inspect the workspace and environment, save the project rules and project plan, and prepare the next stage. Do not generate the whole application now. Stop after this stage's deliverables are complete.

REFERENCE MATERIAL
Read the Waterlily assessment PDF and inspect the supplied frontend reference image if they are available. Preserve the originals. Organize copies as docs/reference/assessment.pdf and docs/reference/ui-theme.jpeg where practical. If a reference is inaccessible, state that limitation; do not pretend you inspected it. The approved requirements below still provide enough information to start.

APPROVED SCOPE
Build an ASP.NET Core MVC inventory application with exactly two main pages: Product List and Report. Product has Id, Name, Category, Price, and Stock. Keep Category as a string; there is no Category table. Use SQL Server, Entity Framework Core, Code-First migrations, and dependency injection.

Data-access direction is Controller -> Service interface -> Repository interface -> EF Core/SQL Server. Controllers and views must never access DbContext, repositories, SQL, or memory cache directly. Services own business validation, results, caching decisions, and callback coordination. Repositories own all runtime product database access. Application startup may register infrastructure and perform explicit setup tasks.

Use async/await for every actual database I/O operation, including queries, stored-procedure execution, and commits. Pass CancellationToken through the full chain. Do not use .Result, .Wait(), async void, or Task.Run to disguise synchronous I/O. EF tracking operations such as Add, Update, and Remove may be synchronous because they do not themselves perform database I/O. Do not run concurrent queries on one DbContext.

Use an asynchronous IGenericRepository<T> plus IProductRepository. Keep the generic interface small and materialize queries inside repositories. Do not expose IQueryable or DbContext across the Repository boundary.

Implement five stored-procedure reports:
1. Average price per category: AVG(Price), not a stock-weighted average.
2. Highest stock value category: greatest SUM(Price * Stock) per category. Return all tied leading categories with deterministic ordering. Document this approved interpretation explicitly.
3. Inventory valuation: SUM(Price * Stock) for every category, with total stock units alongside it.
4. Low-stock alert: products where Stock < an integer threshold; default 10. Stock equal to the threshold is not low stock.
5. Top five most expensive products: TOP (5), Price DESC, Id ASC. Return fewer than five when fewer exist.

All five calculations run in SQL stored procedures and are mapped to typed DTOs in the Repository. Do not calculate substitute reports in JavaScript or in the Service. Report presentation may format, sort already-returned display data where appropriate, and sum category valuations for a grand total; it must not replace the stored-procedure logic.

Use IMemoryCache in a reusable abstraction consumed by Services. Implement the required Cached and CachedLong modes through async generic methods, with conventional Async suffixes where suitable. Cached expires no later than five minutes after insertion. CachedLong has no time-based expiration within the running cache. Neither survives application restart. Use both modes in real application paths. Invalidate affected product and report entries only after confirmed successful writes. This small internal invalidation feature is approved.

Demonstrate delegates as meaningful callback methods in the Service/caching flow. Prefer an explicitly named generic asynchronous data-factory delegate that is invoked on a cache miss and returns Task<T>. Explain its purpose at its declaration and call site. Do not introduce unnecessary events or fire-and-forget callbacks.

All Service CRUD methods return explicit OperationResult<T> outcomes. Do not use void, Task without an outcome, or a bare success Boolean for these application CRUD contracts. Framework-required signatures such as OnModelCreating and migration Up/Down retain their required return types. Never use async void.

Services handle expected validation, not-found, persistence, and conflict outcomes. Unexpected defects reach global exception middleware. Register the middleware in Program.cs, keep its implementation in Middleware/, and return safe errors. Use Serilog for structured rolling local text-file logs, including stored-procedure executions and unhandled errors. Give each exception one logging owner.

Use MVC Razor views in Views/, not @page/PageModel handlers. Use jQuery AJAX for product list reads and all create, edit, details, and delete interactions. The initial page load and navigation between the two pages may use normal MVC navigation. Use Bootstrap/custom CSS, SweetAlert2 delete confirmation, and Toastr notifications. Use Chart.js for the report bar chart with accessible table alternatives. Keep third-party dependencies small, pinned, and documented; use their official distributions rather than copied application code.

THEME
Use the reference image as visual direction: pale cool-grey background, white cards, navy primary actions, blue charts, subtle borders and shadows, a white top header, a narrow left navigation rail, rounded panels, clear table rows, and restrained green/amber/red stock badges. The display brand is SESATH PRODUCTS, configurable in appsettings. The code/project name is WaterlilyInventory. Currency display defaults to USD to match the reference and is configurable; all products use that single display currency.

Use real data and correct English labels. Do not copy the reference's placeholder numbers, incorrect stock badges, inconsistent price ranking, or decorative controls that have no function. Four illustrated panels must fit the two required pages. Do not label the application real-time unless real-time updates actually exist.

PROJECT ORGANIZATION
Create the solution in this root and one MVC application under src/WaterlilyInventory.Web. Keep Domain/Entities, Data/Configurations, Data/Migrations, Repositories/Interfaces, Repositories/Implementations, Services/Interfaces, Services/Implementations, DTOs/Products, DTOs/Reports, Common/Results, Common/Exceptions, Caching, Delegates, Middleware, Controllers, ViewModels, Views/Products, Views/Reports, Views/Shared, and wwwroot organized by responsibility. Add Filters/ or Extensions/ only when there is real code for them. Avoid empty decorative abstractions and unnecessary projects.

COMMENTING
Write concise XML summary comments for public application methods, interfaces, and important infrastructure methods. Explain return semantics and meaningful parameters. Use normal comments only for non-obvious business logic, ordering constraints, and design choices. Identify the important concepts where they are used: dependency injection, repository, delegate callback, stored procedure, async I/O, cache expiration, and cache invalidation. Explain why they are used. Do not comment every obvious assignment or loop. Use inheritdoc where repeating a contract adds no information. Preserve generated-file conventions for migrations.

WORKING RULES
Prefer .NET 10 LTS and matching supported EF Core packages. Inspect installed SDKs and choose an installed concrete .NET 10 SDK version for global.json when available. Use compatible stable package versions and record them. Do not silently switch to an unsupported framework or a non-SQL-Server database.

Use an existing local SQL Server installation where available. On Windows, LocalDB is a suitable development default if installed. On other systems, inspect available SQL Server endpoints or a supported container runtime before choosing a route; do not assume a SQL Server image works on every CPU architecture. Detect prerequisites and give exact actionable instructions for anything unavailable.

Do not overwrite unrelated work, drop a database, delete user data, or store passwords in committed configuration. Use user-secrets/environment variables for credentials. Use EnsureCreated neither as a migration replacement nor alongside the migration workflow. Do not auto-apply migrations on every web request.

No authentication system, extra business entities, external AI integrations, CI/CD pipelines, CQRS, MediatR, or microservices are needed. Do not implement MSTest/Moq tests or create a test project yet. Preserve interfaces that make later Service tests straightforward. Build and smoke-check the current work instead.

Persist these requirements in a root AGENTS.md, scoped to this project. Also create docs/BuildStatus.md. Future stages must read these files and inspect the current code before changing anything. Record completed stages, decisions, commands actually executed, pass/fail results, blockers, and the next stage. Do not record secrets.

BEFORE CODING
Create docs/ProjectPlan.md with exactly these four main sections:
1. Understanding of the Requirement
2. Proposed Solution
3. Potential Issues and Mitigation
4. High-Level Pseudocode
Cover CRUD success/failure paths, no-change updates, all five procedures, caching hit/miss/expiry/invalidation, delegates, and error handling in understandable high-level pseudocode. Distinguish original assessment requirements from approved enhancements. Note that formal MSTest/Moq coverage is a later required stage.

Create docs/Architecture.md with the proposed folder responsibilities, data flow, result conventions, and dependency lifetimes. Read any existing project plan first and reconcile it with these approved decisions rather than deleting it blindly.

COMPLETION
Report the detected OS, SDK and SQL Server status, chosen setup path, files created, explicit assumptions, and any real blocker. Stop here. Prompt 1 will authorize application scaffolding and the Domain/Data implementation.
```

## Prompt 1 — MVC setup, Product, DTOs, and DbContext

```text
Continue the approved WaterlilyInventory project. Read AGENTS.md, docs/ProjectPlan.md, docs/Architecture.md, and docs/BuildStatus.md, then inspect existing files. Implement only this stage. Preserve the established decisions and folder structure.

GOAL
Create a buildable ASP.NET Core MVC application with the Domain/Data layers, shared contracts, dependency registration, and an initial EF Core migration. Do not implement the full repositories, services, reports, or finished UI yet.

SETUP
Create the solution at the root and the MVC project under src/WaterlilyInventory.Web using the official SDK MVC template as a starting skeleton. Remove unrelated sample navigation/content when appropriate. Use the real project/solution names consistently in every command. Pin an available .NET 10 SDK and compatible stable EF Core SQL Server, Design, and local dotnet-ef versions. Prefer a local tool manifest so another developer can reproduce the tooling. Do not fabricate package versions.

Enable nullable reference types and implicit usings. Add a suitable .gitignore for bin, obj, local logs, development secrets, and IDE temporary files. Keep required migrations and dependency manifests in source control. Add README.md with the commands verified so far.

ENTITY AND SQL MAPPING
Implement Product with Id:int, Name:string, Category:string, Price:decimal, Stock:int. Use an identity primary key; Name is required with maximum length 150, Category is required with maximum length 100, Price is decimal(18,2), and Stock is an integer. Name and Category must be nonblank after trimming. Allow Price = 0 and Stock = 0; reject negatives. Reject excessive price precision rather than silently rounding a submitted value. Do not invent a unique-product-name requirement or add a Category table.

Keep SQL configuration in ProductConfiguration implementing IEntityTypeConfiguration<Product>, applied by ApplicationDbContext. Configure required fields, lengths, precision, non-negative check constraints, and a useful Category index. Add DbSet<Product>. Do not put business workflows inside DbContext or entity getters.

Use a small IEntity contract exposing the integer Id only if the generic repository will need it. Avoid a large generic base entity or unrequested audit columns.

DTO AND RESULT CONTRACTS
Create separate CreateProductDto, UpdateProductDto, ProductDto, and DeleteProductResultDto. UpdateProductDto should not allow changing the primary key; the route Id is authoritative. Input DTOs must distinguish an omitted required numeric value from an intentional zero, using nullable numeric inputs with required validation or an equally explicit approach. Use the same constraints in server validation and the eventual form fields.

ProductDto exposes Id, Name, Category, Price, and Stock. Prefer immutable/read-only output DTOs suitable for caching. Do not return tracked EF entities directly to the browser.

Create these report DTO contracts:
- CategoryAveragePriceDto: Category, AveragePrice.
- HighestStockValueCategoryDto: Category, TotalStockUnits, InventoryValue.
- CategoryInventoryValuationDto: Category, TotalStockUnits, InventoryValue.
- LowStockProductDto: Id, Name, Category, Price, Stock.
- TopExpensiveProductDto: Id, Name, Category, Price, Stock.
- ReportDashboardDto: the five corresponding collections and the selected low-stock threshold. Highest-value results are a collection because ties are supported.

Use long for aggregated stock quantities and decimal for money. Align future SQL aliases and result types exactly with these DTOs. Do not create report DTO tables through migrations; prefer unmapped SqlQuery<T> results later.

Create OperationResult<T> with IsSuccess, Data, Message, ErrorCode, and ValidationErrors. Success and failure factory methods must produce consistent states. Use stable error codes for Validation, NotFound, Conflict, and PersistenceFailure. Represent unexpected failures safely at the HTTP boundary. An optional trace identifier can be added in the web response without making Services depend on HttpContext. Reads of an empty collection are successful; a requested single product that does not exist is NotFound.

Plan a separate small RepositoryWriteResult<T> contract with explicit Created, Updated, Unchanged, Deleted, NotFound, and Conflict outcomes; unexpected persistence errors can use a documented PersistenceException defined outside EF-specific code. The Service result contract must not contain HTTP status codes or MVC types. Finalize these shapes in docs/Architecture.md so later stages reuse them.

DATABASE CONFIGURATION
Register ApplicationDbContext with UseSqlServer and a scoped lifetime through constructor injection. Set up a clear named connection string. If Windows LocalDB is available, a password-free local development connection is acceptable. If credentials are needed, configure them through user-secrets or environment variables. Never print them in handoff text or commit them.

Generate InitialCreate with the actual EF tooling and store migrations in Data/Migrations. Inspect the generated migration for exactly the intended Product table, constraints, and index. Do not use EnsureCreated. Apply the migration to the agreed development database if SQL Server is available. Do not create a SQLite or in-memory substitute if it is not.

VERIFICATION AND STOP
Run restore and build. If the database is available, run the migration update and confirm that the schema exists. A build alone is not evidence that SQL Server or migrations work. Record any database blocker accurately and provide the next action needed; continue all unblocked work in this stage.

Update docs/BuildStatus.md and README.md with actual commands, schema assumptions, completed deliverables, and any remaining blockers. Explain the roles of the entity, DTOs, DbContext, migration, and scoped dependency lifetime briefly. Stop before Prompt 2.
```

## Prompt 2 — Async repositories, stored procedures, and database readiness

```text
Continue from the existing project. Read AGENTS.md and the project/status/architecture documents. Inspect the actual entity, DTOs, DbContext, and migrations. Implement this stage using those contracts; do not duplicate them under new names.

GOAL
Implement real asynchronous repositories and all five SQL Server report procedures. Make the database reproducible through migrations, and verify the results against SQL Server when it is available.

GENERIC REPOSITORY
Implement IGenericRepository<T> and GenericRepository<T> for the existing entity contract. Keep only the needed operations: get all, get by Id, create, update, and delete. Database reads return Task<IReadOnlyList<T>> or Task<T?>; writes return Task<RepositoryWriteResult<T>>. All actual database I/O and commits are asynchronous and accept CancellationToken. Use AsNoTracking for read-only queries. Materialize before returning. Do not expose IQueryable.

Choose one explicit save boundary: each repository write method completes its own SaveChangesAsync before returning its persistence outcome. Do not add a separate public save operation that Services might forget to call, and do not save twice per write.

For create, only return Created after a successful commit and return the generated Id. For update and delete, load authoritative database state; never rely on cached existence. Return NotFound for missing records. For update, apply the submitted permitted values, detect whether anything actually changed, and return Unchanged when the stored values already match. Do not treat every SaveChangesAsync result of zero as a failed update. A changed write with an unexpected outcome must be investigated/represented as failure rather than optimistically returning success.

Do not attach multiple instances with the same key to one DbContext. Do not overwrite the primary key. Translate DbUpdateConcurrencyException to a documented conflict outcome. Translate expected database failures to the project's PersistenceException with the original exception preserved as an inner exception; do not leak EF/SQL exception details to the UI. Cancellation must propagate as cancellation. Do not swallow exceptions and return null, an empty collection, or success.

PRODUCT REPOSITORY
IProductRepository extends IGenericRepository<Product> and adds one asynchronous method per report, including an integer threshold parameter for low stock. ProductRepository implements the custom report methods and uses the generic CRUD implementation. Register repositories with scoped lifetimes; keep interface-to-implementation mapping unambiguous.

STORED PROCEDURES
Add a generated migration for the SQL objects. Define these procedures under dbo:
1. usp_Product_AveragePriceByCategory
2. usp_Product_HighestStockValueCategory
3. usp_Product_InventoryValuationByCategory
4. usp_Product_LowStock
5. usp_Product_TopFiveMostExpensive

Each procedure must use SET NOCOUNT ON, explicit columns/aliases, schema-qualified table names, and deterministic ordering. Use the exact table and column names created by the model.

Average price groups by Category and uses AVG(Price). Preserve appropriate decimal precision and leave display rounding to the UI. Highest stock value groups by Category and ranks SUM(Price * Stock), returning every category tied at the maximum. Inventory valuation returns every category's monetary inventory value and total units. Cast Stock to bigint before SUM so aggregation does not overflow int. Ensure monetary multiplication/aggregation uses SQL decimal arithmetic and maps into C# decimal; document the supported range and fail safely rather than overflowing or converting to float.

Low stock accepts @Threshold int, validates non-negative values defensively, and filters strictly Stock < @Threshold. Return zero-stock products when the threshold is positive. Order by Stock ASC then Id ASC. Threshold zero returns no products because negative stock is prohibited. Top five uses TOP (5) and ORDER BY Price DESC, Id ASC; do not use WITH TIES here because the requirement is at most five products.

Do not concatenate user input into SQL. Execute with Database.SqlQuery<TDto> and safe interpolation/parameters, or FromSqlRaw with explicit SqlParameter and correctly configured keyless DTOs if needed. Prefer the unmapped SqlQuery<TDto> route with the current EF version. Call ToListAsync directly on the stored-procedure query. Do not compose server-side Where, OrderBy, FirstOrDefault, Single, or Take over EXEC; materialize first when selecting an item in memory.

Put the authoritative SQL definitions in migrations so a fresh database receives everything through dotnet ef database update. Handle CREATE/ALTER PROCEDURE batch requirements correctly, including idempotent migration script execution. Do not include GO inside a command sent through migrationBuilder.Sql. Implement Down to drop only the procedures introduced by this migration. Avoid a second unsynchronized set of SQL definitions outside migrations.

Add structured ILogger messages around stored-procedure execution: procedure name, safe parameters such as threshold, elapsed time, row count, and outcome. Serilog configuration comes in the next stage; dependency-injected ILogger calls should already compile. Do not log credentials or sensitive connection strings. Coordinate failure logging with the Service/middleware ownership rule to avoid repeating full exceptions at every layer.

DATA AND VERIFICATION
Apply pending migrations to the agreed development database and verify all five procedures exist. Generate an idempotent migration script and check the procedure batch syntax; if possible execute it twice against a separately created, disposable development database whose name is explicitly recorded. Never repurpose or drop an existing database.

Provide a clearly named optional demo seed script or development-only seeder with at least twelve products across four categories, including zero stock, stock 9/10/11, different prices, and ties. It must run only against the intended development database and refuse to overwrite existing product data. No application screen may use a hardcoded mock-data array in place of a functioning database.

Record the actual sample calculations for average price, valuation, threshold behaviour, and price ordering so later frontend verification has known expected values. Verify empty-data behaviour in a disposable context if practical; do not delete user data for a check. If SQL Server is blocked, record which checks were not executed and do not claim the procedures work merely because the application builds.

Run build, update README.md and docs/BuildStatus.md, explain the generic/specific repository split and stored-procedure mapping, and stop before Prompt 3. Do not add automated unit tests.
```

## Prompt 3 — Services, delegates, caching, logs, and error handling

```text
Read AGENTS.md and the project documents, inspect the implemented Repository contracts and DTOs, and continue this existing project. Implement only this stage and reuse the established result types.

GOAL
Implement IProductService/ProductService, IReportService/ReportService, reusable memory caching, explicit asynchronous delegate callbacks, Serilog, and global exception handling. Keep the application buildable and prepare it for the MVC endpoints in Prompt 4.

SERVICE CONTRACTS AND VALIDATION
ProductService exposes asynchronous list, get-by-Id, create, update, and delete methods, all returning OperationResult<T> and accepting CancellationToken. ReportService exposes an asynchronous dashboard method accepting the low-stock threshold and returning OperationResult<ReportDashboardDto>. Inject IProductRepository, the cache abstraction, and ILogger through constructors. Services never reference HttpContext, IActionResult, DbContext, SQL commands, or EF query APIs.

Validate create/update input again at the Service boundary so correctness does not depend on an MVC action having run. Reject blank or overlong trimmed Name/Category, missing numeric inputs, negative Price/Stock, invalid price precision/range, invalid Ids, and invalid thresholds. Keep shared validation rules in one place where practical; do not create inconsistent validation rules in controllers, services, and HTML. Define a simple documented category normalization policy so casing/whitespace do not create confusing report groups. Do not add category-management functionality or product-name uniqueness.

Map only allowed input fields to entities. For updates and deletes use Repository outcomes based on authoritative database reads. Return ProductDto for create/update, a list/single ProductDto for reads, and DeleteProductResultDto containing the deleted Id for delete. Return success for an unchanged update with a clear message such as 'No changes were needed.' A missing single product is NotFound; an empty list is a successful empty list. Do not label a database failure as 'No products found.'

Catch and translate documented persistence failures in the Service. Return safe error messages and stable error codes, and log the diagnostic exception once. Translate known conflicts to Conflict. Propagate CancellationToken cancellation without turning a user-disconnected request into an application error. Let unexpected programming errors reach middleware. Do not catch Exception everywhere and silently return a default result.

DELEGATE CALLBACK
Define a named generic delegate such as AsyncDataFactory<T>(CancellationToken) returning Task<T>. The generic cache methods accept this delegate, and the Service supplies an async callback that calls Repository methods and maps their results to DTOs. The cache invokes the callback only on a miss. This must be an actual invoked callback, not an unused demonstration type.

Add a concise declaration comment explaining that a delegate represents a callable method and separates cache policy from data retrieval. At the Service call site, explain why this particular callback is passed and when it runs. Await it normally. Never capture a scoped DbContext or Repository inside a singleton for later background execution; execute the supplied callback within the active request.

MEMORY CACHE
Implement IAppCache and its IMemoryCache implementation with CachedAsync<T> (the required Cached mode) and CachedLongAsync<T> (the required CachedLong mode). The first uses absolute expiration of five minutes; repeated hits do not extend that deadline. The second has no absolute/sliding time expiration. Keep long-lived entries from ordinary priority-based compaction if needed to match the intended lifetime, and document that process restart/cache disposal still loses them.

Use CachedAsync for product lists and reports, and CachedLongAsync for individual product reads. Cache successful DTO data rather than OperationResult failures, tracked EF entities, or deferred queries. A null product/missing record must not be cached. An empty successful collection may be cached. Use immutable DTOs/collections or safe snapshots so callers cannot mutate shared cached values.

Use centralized, typed/namespaced keys. Include product Ids and report parameters such as threshold. Both ProductService and ReportService must share one product-related cache invalidation scope. On a confirmed Created/Updated/Deleted outcome, invalidate product list, detail, and all affected report entries, including every previously used threshold variant. Unchanged updates do not require invalidation; failed writes must not invalidate data.

Prevent a slow read that started before a write from repopulating the current cache with stale data after invalidation. A small singleton cache-scope component using a generation plus a change token is suitable: capture the scope before lookup/load, invalidate/rotate it after commit, and never publish old-generation data under the current generation. Do not keep an unbounded dictionary of locks or create a global lock around all database I/O. Concurrent independent cache misses may each query their own scoped Repository if that keeps the implementation clear. Handle token lifetime and thread safety correctly.

Keep invalidation small and internal; no admin page or general-purpose cache platform. Cache infrastructure can be singleton, but scoped Services/Repositories/DbContext must not be captured by singleton dependencies. Add comments explaining these lifetimes and the commit-before-invalidate ordering.

Separate the persistence outcome from work after commit. If a cache-maintenance/notification step fails after data was committed, do not return a misleading 'save failed' result or retry the insert. Log the actual secondary failure and preserve the truthful persisted outcome.

REPORT SERVICE
Validate threshold before Repository calls. Retrieve/cache each of the five report datasets through its Repository method and assemble ReportDashboardDto. Await database calls sequentially when they share the request's DbContext; do not use Task.WhenAll on it. Do not replace the SQL calculations with LINQ grouping. If a required report fails, return a clear failure rather than reporting complete success with fabricated zeroes.

SERILOG
Install compatible stable Serilog packages and configure them cleanly from Program.cs/configuration. Use dependency-injected ILogger in application code. Write structured logs to a local application-relative Logs directory with daily rolling text files, a sensible size limit, and bounded retention. A JSON-lines text format or a template that retains structured properties is acceptable. Include request/trace identifiers where available and use structured message templates rather than string interpolation for values.

Enable request logging and preserve the Repository's procedure name, safe parameters, duration, row count, and outcome fields. Use an intentional log level policy. Do not enable sensitive-data logging by default. Flush the logger on shutdown. Keep log files out of source control and verify that a real event reaches a file.

GLOBAL ERRORS
Implement GlobalExceptionHandlingMiddleware in Middleware/ and register it early enough in Program.cs to wrap subsequent application request processing. Handle unexpected errors once, include a trace identifier in the log, and return a safe failure response. JSON endpoints receive the established failure envelope with HTTP 500; page requests may receive a simple MVC-compatible error page. Do not expose stack traces, SQL, connection strings, or internal exception text. Do not attempt to rewrite a response that has already started. Avoid conflicting/duplicate exception-handling pipelines.

Expected Service failures are handled by the Service and mapped to HTTP in the next stage. Middleware is the fallback for exceptions, not a mechanism for making every failed result become HTTP 200. Client cancellation should not create a false unhandled-server-error log.

VERIFICATION AND STOP
Build, verify dependency registrations/lifetimes, and run the app enough to confirm startup and a real rolling log file. Check cache expiration settings and the explicit callback/invalidation paths. State which runtime behaviours cannot yet be exercised until controllers exist. Update docs/Architecture.md, docs/BuildStatus.md, and README.md. Explain error ownership, delegate execution, and the two cache modes briefly. Stop before Prompt 4. Do not implement MSTest/Moq tests.
```

## Prompt 4 — MVC endpoints and the themed application shell

```text
Read AGENTS.md and the current project documents, then inspect Services and result contracts. Implement this MVC/web-boundary stage. Do not redesign the existing layers or duplicate the business logic.

CONTROLLERS AND ROUTES
Implement thin ProductsController and ReportsController classes. Product data actions depend on IProductService; report data actions depend on IReportService. Controllers may also use normal web concerns such as logging, model binding, and response mapping, but cannot access Repository, DbContext, SQL, or cache.

Use these routes consistently and document them:
- GET / redirects to /products.
- GET /products returns Views/Products/Index.cshtml.
- GET /products/data returns the product collection as JSON.
- GET /products/{id:int}/data returns one product as JSON.
- POST /products creates a product from form data.
- POST /products/{id:int}/update updates that product from form data.
- POST /products/{id:int}/delete deletes that product.
- GET /reports returns Views/Reports/Index.cshtml.
- GET /reports/data?threshold=10 returns the complete report dashboard as JSON.

Use async actions for Service calls and pass RequestAborted. The update route Id is authoritative. POST routes are intentional MVC/AJAX commands; do not introduce GET mutations. Return 201 for a successful create with a meaningful resource location, 200 for successful reads/update/delete/no-change update, 400 for validation failures, 404 for missing products, 409 for conflicts, and a suitable 5xx status for persistence/unexpected failures. Do not return a 204 bodyless response for delete because the client requires an explicit outcome.

JSON uses a consistent camelCase envelope with isSuccess, data, message, errorCode, validationErrors, and an optional traceId. Centralize HTTP mapping without putting HTTP concepts into Services. Service failure must not accidentally become HTTP 200. Binding/validation errors should use the same useful envelope.

AJAX AND VALIDATION SECURITY
Add antiforgery validation to all mutations, preferably as an appropriate global MVC filter. Render a token in the initial Razor page and design the AJAX helpers to submit it. Both missing/invalid tokens must be rejected. If MVC produces a non-JSON antiforgery failure, either normalize it through a small appropriate result filter or handle it safely in the frontend; never inject an error HTML page into a product table.

Use server validation and encoded Razor output. Do not bind Product entities directly from requests. Keep mutation values in approved input DTOs. Do not log request bodies or tokens. Avoid building HTML with raw user-supplied strings.

THEMED SHELL
Inspect docs/reference/ui-theme.jpeg again if present. Build _Layout.cshtml and shared styling for both pages using Bootstrap plus focused custom CSS. Use a white top header with a circular navy mark and configurable SESATH PRODUCTS wordmark. Use a narrow left navigation rail with working Inventory and Reports links, obvious active state, accessible names/tooltips, and a compact mobile layout.

Do not add dummy profile, notification, settings, search, or tier controls solely because they appear in the reference. Every visible interactive control needs a meaningful supported behaviour.

Suggested CSS variables, adjustable slightly for readability:
--app-bg: #F2F5F9;
--surface: #FFFFFF;
--primary: #123F8A;
--primary-hover: #0F3270;
--text: #202834;
--muted: #667383;
--border: #DFE5EC;
--chart-blue: #2DA9E1;
Use 12-16px card radii, restrained shadows, thin borders, clear spacing, visible focus states, and readable system typography. Do not reproduce blurry/condensed generated-image text. Keep the application responsive rather than rendering the reference as a full-page screenshot.

Create the two Razor page shells and necessary modal/partial view containers. Load compatible pinned official jQuery, Bootstrap, SweetAlert2, Toastr, and Chart.js assets in the correct order, preferably locally under wwwroot/lib for a dependable assessment demo. Preserve licensing information and document versions. Do not add a frontend framework or an unnecessary bundler. Use a minimal CSS/SVG brand mark if no separate usable logo asset exists; do not use a crop of the entire reference image as the interface.

Put shared scripts in wwwroot/js/site.js, product scripts in products.js, and report scripts in reports.js. Put the theme in wwwroot/css/site.css. Use Razor sections for page-specific scripts; do not place all application logic inline in cshtml. Configure display currency as a single documented option with USD as the initial value.

VERIFY AND STOP
Build and start the application. Verify both page routes, active navigation, asset loading order, and JSON routes with the real Service layer. Verify one rejected mutation without an antiforgery token and the expected error outcome for an invalid/missing product. No UI controls should claim to work before they are wired; clearly record unfinished interactions for Prompt 5. Update docs/BuildStatus.md and the endpoint documentation. Stop before Prompt 5.
```

## Prompt 5 — Finish AJAX CRUD and all report panels

```text
Continue this existing implementation. Read AGENTS.md and the status/architecture documents. Inspect the reference image and the real routes, JSON contracts, Razor files, and JavaScript before editing. Complete the frontend now; do not build a mock frontend or invent API responses.

PRODUCT LIST PAGE
Match the reference's compact white-card inventory layout. Include a page title, search input, category filter, stock-status filter if implemented, Add Product button, and a responsive table with Product ID, Name, Category, Price, Stock, Status, and Actions. Format prices consistently in the configured currency. Keep actual numeric stock visible alongside status badges.

Use a default low-stock threshold of 10 for this page, visibly explained in the filter/status context. Stock 0 is Out of Stock; stock greater than 0 and below 10 is Low Stock; stock at least 10 is In Stock. The report page can use a separately selected threshold. A zero-stock product is still included in a low-stock report whose threshold is positive.

For this small assessment, load the product collection through GET /products/data and implement search, category filtering, sorting, and modest pagination on that returned collection. Document that this is client-side filtering for a small dataset, not scalable server pagination. Reconcile the current page after a deletion so a populated preceding page is not hidden behind an empty last page. Keep controls functional and remove optional controls that are not implemented.

Create a Bootstrap modal for Add/Edit with labelled Name, Category, Price, and Stock inputs, helpful validation messages, a clear submit button, and cancel. Edit loads the product through GET /products/{id}/data, using the CachedLong path maintained by invalidation after application writes. A write must still check authoritative database state in the Repository. Do not claim that a long-lived cache automatically detects changes made outside this application; document that limitation. If the endpoint reports a removed product, show the failure rather than opening an editable empty form. Details, if offered, use the same data path and a modal rather than a new main page.

Perform create, update, and delete via the established POST AJAX endpoints. Send antiforgery tokens and form fields correctly. Disable the relevant submit/action while its request is in flight; restore state on both success and failure. Prevent duplicate handlers and accidental double submits. Show Toastr success only when the server's isSuccess is true and the HTTP operation succeeded. Handle non-2xx responses in the AJAX failure path, parsing safe JSON messages and field validation errors where available. Handle a network failure or unexpected non-JSON response with a useful generic message.

Use SweetAlert2 to confirm deletion, naming the product as safely rendered text. Cancellation performs no request. On confirmed success, refetch product data using AJAX so the table reflects committed state and the server's cache invalidation behaviour is exercised. On failure, keep the existing row and show a truthful error. Refetch/update after create and edit as well, while preserving useful filters and sorting. Do not call location.reload or navigate away to complete a CRUD operation.

If the write succeeds but a follow-up table refresh fails, tell the user the save succeeded and that refreshing the list failed; do not mislabel the save as failed or resubmit it. Show distinct loading, empty, validation-error, and unavailable states. Preserve user-entered form values on failed validation. Return keyboard focus sensibly when dialogs close.

Use .text(), safe DOM APIs, and encoded templates for user-provided Name/Category. Do not concatenate untrusted fields into HTML, inline event attributes, or SweetAlert HTML. Use data attributes/closures safely for Ids and delegate event handlers for dynamic table rows.

REPORT PAGE
Keep all five reports on the existing /reports page with a responsive card grid and larger full-width panels. A tab/anchor layout is acceptable if it preserves access to all reports and the two-page application structure. Fetch real data from /reports/data?threshold=... using jQuery AJAX.

1. Average Price per Category: show each category and its actual average price, using a compact readable table or labelled rows. Do not display an unexplained single global average as though it represents every category.
2. Highest Stock Value Category: show category name, monetary inventory value, and stock units. Show all tied categories clearly. This card uses SUM(Price * Stock), not the quantity-only maximum.
3. Inventory Valuation by Category: include a blue bar chart plus a readable table of Category, Total Units, and Inventory Value. Use category names as chart labels and money on the value axis/tooltips. The table is the exact accessible alternative. A grand total can sum returned category inventory values; never sum average prices into a misleading total.
4. Low-Stock Alert: provide a labelled integer threshold input, default 10, Apply and/or a clearly implemented refresh action, and a product table. State 'Stock below [threshold]'. Validate the threshold, show helpful errors, and send the actual parameter to the stored procedure through the API. For threshold 10, stock 9 belongs in the report and stock 10 does not. Do not promise real-time push updates; refresh on page entry and user actions.
5. Top 5 Most Expensive Products: use attractive ranked rows/cards inspired by the reference, showing rank, Name, Category, Price, and optional stock. Render the actual descending order from the procedure; there may be fewer than five rows. Use generic icons if desired, not invented product images, tiers, or fake dropdowns.

Use Chart.js only for the actual returned inventory data. Update/destroy chart instances properly on refresh. Provide responsive container sizing, legible labels, sensible axis formatting, and an empty state when there are no rows. Do not display sample bars when the database call fails. Avoid chart animations that impede use.

Prevent out-of-order AJAX responses from overwriting newer report/filter selections, using cancellation of the earlier request or a request-sequence check. A cancelled older request should not show a failure toast. Destroy stale loading states when requests finish. Distinguish empty results from unavailable reports.

POLISH AND ACCESSIBILITY
Match the light/navy reference without copying placeholder text or data. Use consistent spacing, button sizes, badges, labels, and modal layouts. Keep text contrast and keyboard focus clear. Give icon-only actions accessible names and use semantic buttons. Ensure tables scroll inside their cards on small screens, while the overall page stays usable. Stack report cards on mobile and keep the header/navigation compact. Avoid decorative-only toggles and meaningless controls.

VERIFICATION AND STOP
Run the application against SQL Server and exercise create, list, details, edit, unchanged edit, delete cancellation, confirmed delete, invalid input, and missing products through the actual browser UI if browser access is available. Check that CRUD does not reload the page and that failed operations do not show success. Check all five reports, strict threshold semantics, real chart data, and correct ranking.

Inspect at least a desktop and a narrow/mobile viewport for clipped controls, unreadable text, overflow, and modal focus. Check browser console/network errors when tools allow. If browser tooling is unavailable, say so and provide a short reproducible manual checklist rather than claiming visual verification. Fix issues within this phase and update docs/BuildStatus.md. Stop before Prompt 6. Do not implement automated unit tests.
```

## Prompt 6 — Run, verify, document, and hand over

```text
Read AGENTS.md, docs/BuildStatus.md, README.md, and the actual code. Complete the implementation handoff for the approved scope. This phase includes build/runtime smoke verification, not the deferred MSTest/Moq suite.

REPRODUCIBLE STARTUP
Verify the documented sequence from a fresh dependency restore through local dotnet tool restore, build, migration update, optional demo seed, and dotnet run. Use actual project paths, framework/package versions, connection-string configuration, and launch URL. Show separate instructions only where Windows LocalDB and another supported SQL Server setup differ. Keep credentials out of instructions/logs. Record what was actually executed in this environment.

If the agreed SQL Server prerequisite is still blocked, complete every unblocked source/documentation task and state the exact remaining prerequisite. Never claim the system is up and running on real SQL Server unless it was. Do not substitute an in-memory provider or fake frontend data to make a demo appear successful.

SMOKE CHECKS
Check the following against the real application, using temporary records that can be identified and removed without touching existing user data:
- Successful create/read/update/delete returns the documented HTTP status and operation outcome.
- An unchanged update succeeds without being confused with a failed write.
- Invalid values and missing required numeric fields fail without database changes.
- A missing product returns NotFound; delete cancellation makes no mutation.
- Mutation requests without a valid antiforgery token are rejected.
- Special characters in product names/categories render as text rather than executable markup.
- UI failures show truthful errors, not success notifications or false empty states.
- All five report outputs match the demo data/manual SQL calculations, including highest-value ties, strict threshold boundaries, and top-five ordering.
- Repeated reads demonstrate cache hits; a successful mutation causes subsequent product/report reads to reflect the new database state. Use procedure logs or another non-invasive observation to demonstrate relevant cache misses/hits. Review that Cached has an absolute five-minute cap and CachedLong has no time expiry. Do not pretend you waited for expiry if you only inspected the configuration.
- Structured logs are actually written and contain procedure execution details and safe error information. Verify exception handling by a controlled local-only check if possible; do not leave an intentional error endpoint or temporary fault in the delivered app.
- Desktop/mobile layouts, assets, modals, and charts work without console errors where browser tools are available.

Do not build a full automated testing framework during this phase. Do not make destructive database changes merely to force a failure. Mark unavailable checks honestly. Correct observed defects and repeat only the checks needed to confirm those fixes.

DOCUMENTATION
Finish README.md with purpose, stack, prerequisites, setup/run commands, database/procedure creation, optional seed usage, page routes, configured currency/branding, logging location, caching behaviour, architecture overview, and known limitations.

Update docs/ProjectPlan.md and docs/Architecture.md to reflect the implementation. Include concise explanations of the actual generic repository, dependency injection, async boundaries, delegate callback, DTO mapping, five stored procedures, cache lifetime/invalidation, result semantics, and exception ownership. Explain the approved monetary definition of highest stock value and the handling of ties.

Create docs/Verification.md with a clear Passed / Failed / Not Run status for each relevant check and the evidence or reason. Create docs/TestingBacklog.md documenting the later required MSTest/Moq Service tests: CRUD success/failure, validation rejection without a write, not-found/conflict, unchanged update, cache behaviour, callback invocation, and cancellation. Do not label this deferred work complete.

Remove temporary throwaway code and verification data you created, leaving intended optional demo data intact. Do not remove user files. Ensure no credentials, generated logs, or build-output folders are committed. Preserve migrations and reproducible dependency metadata.

FINAL HANDOFF
Give me:
1. The actual local application URL and whether the app is currently running.
2. The exact commands needed to start it again.
3. A concise summary of implemented features and meaningful design choices.
4. Verification results with any remaining blocker/limitation.
5. The key files to inspect to understand each requirement.
6. A clear statement that formal MSTest/Moq tests remain the next development stage.

Do not claim full assessment completion while the required unit tests are deferred. End the current implementation phase after the working application and documentation are handed over.
```

## If the agent loses context

Paste this before the next numbered prompt in a new conversation:

```text
Resume the existing WaterlilyInventory project; do not scaffold it again. First read the root AGENTS.md, docs/ProjectPlan.md, docs/Architecture.md, and docs/BuildStatus.md. Inspect the current code and working tree. Briefly identify the last completed stage, real blockers, and the next unfinished stage. Preserve the approved architecture, monetary highest-stock-value formula, cache invalidation decision, MVC Razor views, and deferred MSTest/Moq work. Follow the next numbered prompt I provide. Do not repeat completed migrations, overwrite existing implementation, or infer success from documentation without checking the current files.
```

## If a stage fails

Paste the actual error output with this instruction:

```text
Fix the failure in the current stage before starting the next one. Read the exact error and relevant existing code. Identify the cause, make the smallest correct fix that preserves AGENTS.md and the approved design, then rerun the failed check. Do not replace SQL Server, bypass validation/security, suppress errors, or return fake success to make the symptom disappear. Update docs/BuildStatus.md with the actual outcome. If a prerequisite requires my action, give me the exact next action and complete all other unblocked work in this stage.
```

## If the agent only gives advice instead of editing files

```text
Implement this stage directly in the open workspace now. Create or edit the required files, run the relevant commands, fix observed errors, and report the verified outcome. I have authorized the current stage. Stop only at the stage boundary or for a concrete blocker requiring information or access you do not have. Do not give me the entire remaining codebase or move into a later stage.
```

## Source and implementation notes

The source assessment is the user-provided `Waterlily C# Web Development Task II.pdf`. The frontend reference is the user-provided `Gemini_Generated_Image_tgb2yjtgb2yjtgb2.jpeg`. The reference's visual design has been translated into functional MVC screens; its example data and labels are not treated as requirements.

The assessment requires the project plan, two MVC pages, Product CRUD, Service/Repository separation, MSSQL migrations, the first two report calculations, both caching modes, delegates, jQuery, explanatory comments, and MSTest tests with mocks. The other three reports, generic async repository, typed results, Serilog, middleware, AJAX polish, and cache invalidation are the approved extensions. No final unit-test implementation is included in this prompt sequence.

Official references used to check implementation guidance:

- [Antigravity project rules](https://www.antigravity.google/docs/rules/) — root AGENTS.md/project-scoped instructions. These prompts use ordinary agent messages, without requiring custom slash-command workflows.
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy) — .NET 10 LTS baseline; check actual installed SDK and compatible package patches during setup.
- [ASP.NET Core MVC views](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/overview?view=aspnetcore-10.0) — controller-served Razor views.
- [EF Core SQL queries](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries) — unmapped DTO results, parameterization, and stored-procedure composition limits.
- [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing) — SQL objects managed through migrations and procedure batch handling.
- [ASP.NET Core memory caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0) — absolute/sliding expiration and in-process cache behaviour.
- [EF Core SaveChangesAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.dbcontext.savechangesasync?view=efcore-10.0) — persistence outcomes and DbContext concurrency restrictions.
- [Serilog ASP.NET Core integration](https://github.com/serilog/serilog-aspnetcore) and [file sink](https://github.com/serilog/serilog-sinks-file) — structured request/application logging and rolling files.
- [Chart.js integration](https://www.chartjs.org/docs/latest/getting-started/integration.html) — plain JavaScript integration without a frontend framework.
