# Waterlily Inventory — MSTest and Moq agent prompts

Use these prompts in the existing application workspace, one checkpoint at a time. They continue the original implementation pack with stages **7A–7F**. They work with Gemini 3.1 Pro High or the currently selected coding model.

Read the companion `README_MSTest.md` for starting, continuing, and recovering after a usage-limit interruption. Both files belong beside `Waterlily_Antigravity_Prompt_Pack.md` in your project root. The agent must also create a separate, implementation-specific `README.md` inside the test project.

These are instructions to implement tests in your local IDE. No test result is claimed by this document, and the generated application source has not been inspected here.

## Shared rules for every testing checkpoint

```text
You are implementing the testing phase of my existing WaterlilyInventory assessment. Work directly in the open workspace. The user has now authorized MSTest/Moq implementation, superseding the previous instruction to defer unit tests. Preserve all other approved application requirements.

Use the actual generated code. Read AGENTS.md, docs/ProjectPlan.md, docs/Architecture.md, docs/BuildStatus.md, any docs/TestingBacklog.md, and the relevant source. Once available, also read docs/TestingStatus.md and tests/WaterlilyInventory.UnitTests/README.md. If actual paths differ, use and document them consistently. Do not create a second solution or duplicate application.

Implement only the current requested checkpoint. Acknowledge completed work from code and execution evidence, not just status documents. Inspect incomplete edits and existing processes before resuming after an interruption. Stop at the end of the current checkpoint and report the next checkpoint.

UNIT-TEST BOUNDARY
Exercise the real ProductService and ReportService. Use Moq for Repository interfaces and controlled cache abstractions. Use NullLogger by default. Do not mock the Service under test, instantiate a real Repository/DbContext, launch the website, connect to SQL Server, run migrations, load real credentials, or start Serilog file logging for these tests. Do not use EF Core InMemory or SQLite as a replacement for mocking the Repository.

Separate real cache-helper/component tests from isolated Service tests using files and categories. They may use the real in-process memory cache, but still have no external dependencies. A mocked report response cannot prove the correctness of stored-procedure SQL; record SQL/report integration coverage separately.

CORRECTNESS
Test the approved observable behaviour: meaningful success/failure results, DTO values, validation errors, relevant arguments, required writes, cancellation, and cache side effects. Do not test only whether a mock returns the value configured on that same mock. Do not assert only non-null or true for every case. Avoid tests that copy production algorithms to calculate their expected result. Use simple fixed expected values.

Adapt names and signatures to the existing implementation without weakening its approved semantics. If a test exposes a defect, identify whether the fixture, expectation, or implementation is wrong. Small production fixes needed to restore an approved behaviour or create a narrow testability seam are authorized; explain them and cover them with a regression test. Do not change public behaviour, redesign architecture, or rewrite entire layers merely to make tests convenient. If an approved feature is absent, record the gap and continue independent tests; do not manufacture a pass or silently skip it.

Use awaited async Task test methods, never async void. Test methods need no OperationResult return value; assertions report their outcome. The explicit-result rule applies to application CRUD methods. Use fresh mocks and mutable state per test, deterministic values, and concise Arrange/Act/Assert structure. Share small builders and setup helpers only when they improve clarity. Do not hide the entire scenario in a large inheritance hierarchy or catch exceptions merely to pass the test.

Use strict mocks where they make unplanned data calls visible, especially Repository mocks. Match meaningful arguments and cancellation tokens. Keep assertions focused on the contract rather than incidental read counts or private implementation details. Verify critical writes and prohibited calls explicitly. Avoid blanket VerifyAll/VerifyNoOtherCalls on logging or harmless implementation details.

Comments should explain each test class's responsibility and non-obvious mock/delegate/clock arrangements. Use descriptive test names and short summaries for important helper methods. Do not comment every assertion, duplicate obvious names, or litter test files with narrative paragraphs.

CHECKPOINTS
After each coherent work unit, run the relevant focused tests/build check and update docs/TestingStatus.md. Record the checkpoint, completed files, actual commands, discovered/passed/failed/skipped test counts where available, current failure/blocker, and exact next action. Include work that is incomplete. Update docs/BuildStatus.md with a concise link/status. Never mark a checkpoint complete from a successful build when the tests were not discovered or executed.

Credit interruption is a reason to preserve a useful checkpoint, not restart or lower assertions. Keep test output concise and avoid unnecessary repeated restores or whole-suite runs after every small edit. Do not add automated retries to hide intermittent failures. Do not add CI/CD or new external services.

DOCUMENTATION
Maintain tests/WaterlilyInventory.UnitTests/README.md (or the actual test folder's README) with real prerequisites, runner/package choices, verified commands, test categories, mock strategy, coverage boundaries, and troubleshooting. Preserve the application README and existing unrelated instructions. Do not overwrite the companion README_MSTest.md with a fabricated test report.
```

## Prompt 7A — Inspect contracts, configure MSTest, prove test discovery

```text
Execute checkpoint 7A from Waterlily_MSTest_Agent_Prompts.md. Read and apply its Shared rules first. I now authorize the MSTest/Moq testing phase. Implement this checkpoint directly in the existing workspace, then stop before 7B.

1. Inspect the application and plan
Locate the real solution, application .csproj, target framework, global.json, NuGet/package management, ProductService/ReportService constructors, Repository/cache interfaces, delegate definitions, result factories, error codes, and DTO validation. Read existing testing notes and working-tree changes. Update only the obsolete 'defer MSTest/Moq' instruction in project rules to reflect this authorization; preserve unrelated rules.

Create docs/TestingPlan.md with a contract map: public operation, input/output types, relevant dependency calls, expected outcomes, and test category. Link cases to approved requirements. Record any implementation/plan disagreement explicitly. This document should distinguish Service result behaviour from HTTP status mapping, stored-procedure mathematics, and browser interactions.

Confirm these approved semantics against source: zero Price/Stock are valid; blank fields are invalid; missing products are NotFound; an empty list is successful; unchanged updates succeed; failed writes do not invalidate caches; committed writes remain committed even if later cache maintenance fails; expected persistence failures are safe Service results; cancellation propagates; unexpected defects escape to middleware.

2. Add one test project
Create or reuse tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj. Match the application's supported target framework and reference its actual .csproj. Add the project to the existing solution once. Use MSTest and Moq with concrete compatible stable versions; honor central package management if present. No preview packages are necessary.

Inspect the installed template/SDK and existing test configuration. For a new .NET 10 setup, prefer the supported MSTest.Sdk configuration and a consistent Microsoft.Testing.Platform runner. If the solution already has a working compatible MSTest runner, reuse it. Do not install conflicting adapters/collectors or combine VSTest-specific flags with MTP-only configuration. Preserve existing SDK pins when editing global.json. Record the chosen runner and actual commands in the test README. Never guess version numbers or claim package restoration succeeded when it failed.

Use public TestClass/TestMethod classes/methods for normal discovery. Add a deliberate assembly parallelization policy appropriate to independent tests and address relevant MSTest analyzer warnings. No tests may rely on shared mutable static fixtures or execution order. Exclude bin, obj, TestResults, and coverage output from source control without excluding test source or project metadata.

3. Set up minimal test support
Create fresh Moq Repository/cache dependencies and a real ProductService using its actual constructor. Use NullLogger unless a specific log behaviour is tested later. Build small valid input/entity/result builders with fixed values; clone mutable objects per test.

Make the cache mock's miss behaviour invoke the exact asynchronous factory-delegate type used by the application. Configure it to return/await the delegate's Task from the mock Returns setup; do not put an async lambda into a void Moq Callback and create async void accidentally. Do not invent a different delegate type simply because its signature looks similar.

4. Implement and run the first meaningful test
Start with a real ProductService creation success case. Arrange valid input and a successful Repository result containing a known generated Id. Call the Service, assert its success/data/error state, check the mapped values and single intended Repository write, and verify the approved cache invalidation side effect. If creation is currently blocked by an unrelated missing feature, use another existing public CRUD behaviour that can be tested honestly and record the blocker.

Remove any unneeded template assertion-only sample. Run the selected test. Confirm the runner discovers and executes at least one meaningful test, with actual outcome/count output. A build or an empty successful test run is insufficient.

5. Document and checkpoint
Create the test project's README.md in the same folder as its .csproj. Include the actual relative paths, SDK/runner/Moq choices, restore/run command that worked, the first test's purpose, isolation boundary, and how to run tests without starting SQL Server or the website.

Create docs/TestingStatus.md covering 7A–7F, marking only verified work complete. Update docs/BuildStatus.md. Report files changed, discovered/executed results, any minimal production fix, unresolved blockers, and that 7B is next. Do not implement the remainder of the suite in this checkpoint.
```

## Prompt 7B — Create and Read Service tests

```text
Execute checkpoint 7B only. Read the Shared rules in Waterlily_MSTest_Agent_Prompts.md, the test README, docs/TestingPlan.md, and docs/TestingStatus.md. Inspect the existing tests and actual Service contracts. Reuse the working setup and first test from 7A.

CREATE TESTS
Cover these behaviours through the real ProductService:
- Valid input plus Repository Created returns success and the generated product Id. Verify mapped Name, Category, Price, and Stock, the write's meaningful arguments, and the required cache invalidation. Reuse/expand the existing test instead of duplicating it.
- A representative invalid input, such as blank Name, returns a field-related validation failure without any Repository write or invalidation. The broader validation matrix comes in 7D.
- An expected PersistenceException becomes the documented safe failure result and does not invalidate caches or retry the write. Include a distinctive internal marker in the injected exception and verify it is absent from public response fields.
- If the Repository contract has explicit conflict/failure outcomes applicable to creation, verify their meaningful mapping. Do not invent an impossible status or illegal mock return state only to increase test count.

READ TESTS
For product lists:
- A cache miss executes the real Service's supplied factory against a Repository mock and returns correctly mapped DTOs.
- A successful empty Repository list returns IsSuccess true with an empty collection, not NotFound.
- A documented Repository failure returns failure, not a fabricated successful empty list.
- A cache hit returns the arranged DTO snapshot without Repository access. This proves Service cooperation with the cache abstraction, not the real cache's retention policy.

For individual products:
- An existing product returns all intended fields and the correct Id.
- A Repository miss returns the approved NotFound result.
- A representative invalid Id fails validation before data access.
- An expected read failure follows the documented safe failure policy.
- The Service uses the long-lived cache method for a detail read and passes an Id-specific key/scope according to the actual abstraction. Assert the cache mode and parameter distinction if these are public contract choices; do not hardcode arbitrary private string formatting.

Keep cache hit and miss fixtures explicit. A hit setup must not execute its factory. A miss setup must return/await the factory's Task so mapping/error handling inside the Service is exercised. Configure correct generic types and return shapes. Never test the cache factory by calling a mock Repository directly outside the Service and treating that as Service coverage.

ASSERTION QUALITY
Assert results and meaningful side effects, not only that a Task completed. Use exact decimal expected values. Check that critical writes occur once. Avoid overspecifying how many harmless reads a valid workflow performs unless that count is itself a requirement. Do not demand IQueryable or EF behaviour across the Repository boundary.

Use clear class/file separation such as ProductServiceCreateTests and ProductServiceReadTests, adapted to existing names. Categorize these as Unit and ServiceCrud if supported by the chosen organization. Do not duplicate scenarios already implemented in 7A.

Run the new/changed tests and existing affected tests. Resolve failures against the approved contract. Record any small production fix and the regression it addresses. Update the test README, coverage matrix, docs/TestingStatus.md, and docs/BuildStatus.md with actual outcomes. Stop before 7C.
```

## Prompt 7C — Update and Delete Service tests

```text
Execute checkpoint 7C only, following this pack's Shared rules. Read existing tests, the test README, TestingPlan, and TestingStatus. Inspect actual update/delete signatures and Repository outcomes before configuring mocks.

UPDATE TESTS
- A valid changed product and successful Repository update return the correct updated DTO and success. Verify the authoritative operation Id and all allowed updated values reach the Repository. The input must not change the primary key.
- A Repository Unchanged outcome is a successful no-change result. It must not become failure because no state was written, and it follows the approved no-invalidation policy.
- A missing product returns NotFound and does not invalidate caches.
- Representative invalid input prevents a Repository write and invalidation.
- A documented concurrency/conflict outcome becomes Conflict, not success or generic NotFound.
- An expected persistence exception becomes the approved safe failure without a retry or invalidation.

DELETE TESTS
- A successful deletion returns an explicit success outcome with the correct deleted Id and invalidates the relevant cache scope.
- A missing product returns NotFound without successful invalidation.
- An invalid Id is rejected before the Repository mutation.
- An expected failure or applicable conflict is reported truthfully and does not invalidate caches or retry the mutation.

AUTHORITATIVE OUTCOMES
Ensure write decisions use Repository persistence outcomes even when cached detail data exists. Configure cached product data and a Repository NotFound/Conflict result where the actual call path permits it; the Service must not report success based solely on the cache. Respect the implemented update/delete flow: if it legitimately reads first, arrange that read. Do not insist on a fictitious signature or mock DbContext/SaveChangesAsync inside a Service unit test.

COMMIT ORDERING
Add a focused test showing invalidation cannot precede a successful write outcome. Arrange the Repository write to return an incomplete TaskCompletionSource with RunContinuationsAsynchronously. Start the Service call, use an explicit signal to know it has reached the mocked write, verify no invalidation yet, then complete the write successfully and await the Service. Confirm success and subsequent invalidation. Include bounded test timeouts and ensure all tasks settle during cleanup. Do not use Thread.Sleep or a timing race.

Use a separate failure path to show unsuccessful completion does not invalidate. A post-commit cache-maintenance failure case will be finalized in 7E; do not wrap this stage in broad logging-implementation assertions.

Keep cases readable, reuse valid data builders, and verify the critical mutation occurs once. A Service test verifies its interpretation of Repository outcomes; it does not prove SQL row changes or persistence transactions.

Run the affected tests and fix genuine defects with focused changes. Preserve all earlier passing behaviours. Update the test README, TestingPlan mapping, TestingStatus, and BuildStatus with commands/outcomes and exact next action. Stop before 7D.
```

## Prompt 7D — Validation boundaries, failures, and cancellation

```text
Execute checkpoint 7D only. Apply the Shared rules, read TestingStatus and TestingPlan, and inspect current validation/normalization/result code. Expand existing tests rather than adding duplicate cases with slightly different names.

VALIDATION MATRIX
Exercise the real Service, not only DataAnnotations in isolation. Use data-driven tests for related inputs while keeping unrelated behaviours in separate tests. Use the actual approved maximum lengths and monetary precision/range; compare implementation differences with the project plan and record/fix genuine defects.

Cover create and update appropriately:
- Name: missing/null when representable, empty, whitespace-only, maximum accepted length, one character beyond it, and documented trimming.
- Category: missing, whitespace-only, accepted/overlong lengths, and the approved case/whitespace normalization policy. Do not invent title-casing, a Category table, or a uniqueness rule.
- Price: missing when representable, negative, zero, an ordinary valid decimal, a valid maximum boundary, over-range input when representable, and excessive non-zero fractional precision such as 1.234m if two decimals are supported.
- Stock: missing when representable, negative, zero, and a positive integer.
- Id: zero, negative, and valid positive input for applicable read/update/delete operations.

For invalid cases, assert the relevant field error/code, no Repository access where validation must precede it, and no cache invalidation. For valid boundary cases, arrange a valid persistence result and prove the boundary is accepted. Preserve the rule that zero price and zero stock are valid. Do not confuse an omitted required numeric input with an intentional zero. If the implemented DTO cannot represent that distinction, record the discrepancy and make the smallest approved-contract correction rather than writing an impossible test.

Use DynamicData or typed builders for decimal values; do not convert money to double simply to fit DataRow attribute arguments. Keep expected values explicit. Do not validate HTTP form binding or antiforgery by pretending a direct Service call exercises MVC.

ERROR BEHAVIOUR
Cover expected persistence-failure mapping across CRUD without copying identical tests unnecessarily. Put an artificial diagnostic marker in the inner exception and assert that it is absent from public Message, ValidationErrors, and returned data. Use the real expected exception/result factories. Do not expose real credentials or depend on Serilog files.

Arrange an unexpected exception for a path where the approved Service policy permits it to escape. Assert that it propagates using the selected MSTest version's awaited exception assertion API. Do not assert that middleware ran in this unit test. If the Service catches everything, determine whether that violates the approved policy; do not change the test to accept fabricated failure/success merely for a green run.

CANCELLATION
Pass a distinct CancellationToken to the real Service and verify that it reaches the relevant cache factory/Repository call. Arrange that dependency to complete as cancelled or throw the appropriate OperationCanceledException. Verify cancellation remains cancellation rather than a PersistenceFailure result, a retry, or success.

For a write cancelled before a successful Repository outcome, no successful cache invalidation should occur. Test pre-cancelled tokens only to the extent promised by the public contract; do not invent a policy about already-cached values that was never specified. Do not simulate a post-commit cancellation as though it proves the database rolled back.

QUALITY CHECK
Use fresh test instances and fixtures. Avoid shared mutable static test data, wall-clock time, random identifiers that make failures hard to reproduce, sleeping, broad assertions of every logger call, or private-member reflection. Await all work and dispose any resources owned by the fixture. Ensure parameterized cases are actually discovered and report the difference between logical cases and expanded runner counts.

Run the relevant validation/error/cancellation tests and affected CRUD regressions. Fix issues under the approved behaviour. Update the test README, TestingPlan coverage matrix, TestingStatus, and BuildStatus. Record verified outcomes and any unresolved distinction between application and test-fixture defects. Stop before 7E.
```

## Prompt 7E — Cache/delegate behaviour and Report Service tests

```text
Execute checkpoint 7E only. Read the Shared rules and existing test documents. Inspect the real cache abstraction/helper, invalidation scope, generation/change-token handling, delegate signature, ProductService, and ReportService. Work in sub-checkpoints 7E.1, 7E.2, and 7E.3, saving status after each so a credit interruption is recoverable. Reuse tests already written for equivalent scenarios.

7E.1 — SERVICE CACHE CONTRACTS
Use the real ProductService with Repository/cache mocks. Finalize coverage for the public contract:
- List reads use Cached, detail reads use CachedLong.
- An arranged cache hit does not query the Repository; an arranged miss invokes and awaits the Service-supplied delegate.
- Product Ids and report parameters produce independent cache entries according to the public abstraction.
- Successful create/update/delete invalidates the relevant scope after persistence; failure, missing record, and unchanged outcomes follow their approved non-invalidation policies.
- If invalidation throws after the Repository has confirmed a commit, the Service preserves the truthful persistence outcome and does not retry the write. Verify write count and returned persisted data. Arrange the secondary failure explicitly so the test cannot pass without exercising that branch.

The application's data-factory delegate is distinct from Moq's Callback feature. Use Moq Callback only for synchronous observations such as captured arguments or signals. Return the asynchronous factory Task through Returns when controlling asynchronous behaviour. Never put async work into a void callback, fire-and-forget it, and claim it was tested.

7E.2 — REAL CACHE-HELPER COMPONENT TESTS
Use the real production helper, a fresh actual IMemoryCache implementation, and a fresh product invalidation scope for each test. Keep them in Caching/ with a CacheComponent category so their boundary is visible. No Repository implementation or database is involved; the loader delegate supplies fixed data and an invocation count.

Cover:
1. Miss then hit: two reads with the same key produce one factory invocation and the expected value.
2. Key separation: different product keys/threshold parameters remain independent.
3. Successful empty collection: can be retained.
4. Missing product/null: not retained; the next call invokes the loader again.
5. Factory exception or cancellation: does not populate a successful cache entry; a later legitimate request can try again.
6. Invalidation: both cache modes and representative list/detail/report/threshold keys are refreshed by the shared scope.
7. Generic results: at least one non-collection result demonstrates that a legitimate zero/false value is not confused with a missing reference value, if supported by the actual generic contract.

EXPIRATION
Test that Cached has an absolute deadline five minutes from insertion and that a hit before the deadline does not extend it. Test CachedLong has no time-based expiration and still refreshes after explicit invalidation.

Use a deterministic clock mechanism compatible with the actual cache implementation. Advance the clock used by the cache that enforces expiration, not merely an unrelated FakeTimeProvider. Inspect the installed version's supported clock/options surface before choosing the approach. A narrow adapter/seam that preserves production behaviour is allowed; document why it is necessary. Do not add Task.Delay or Thread.Sleep to wait for five real minutes, reduce the production timeout just for the test, or use private-field reflection.

Assert a cached result just before the absolute deadline and a loader invocation at/after the deadline. For CachedLong, also verify the absence of expiration configuration and retention beyond the ordinary cache duration before invalidation; do not claim a finite test mathematically proves eternal retention. If a supported deterministic expiration route cannot be established without broad changes, record the precise unverified runtime case and verify configuration separately rather than inventing evidence. Do not silently skip an expiration test and call the scope fully complete.

STALE IN-FLIGHT READ
Use TaskCompletionSource signals with RunContinuationsAsynchronously and bounded waits to control this sequence:
- Start an old-generation cache miss and pause its factory after it has entered.
- Invalidate the product scope, representing a successful write.
- Read the same logical key in the new generation and publish a new value.
- Release the old factory with the old value and await it.
- Read again and prove the new-generation value remains current and the old load did not overwrite it.
The old caller may receive its old snapshot; this test concerns the value retained for subsequent reads. Avoid sleeps, scheduling assumptions, deadlocks, and unfinished background tasks. Dispose resources only after the test's in-flight work settles.

Do not demand one factory invocation for simultaneous cold misses unless the application explicitly guarantees request coalescing. The approved implementation permits independent misses; test correctness rather than inventing a stampede-prevention requirement.

7E.3 — REPORT SERVICE UNIT TESTS
Use the real ReportService with mocked Repository/cache dependencies. Return distinct fixed DTO datasets for the five reports so a swapped or omitted mapping is detectable. Cover:
- Valid threshold produces all five datasets and preserves the selected threshold.
- The low-stock Repository call receives the exact threshold, including zero if valid.
- An invalid negative threshold fails before data retrieval.
- Empty report datasets remain legitimate results.
- Multiple tied highest-value categories are preserved in the output.
- Correctly ordered top-five Repository data is passed through; do not pretend mocked data proves SQL TOP/ORDER BY correctness.
- A required report's persistence failure returns the documented failure, not a dashboard with invented successful zero values.
- Relevant cache keys include threshold where necessary.
- Cancellation reaches the dependency and propagates.

If the real Service awaits several calls on one scoped Repository/DbContext, add one focused coordination check that shows it does not start overlapping database calls. Use controlled tasks/entry signals rather than exact total timing. Do not unnecessarily fix an arbitrary order when only non-overlap is required.

BOUNDARIES AND EXIT
SQL calculations, SQL parameters at execution time, actual migrations, HTTP status mapping, middleware responses, antiforgery, and browser behaviour remain separate integration/web checks. Document the boundary; do not claim mocked Service tests validate those systems.

Run each sub-checkpoint's focused tests, record outcomes and justified production fixes, update the test README and status documents, and stop before 7F. Report any real coverage gaps explicitly.
```

## Prompt 7F — Full run, evidence, and the test-project README

```text
Execute checkpoint 7F only. Apply the Shared rules. Read the test README, TestingPlan, TestingStatus, current test files, and the actual runner configuration. Finish the testing deliverable with verified results and useful documentation.

1. REVIEW THE SUITE
Check that each approved core CRUD behaviour is represented by a meaningful test and that tests exercise real Services. Remove only obsolete template tests or confirmed duplicates; preserve distinct edge cases. Confirm fresh fixtures, no external database/app startup, no sensitive configuration, no waiting for real expiry, no async void, no unobserved asynchronous callbacks, and no disabled tests hiding required failures.

Review any production changes made during testing. Each must address a documented defect or narrow testability need with regression coverage; no tests should require a broad replacement architecture. Keep comments useful and test helpers understandable.

2. EXECUTE
Restore as needed, build, and run the full test project using the chosen runner. Confirm actual discovery and execution counts, not just exit code zero. Investigate failures instead of weakening assertions. Re-run affected cases after fixes; repeat the whole suite when those changes could affect it. Do not run optional repeated suites or add retries without a concrete reason.

The unit/cache suite must not need SQL Server, a running site, migrations, user-secrets, or real log files. Confirm this from its dependencies and execution without initiating those services; do not shut down the user's existing SQL Server or application merely to demonstrate isolation.

Generate a machine-readable TRX result using the installed runner's supported extension/options, and confirm the report exists and agrees with executed results. For MTP, ensure the required reporting extension/profile is actually configured; do not assume VSTest's --logger option works. Use project/solution and filter syntax appropriate to the chosen runner and record the working commands.

Produce a Service-focused coverage report if a compatible coverage tool is available or can be added simply. Treat it as evidence of unexercised branches, not a substitute for assertions or an arbitrary 100% target. Record the collector/extension and denominator/scope. Do not exclude production Service code to inflate a score, mix incompatible collectors, or claim coverage was measured when it was not. If coverage tooling is blocked, preserve the passing tests and report that separate limitation.

3. COMPLETE THE README IN THE TEST FOLDER
Create/finalize README.md beside the actual test .csproj, normally tests/WaterlilyInventory.UnitTests/README.md. It must describe the implemented suite, not just repeat a generic testing plan. Include:
- Purpose and relation to the Waterlily assessment.
- Actual project path, framework, MSTest/Moq versions, and selected runner.
- Prerequisites and the fact that SQL Server/site startup are unnecessary.
- Copyable, verified commands from the repository root for restore, all tests, a selected class/category, and TRX reporting. Include coverage commands only when configured and verified.
- Test folder/file responsibilities, categories, and a short actual Arrange/Act/Assert example adapted from an implemented test.
- What is real versus mocked, including Service, Repository, cache, and logger boundaries.
- Meaning of the application's delegate callback versus a Moq observation callback.
- Cache expiration/invalidation testing technique and any limitations.
- Covered CRUD outcomes, validation, error/cancellation, and report-coordination cases.
- Limits: unit mocks do not prove stored-procedure SQL, HTTP middleware, antiforgery, or browser behaviour.
- Troubleshooting for zero tests discovered, package/SDK mismatch, stale builds, runner flag mismatch, strict-mock failures, and unexpectedly required database connections.
- How to add a test and how to resume from TestingStatus after interruption.
- Latest verified result summary or a link to docs/TestingResults.md with its real timestamp.

Retain the separate root README_MSTest.md as the user's prompt-running guide. Add a short link from the existing application README to the test-project README without replacing application instructions.

4. EVIDENCE AND STATUS
Create docs/TestingResults.md with actual commands, runner/version, execution date/time, discovered/passed/failed/skipped counts (and expanded data rows where relevant), TRX location, measured coverage or 'Not measured', focused production fixes, and remaining gaps. Keep bulky generated artifacts under an ignored TestResults location; keep the concise findings in documentation. Reference the verified local paths rather than invented report files.

Update docs/TestingBacklog.md, if it exists, marking only verified cases complete and retaining separate integration/web tasks. Update docs/TestingStatus.md and docs/BuildStatus.md. If a mandatory case is blocked or failing, label it accurately rather than declaring all testing complete.

FINAL HANDOFF
Tell me the exact test project and README paths, the simplest verified run command, actual pass/fail/skip counts, meaningful coverage limits, production defects fixed, and anything remaining. Do not claim full application or assessment correctness from unit tests alone. Stop after the testing handoff.
```

## Recovery prompt — Continue the interrupted testing checkpoint

```text
The previous agent stopped during the MSTest/Moq testing phase. Resume the existing workspace using the currently selected model.

Read Waterlily_MSTest_Agent_Prompts.md, README_MSTest.md, AGENTS.md, docs/TestingStatus.md, docs/BuildStatus.md, docs/TestingPlan.md, and the actual test-project README where present. Identify the last checkpoint/sub-checkpoint, then inspect the actual source, tests, runner configuration, and incomplete edits. Check for an already running test command.

Status files may lag behind the last edits. Separate verified work, unfinished work, and unverified work. Reuse existing projects and tests. Do not scaffold again, repeat completed setup, weaken assertions, or advance to another checkpoint prematurely.

Resume only the unfinished checkpoint. Run focused verification, fix observed issues under the approved contract, and save progress after every coherent unit. Keep the suite isolated from SQL Server and the website. Finish that checkpoint and stop at its boundary.
```

## Repair prompt — Fix the current test failure

```text
Fix the current testing checkpoint before advancing. Read the actual failure output and relevant Service, dependency contracts, test fixture, and runner configuration. Classify whether the cause is a test setup mistake, an incorrect expectation, an application defect, or an environment/runner problem.

Preserve the approved business behaviour and isolation boundary. Make the smallest justified fix and rerun the failed/affected tests. Do not skip the case, relax meaningful assertions, introduce fake success, connect to SQL Server, or replace MSTest to obtain a green run. Record the cause, fix, execution result, and next action in docs/TestingStatus.md.
```

## Official technical references

These links support setup and test-tool usage. Scenario expectations come from the approved Waterlily project design and must be reconciled with the actual source in the IDE.

- [Get started with MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-getting-started)
- [Write tests with MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests)
- [MSTest test lifecycle](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests-lifecycle)
- [dotnet test and runner modes](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
- [Moq official repository](https://github.com/devlooped/moq)
