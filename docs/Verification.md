# WaterlilyInventory — Verification Evidence & Smoke Check Log

> This document details the live runtime verification and smoke check evidence collected for the WaterlilyInventory application against the approved requirements, running on .NET 10 LTS with SQL Server 2022 Express (`.\SQLEXPRESS`).

---

## Verification Matrix

| Check ID | Verification Area | Target Requirement | Status | Evidence / Verification Notes |
|---|---|---|---|---|
| **CHK-01** | **Reproducible Dependency Restore** | Pinned .NET 10 SDK and NuGet package restore | ✅ **PASSED** | `dotnet tool restore` restored `dotnet-ef` 10.0.12 cleanly; `dotnet restore` restored all packages from `global.json` and `.csproj` with zero errors. |
| **CHK-02** | **Project Compilation** | Zero warnings, zero errors on build | ✅ **PASSED** | `dotnet build` succeeded with 0 Warning(s) and 0 Error(s). |
| **CHK-03** | **Database Schema & Migrations** | Code-First migrations applied idempotently | ✅ **PASSED** | `dotnet ef database update` applied `InitialCreate` and `AddStoredProcedures`. Verified via `sqlcmd`: `__EFMigrationsHistory` records both migrations with version `10.0.12`. |
| **CHK-04** | **Page Routes & Layout** | Two MVC Razor pages with themed layout shell | ✅ **PASSED** | `GET /` redirects to `/products` (HTTP 302). `GET /products` returns HTTP 200 with complete inventory table shell and modal containers. `GET /reports` returns HTTP 200 with reporting dashboard shell. Both render `SESATH PRODUCTS` brandmark and configured currency `USD`. |
| **CHK-05** | **Local Pinned Assets** | Local libraries loaded without remote CDN dependencies | ✅ **PASSED** | All 12 local assets in `wwwroot/lib` and `wwwroot/` returned HTTP 200: Bootstrap 5.3.3 CSS/JS, jQuery 3.7.1, Toastr 2.1.4, SweetAlert2 11.14.5, Chart.js 4.4.6, `site.css`, `site.js`, `products.js`, `reports.js`. |
| **CHK-06** | **Product Read Collection** | `GET /products/data` returns all products in JSON envelope | ✅ **PASSED** | HTTP 200 returned with `{ isSuccess: true, data: [...] }` containing 13 seeded products. |
| **CHK-07** | **Product Read Single** | `GET /products/{id}/data` returns single product DTO | ✅ **PASSED** | `GET /products/1/data` returns HTTP 200 with `{ isSuccess: true, data: { id: 1, name: "Laptop Stand", ... } }`. |
| **CHK-08** | **Product Missing Read** | `GET /products/{id}/data` for missing ID returns 404 | ✅ **PASSED** | `GET /products/999999/data` returns HTTP 404 with standardized failure envelope (`errorCode: "NotFound"`). |
| **CHK-09** | **Product Creation (Create)** | `POST /products` creates product and returns HTTP 201 | ✅ **PASSED** | Created product with valid form data and CSRF token returned HTTP 201 Created with `Location: http://127.0.0.1:5126/products/{id}/data` and populated `ProductDto`. Product count increased from 13 to 14. |
| **CHK-10** | **Product Update (Edit)** | `POST /products/{id}/update` updates fields | ✅ **PASSED** | Updated name, price, and stock returned HTTP 200 with updated `ProductDto`. Subsequent read confirmed persisted values. |
| **CHK-11** | **Unchanged Product Update** | Submitting identical values returns 200 with "No changes were needed" | ✅ **PASSED** | Submitting unchanged values returned HTTP 200 with `isSuccess: true` and message `"No changes were needed."`. Serilog confirmed: `Product {id} update submitted with identical values. No changes made.` and zero database commits occurred. |
| **CHK-12** | **Validation Rejection** | Negative price/stock and blank names fail without writing | ✅ **PASSED** | POST with blank name, empty category, negative price (`-50`), and negative stock (`-1`) returned HTTP 400 Bad Request with `errorCode: "Validation"` and field validation errors. Database remained completely unmodified. |
| **CHK-13** | **Product Deletion (Delete)** | `POST /products/{id}/delete` removes product and returns 200 | ✅ **PASSED** | Deletion returned HTTP 200 with `{ data: { deletedId: ... } }` (never 204). Subsequent GET returned HTTP 404. Total product count returned to 13. |
| **CHK-14** | **Antiforgery Security** | Mutation requests without CSRF token are rejected | ✅ **PASSED** | POST `/products` without `__RequestVerificationToken` returned HTTP 400 with standardized JSON envelope (`errorCode: "AntiforgeryValidationFailed"`). No HTML error page injected into AJAX caller. |
| **CHK-15** | **XSS & Injection Protection** | HTML tags in product Name and Category render safely as text | ✅ **PASSED** | Created product with `<script>alert('xss')</script>&quot;Safe&quot;` and `<b>Formatting</b> & Co.`. Verified frontend DOM scripts use `.text()` and SweetAlert2 uses `text:`, ensuring zero markup execution. Cleaned up cleanly. |
| **CHK-16** | **Report 1: Average Price** | Stored procedure `usp_Product_AveragePriceByCategory` | ✅ **PASSED** | Returned 4 categories with arithmetic means: Accessories ($47.50), Electronics ($31.67), Office Furniture ($100.00), Stationery ($17.00). Matches manual SQL `AVG(Price)` calculations. |
| **CHK-17** | **Report 2: Highest Stock Value** | Stored procedure `usp_Product_HighestStockValueCategory` | ✅ **PASSED** | Verified tied leaders detected via `DENSE_RANK()`: Electronics ($3,000.00 / 110 units) and Office Furniture ($3,000.00 / 30 units). UI displays `#highestValueTieBadge` (`Tied Leaders (2)`). |
| **CHK-18** | **Report 3: Category Valuation** | Stored procedure `usp_Product_InventoryValuationByCategory` | ✅ **PASSED** | Returned 4 categories totaling $8,000.00 monetary value across 310 units. Chart.js bar chart and accessible table display matching data with cumulative grand total. |
| **CHK-19** | **Report 4: Low-Stock Strict Semantics** | Stored procedure `usp_Product_LowStock` | ✅ **PASSED** | Threshold 10 returned exactly 3 items strictly below 10: `Notebook Set` (stock 0), `Mechanical Keyboard` (stock 0), `Footrest` (stock 9). Items with stock 10 (`Ergonomic Chair`, `USB Flash Drive`) strictly excluded. Negative threshold (-1) rejected with HTTP 400. |
| **CHK-20** | **Report 5: Top 5 Expensive** | Stored procedure `usp_Product_TopFiveMostExpensive` | ✅ **PASSED** | Returned top 5 products strictly descending by Price, then ascending by Id: `Ergonomic Chair` ($200.00), `Mechanical Keyboard` ($150.00), `Laptop Stand` ($50.00), `Desk Lamp` ($50.00), `Footrest` ($50.00). |
| **CHK-21** | **In-Memory Cache (Cached Mode)** | 5-minute absolute expiry on product list & reports | ✅ **PASSED** | Demonstrated via procedure logs: Cold read executed all 5 stored procedures (`[INF] Executed stored procedure dbo.usp_Product_...`). Warm read immediately hit `CachedAsync` with 0 stored procedures executed, responding in ~5ms. |
| **CHK-22** | **In-Memory Cache (CachedLong Mode)** | Long-lived product detail caching with NeverRemove | ✅ **PASSED** | `ProductById` cached via `CachedLongAsync` with `CacheItemPriority.NeverRemove`. Readily retrieved without database query until mutation occurs. |
| **CHK-23** | **Cache Invalidation on Mutation** | Automatic invalidation of affected product & report scopes | ✅ **PASSED** | Verified via logs: On create, update, and delete, `AppCache.InvalidateByPrefix` cleared product and report cache scopes. Subsequent read of reports re-executed all 5 stored procedures against SQL Server. Unchanged update did not invalidate. |
| **CHK-24** | **Structured Rolling Logs** | Serilog text file logs with structured properties | ✅ **PASSED** | Log file `src/WaterlilyInventory.Web/Logs/log-20261012.txt` contains structured request metrics (`UseSerilogRequestLogging`), procedure execution timings, row counts, and safe outcomes. No sensitive tokens or database credentials logged. |
| **CHK-25** | **Global Exception Handling** | Catch unhandled exceptions without leaking internals | ✅ **PASSED** | `GlobalExceptionHandlingMiddleware` catches unhandled exceptions, logs trace identifier, returns safe HTTP 500 JSON envelope for AJAX/JSON requests, redirects page requests to `/Home/Error`, and handles client cancellation cleanly without false error logs. |
| **CHK-26** | **Responsive Design & Accessibility** | Semantic HTML, keyboard accessibility, mobile rail | ✅ **PASSED** | Verified table header keyboard focus (`tabindex="0"`, `role="button"`), accessible alternative table for Chart.js, aria attributes on modals and navigation, responsive wrapping on small screens. |
| **CHK-27** | **Formal Unit Tests (MSTest/Moq)** | Comprehensive automated test suite | ⏸️ **NOT RUN (DEFERRED)** | Explicitly deferred to the next required stage as defined in project rules and prompt pack. All interfaces (`IProductService`, `IProductRepository`, `IAppCache`) preserved mockable. |

---

## Smoke Check Script Execution Summary

```powershell
==========================================================
 WATERLILY INVENTORY -- SMOKE CHECKS AND VERIFICATION 
==========================================================

--- [Check 1] Page Routes and Antiforgery Rendering ---
Products Page Loaded: True
Reports Page Loaded: True
CSRF Token Extracted Length: 155

--- [Check 2] Static Assets Availability ---
All 12 Local Static Assets Loaded HTTP 200: True

--- [Check 3] Cache Miss vs Cache Hit Observation via Procedure Logs ---
Request 1 (Cold / Miss): Stored Procedures Executed = 5 (Initial cold load)
Request 2 (Warm / Hit): Stored Procedures Executed = 0 (Expected: 0 - CACHE HIT PROVEN!)

--- [Check 4] Mutation and Cache Invalidation Lifecycle ---
Create Status: 201, Created Id: 21
Cache Invalidation Logged: True (Scope invalidated on successful commit)
Request 3 (After Invalidation): Stored Procedures Executed = 5 (Expected: 5 - RELOADED FROM SQL!)

--- [Check 5] Unchanged Update Outcome ---
Unchanged Update Status: 200
Unchanged Update isSuccess: True, Message: 'No changes were needed.'

--- [Check 6] Validation Rejection (No Database Change) ---
Validation Failure Status: 400
Validation ErrorCode: Validation
Validation Errors Reported: True

--- [Check 7] Missing Product Handling ---
NotFound Status: 404
NotFound ErrorCode: NotFound, Message: 'Product with ID 999999 was not found.'

--- [Check 8] Antiforgery Security Check ---
Missing Token Status: 400
Missing Token ErrorCode: AntiforgeryValidationFailed

--- [Check 9] Delete and Clean-up of Temporary Smoke Record ---
Delete Status: 200, Deleted Id: 21
Post-delete verification Status: 404 (404 Confirmed)

--- [Check 10] All 5 Reports Baseline Accuracy ---
Report 1 (Avg Price): 4 categories
Report 2 (Highest Value): 2 tied categories (Electronics and Office Furniture at $3,000.00)
Report 3 (Valuation): 4 categories totaling $8,000.00 valuation
Report 4 (Low Stock < 10): 3 items strictly below 10 (Notebook Set, Mechanical Keyboard, Footrest)
Report 5 (Top 5 Most Expensive): 5 items (Top: Ergonomic Chair at $200.00)

==========================================================
   ALL SMOKE VERIFICATION CHECKS PASSED WITH ZERO ERRORS! 
==========================================================
```
