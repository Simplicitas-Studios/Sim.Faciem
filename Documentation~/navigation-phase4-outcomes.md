# Phase 4 — opt-in outcomes and contained UI recovery

Based on merged main `c5733d6266ab6848883d663a89abdc89994dc80d` (PRs #3–#5). Framework only. Draft for independent review; no Display integration, pin update, merge or deployment.

## API

Four new extension methods; existing interfaces, signatures, default-literal calls and throwing/cancellation behavior remain available. No second navigator/coordinator is introduced. A per-request observation record travels through the same native navigator and existing reference-identity Region queue.

```csharp
NavigationResult result = await navigation.NavigateToResult(
    manager, viewId, region, parameters,
    NavigationPolicy.LatestRequestWins,
    NavigationEntryMode.ReenterIfActive);

switch (result.Status)
{
    case NavigationStatus.Succeeded:
        // Logical entry completed and the requested owned Views passed verification.
        break;
    case NavigationStatus.Superseded:
        // Do not publish obsolete work or treat this as navigation acceptance.
        break;
    default:
        // Inspect Status, Stage, Recovery and Error; choose application recovery explicitly.
        break;
}

NavigationResult cleared = await navigation.ClearResult(manager, region);
// Succeeded means active Views were cleared. NoOp means already empty at owned execution time.
```

**Inside lifecycle callbacks, including after awaits, use the protected scoped `Navigation` handle:**

```csharp
NavigationResult result = await Navigation.NavigateResult(
    nextViewId, independentRegion, parameters,
    NavigationPolicy.Fifo, NavigationEntryMode.Default);
NavigationResult cleared = await Navigation.ClearResult(independentRegion);
```

Same-owned-Region and dependency-cycle submissions through this scoped handle return `InvalidConfiguration` at `Queue`, with the rejection exception. They do not deadlock, reset state or supersede the owner. The raw root `INavigationService` remains for external submissions; awaiting it from an owning callback can still self-deadlock. Result methods do not infer origin or change that contract.

New methods return operational failures/cancellation as **values in a succeeded `UniTask<NavigationResult>`**. This is deliberate opt-in behavior; callers of old methods still receive the legacy exceptions/cancelled tasks. `IsSuccess` is true **only** for `Succeeded`, not `NoOp` or a missing lookup. Custom navigation services are not optimistically dispatched and labelled successful: native result/recovery support is required, otherwise `UnsupportedOperation` is returned without dispatch.

## Outcome contract

| Status | Meaning |
| --- | --- |
| `Succeeded` | To completed; every target has exactly the requested active ID, expected model dataSource, visible cached View in its owned visual Region, and valid activation. For Clear: active IDs are empty after actual work. |
| `NoOp` | Clear found no active Views **after** predecessors drained. Ordinary same-ID navigation still calls To and is not a no-op. |
| `MissingRegion` | Lookup failed or resolved no actual target Regions. Region lookup has precedence over ViewId lookup. |
| `MissingViewId` | Region resolved but ViewId was not registered. No construction or lifecycle dispatch. |
| `Superseded` | This opted-in operation was superseded and observed cancellation. Queued work still waits for its barriers; executing callbacks must return before this result is released. |
| `UnsupportedOperation` | Multi-view result navigation/Clear, or a custom service without native result/recovery support. No optimistic fallback to the old API. |
| `InvalidConfiguration` | Invalid options/required inputs, template/context configuration, foreign or incompatible cached/active dataSources, heterogeneous requested caches, unsafe re-entry cohort, busy shared model, or scoped reentrancy/cycle rejection. |
| `Cancelled` | Observed cancellation not caused by this queue operation's supersession. |
| `Failed` | Construction, synchronous preparation/commit, lifecycle, cleanup or postcondition failure; also any incomplete containment. |

`Stage` identifies the **primary** failure (validation, queue, construction, preparation, commit, cleanup, exit, entry or verification). Recovery failures do not replace that stage. `Error` retains the exception observed at the framework boundary; additional containment errors are aggregated, not discarded. **UniTask can normalize cancellation exceptions before Faciem sees them**: cancellation token/type are observable, but the application-thrown cancellation object's identity/message/stack are not promised. A lifecycle-thrown `NotSupportedException` is a failure at Entry/Exit, not inferred to mean unsupported configuration.

Only single-view Regions and homogeneous requested cached models are supported by the new result path. Existing multi-view/heterogeneous legacy behavior is unchanged. An invalid group is preflighted before normal result-path exit mutation, not partially accepted.

## Recovery is UI containment, not a transaction

| Recovery | Guarantee / limit |
| --- | --- |
| `NotRequired` | No recovery needed; inspect Status to distinguish success, no-op and missing lookup. |
| `Preserved` | This request failed before its owned UI/lifetime mutation. Existing owned screen/lifetime was not reset by this request. Detached construction/preparation effects outside that boundary are not rolled back. |
| `Contained` | Known owned trees are hidden, captured target active IDs are empty, and known activation endings/cleanup have been awaited/attempted. No synthetic To/Away is invented during recovery. Cache entries and dataSources are retained. |
| `Incomplete` | Further recovery errors or invalid ownership prevented establishing containment. Inspect the aggregate; do not assume a usable screen or silently retry. |

### Before mutation

Validate the whole target group, construct/select the model, instantiate missing Views and assign their dataSources while detached. A missing template, invalid ownership, constructor exception or synchronous preparation exception can therefore preserve the prior framework screen/lifetime. Arbitrary constructor, custom View factory, DI or data-binding side effects are not reversible. Constructed candidates remain under the existing DI/final-model ownership contract; this phase does not dispose them or their lifetime bags.

### After exit/commit begins

Hide the complete **owned** logical-model cohort before its one Away; end/clean its activation and await Away. For explicit re-entry, all restart preflight rules remain. A failed exit cannot resurrect the old activation, so recovery deliberately contains rather than showing a dead prior screen. Partial group failures contain all captured target Regions, not just the first member. Distinct physical trees remain cached; a later explicit application request can reuse them with a fresh activation.

Containment handles a mount that threw **before recording an active ID**: it also hides tracked trees, not just the active-ID list. It neither replaces foreign dataSources nor moves trees back from unowned parents. A reparented tree, busy unowned shared model or further cleanup failure is reported as incomplete. Original errors and all attempted recovery errors remain observable.

If an earlier exit corrupts a later Region's dataSource, the result path rejects/contains that partial operation instead of taking a legacy early return and reporting success. It does not dispose the foreign object or invent a later model's Away callback.

### Explicit rollback limits

- No reversal of arbitrary user writes, network/audio/game side effects, constructor effects or resource-release failures. Contained does **not** mean every user resource successfully released; a primary cleanup error remains in Error even when UI containment succeeds.
- No transactional rollback of nested Region registration, binding internals, DI registration or a partially configured retained cache. Such configuration/commit failures can leave application repair necessary. A subsequent successful result verifies the top-level requested activation, not reconstruction of those subsystems.
- No promise that all asynchronous UI Toolkit bindings resolve, layout/render completes, pixels appear, or player/World Space behavior is correct. Synchronous preparation/dataSource/commit exceptions are observed; later binding-engine diagnostics are not a navigation transaction. There is no speculative frame wait or private binding/root reflection.
- Latest admission effects are not rolled back: a newer request can supersede an older opted-in request even if the newer request later fails preflight. Preserved refers to this request's mutation boundary, not undoing admission or earlier actors.
- An uncooperative callback (or blocking user cleanup) can retain ownership indefinitely. Timeout/cancellation does not authorize early Region release. Arbitrary unguarded writes are not prevented; capture the activation and publish through its validity guard.
- A shared model remains a logical activation cohort. Ending a member can invalidate shared activation resources without physically mutating unowned Regions. Independent screen lifetimes require distinct models; concurrent lifecycle use of a shared model is rejected.
- No final ViewModel/cache/root disposal, cache purge, failure rollback, new public origin API or forced callback cancellation.

## Executable evidence

`Tests/Editor/NavigationOutcomeTests.cs`: **56 new cases**, real mounted EditorWindow Regions, imported A/B/C UXML Views, real configured models, deterministic completion-source gates. Two preparation cases use an explicitly labelled internal fault seam (before instantiation / after real detached dataSource assignment); two commit cases use a real Region subclass that faults before/after real AddView. These prove recovery at those boundaries, **not** that the UI Toolkit engine itself threw a native binding error.

Final combined suite: **152 Passed / 0 Failed / 0 Skipped** = 56 new + all **96 unchanged** compatibility/disposable-bag/Phase 1–3 cases. Existing test sources, templates, assembly definitions and metadata are unchanged. Mounted panels require graphics-enabled isolated Editor tests; the consumer was closed and no running Editor was repointed/closed. PlayMode, player, World Space, production Display and audio acceptance are intentionally not claimed.

History retained honestly:

1. Initial 148: **145 Passed / 3 Failed**. Product assertion caught duplicate Away for a shared model; two fixture assertions incorrectly required original cancellation object identity. Original reports, three failing traces and pre-fix source copies retained.
2. Corrected 150: **150 Passed / 0 Failed**. Added busy-shared-model and overlapping-group result cases. Intermediate source hashes were not captured at run time; this is retained observation evidence, not final-source acceptance.
3. Reviewed final 152: **152 Passed / 0 Failed**. Added Navigate/Clear partial-source-corruption regressions and the result-only Clear guard. Final source hashes captured before execution and verified afterward. Formatter/verification exit 0.

**152 distinct final cases; 450 observations across three launches, including 3 failed observations.** Repeated passes are not extra distinct cases. No test runner retries, backend switching, Editor closure, lock deletion or assertion suppression.

See [runs](evidence/navigation-phase4/runs.json), [contracts](evidence/navigation-phase4/contracts.json), [checks](evidence/navigation-phase4/checks.json), [final recovery traces](evidence/navigation-phase4/traces) and [integrity manifest](evidence/navigation-phase4/artifact-integrity.json). Normalized/NUnit reports are byte-exact. Only surplus EOF blank lines in exported text traces may be normalized; raw/exported hashes and affected paths are recorded. Targeted `-whitespace` attributes preserve exception-stack trailing spaces in trace/XML evidence; source and documentation still receive whitespace checks.

**Stop after the focused draft PR for independent review.**
