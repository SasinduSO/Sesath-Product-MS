# README — Running the Waterlily testing prompts

## What these files do

`Waterlily_MSTest_Agent_Prompts.md` contains six detailed prompts, **7A–7F**, that instruct your IDE agent to implement the MSTest/Moq suite in the existing WaterlilyInventory project. This README explains how to use those prompts and recover if the agent stops.

The prompts use your real Services with mocked Repository/cache dependencies. A separate group exercises the real in-memory cache helper without a database. They preserve the application architecture and adapt to the code your agent actually generated.

**These documents do not mean tests already exist or have passed.** The agent must create and run them in your project. Passing unit tests also does not prove that SQL procedures, HTTP endpoints, or browser interactions work; those require their own checks.

## Put both files in the same project folder

Download this README and `Waterlily_MSTest_Agent_Prompts.md` into your existing project root, beside `Waterlily_Antigravity_Prompt_Pack.md` and `AGENTS.md` where those files exist.

| File or folder | Purpose |
| --- | --- |
| `Waterlily_Antigravity_Prompt_Pack.md` | Earlier application implementation instructions |
| `Waterlily_MSTest_Agent_Prompts.md` | Testing instructions for stages 7A–7F |
| `README_MSTest.md` | This usage guide |
| `src/WaterlilyInventory.Web/` | Expected existing application project |
| `tests/WaterlilyInventory.UnitTests/` | Test project the agent will create or reuse |
| `tests/WaterlilyInventory.UnitTests/README.md` | Agent-maintained README for the actual test suite, in the test project's folder |
| `docs/TestingStatus.md` | Checkpoint and recovery record |

Paths under `src/` and `tests/` are the agreed structure. If your agent used another valid structure, it should inspect and document the actual paths instead of creating duplicates. Preserve your application README; the testing README is separate.

## Before starting

- Open the existing project folder in Antigravity. Do not create a fresh empty project.
- Select your coding model; Gemini 3.1 Pro High is suitable for following this pack if available in your account.
- Keep existing implementation files and status documents in place.
- The Services to be tested should already exist. The inspection stage will identify missing pieces; it must not invent them or claim coverage for absent code.
- You have now authorized unit-test development. Prompt 7A updates the older testing-deferral instruction while keeping other project rules intact.

## Start with Prompt 7A

Paste this in the agent conversation:

```text
Read README_MSTest.md and Waterlily_MSTest_Agent_Prompts.md in this workspace.

Apply the Shared rules and execute only Prompt 7A now. I authorize the MSTest/Moq testing phase, superseding the earlier instruction to defer those tests.

Inspect the actual application, create or reuse the test project, run its first meaningful Service test, and create the separate README.md inside the test-project folder. Save checkpoint progress in docs/TestingStatus.md.

Report actual results and stop before Prompt 7B.
```

Alternatively, copy the complete Prompt 7A block from the prompt file. Its first line tells the agent to read the shared rules. Do not paste all six checkpoints at once.

## Continue one checkpoint at a time

| Checkpoint | Work | Expected handoff |
| --- | --- | --- |
| **7A** | Inspect actual contracts; configure MSTest/Moq; run the first meaningful test | Working discovery/execution, runner choice, test README |
| **7B** | Create and Read Service tests | Verified result mapping, validation/failure examples, cache hit/miss boundaries |
| **7C** | Update and Delete Service tests | Verified changed/unchanged/missing/conflict/failure outcomes and invalidation ordering |
| **7D** | Validation boundaries, error policy, cancellation | Data-driven boundary coverage and correct failure/cancellation handling |
| **7E** | Cache/delegate and Report Service tests | Deterministic cache checks and report coordination; checkpoints 7E.1–7E.3 |
| **7F** | Review/run the full suite and finish documentation | Real results, TRX report, coverage if measured, finalized test README |

After 7A completes, paste:

```text
Execute only Prompt 7B from Waterlily_MSTest_Agent_Prompts.md.

Read the Shared rules, docs/TestingStatus.md, and the test-project README first. Inspect the existing tests, implement the missing work for this checkpoint, run relevant verification, and save actual results.

Stop at this checkpoint's boundary. Do not recreate completed setup or advance to the next checkpoint.
```

For each subsequent checkpoint, change **7B** to **7C**, **7D**, **7E**, or **7F**. You do not need to paste the original application prompts again. If the agent cannot locate this pack, first check that you downloaded it into the folder the agent has open.

## If credits run out or you switch models

Save the current files and keep the same project folder. When a model is available, paste:

```text
Resume the interrupted Waterlily MSTest/Moq checkpoint.

Read Waterlily_MSTest_Agent_Prompts.md, README_MSTest.md, AGENTS.md, docs/TestingStatus.md, docs/BuildStatus.md, and the actual test-project README. Use the Recovery prompt in the testing pack.

Inspect current files and any running terminal command because the checkpoint may not include the last edit. Complete only the unfinished checkpoint, run its relevant tests, update the status, and stop before the next checkpoint. Do not restart the test project.
```

The agent's checkpoint should state the last completed case, what is unverified, the latest failure, and the exact next action. Reading that checkpoint together with the code is more useful than repeating the entire setup prompt.

## If the agent gives explanations without creating tests

```text
Implement the current testing checkpoint directly in the existing workspace. Create or edit the actual test files, run the configured test runner, fix observed failures, and update the test README and checkpoint documents. I have authorized this checkpoint. Explain any concrete environment blocker, but do not stop at a suggested plan or pasted sample code.
```

## How to run the completed tests yourself

After 7A, open `tests/WaterlilyInventory.UnitTests/README.md` or the actual path reported by your agent. It must contain the run command verified for your installed SDK and runner.

Open an Antigravity terminal at the **project root**. A configured solution normally supports:

```bash
dotnet test
```

The agent must verify and document that command in your setup. To target the expected test project specifically, the command depends on the selected runner:

| Runner configuration | Example command from the project root |
| --- | --- |
| Microsoft.Testing.Platform mode | `dotnet test --project tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj` |
| VSTest mode | `dotnet test tests/WaterlilyInventory.UnitTests/WaterlilyInventory.UnitTests.csproj` |

Use **only the row matching the runner recorded in your test README**. If paths differ, use its actual verified command. Filtering, TRX, and coverage options also depend on the runner; copy those from the generated README rather than mixing commands from different tutorials.

Tests run in the terminal or a configured IDE test explorer. There is no localhost page for running this suite, and SQL Server/site startup is not required. The console should report discovered/executed tests and pass/fail/skip counts. A successful build or a result showing zero executed tests is not evidence of a passing suite.

## What should be real and what should be mocked?

| Component | Service unit tests | Cache-helper component tests |
| --- | --- | --- |
| ProductService/ReportService | Real Service under test | Usually unnecessary |
| Product Repository | Moq mock | Unnecessary |
| Cache abstraction/helper | Controlled mock | Real production helper |
| IMemoryCache storage | Hidden behind the mock | Fresh in-process cache per test |
| Logger | NullLogger unless directly relevant | NullLogger where required |
| SQL Server, website, credentials | Not used | Not used |

Moq supplies controlled dependency outcomes. The test must call the real Service and assert its behaviour. A test that only invokes its own mock proves little about your application.

## Files the agent should maintain

| Document | Contents |
| --- | --- |
| `docs/TestingPlan.md` | Actual contract map and scenario coverage matrix |
| `docs/TestingStatus.md` | Completed/incomplete checkpoints, commands, outcomes, next action |
| Test-project `README.md` | Actual test structure, prerequisites, versions, verified run commands, mock strategy, limitations, troubleshooting |
| `docs/TestingResults.md` | Final actual counts, execution time, report paths, measured coverage or its absence, unresolved gaps |
| `docs/BuildStatus.md` | Short link/status for testing alongside application development |
| `docs/TestingBacklog.md`, if present | Only verified items marked complete; integration/web checks kept separate |

The test-project README is technical documentation for the delivered suite. This root README_MSTest.md is your guide for directing the agent. Keep both.

## When a test fails

Give the agent the real error output and use the Repair prompt in the pack. It should determine whether the fixture, expectation, application code, or runner configuration is wrong, then make the smallest correct fix and rerun the affected tests.

Do not accept removing a meaningful assertion, ignoring a required failing case, or returning fake success as a fix. A requirement-backed failing test may have found an actual application defect.

## Completion criteria

- Required Product Service CRUD success/failure behaviours are covered and pass.
- Validation, unchanged updates, missing records, and relevant cache side effects have meaningful assertions.
- Added cache/delegate/report tests are implemented with documented boundaries and real outcomes.
- The suite runs without SQL Server or launching the MVC application.
- The runner discovers and executes the intended tests; failures/skips are explained.
- The test folder contains its own accurate README with verified commands.
- Reported test counts and coverage come from actual execution, not estimates.
- Remaining SQL integration, HTTP, and browser checks are not misrepresented as covered by mocks.

## Technical references

- [MSTest setup](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-getting-started)
- [dotnet test runner modes and command syntax](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
- [MSTest test structure](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests)
- [Moq](https://github.com/devlooped/moq)
