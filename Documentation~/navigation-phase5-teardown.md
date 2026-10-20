# Phase 5 — explicit final cache ownership and teardown

Baseline: merged main `c9507cce4a71d54a13d8efe595b4d01c66b2d14a`, including PRs #3–#6. Framework-only draft; no Display code/pin change, merge, deployment or integration.

## Clear is not teardown

| Operation | Cache/model identity | Activation | Final model lifetime |
| --- | --- | --- | --- |
| Ordinary navigation / same-ID To | Retained | Existing activation or ordinary new entry | Retained |
| NavigateAway / Clear | Retained, including inactive caches | Ended on leaving | Retained |
| Explicit re-entry | Exact cached trees/model retained | Old ended, fresh activation | Retained |
| `TeardownResult` | Owned cache references removed; owned trees unbound/detached | Ended before exit/release | Each owned model's Dispose attempted once |

A successfully released Region is **terminal**, not an empty reusable cache. Replace it with a new Region identity and update its manager registration (or create a new manager) for a new scope. The model factory/DI scope must supply fresh instances. A disposed singleton returned by DI cannot be set up/reactivated.

## Minimal API and explicit authority

```csharp
// The application knows which models its DI/navigation scope owns.
// Do not infer ownership from a ViewModel type or blindly approve DI singletons.
NavigationResult result = await navigation.TeardownResult(
    new[] { gameplayRegion },
    model => applicationOwnedModels.Any(owned => ReferenceEquals(owned, model)));

if (result.Status == NavigationStatus.Failed)
{
    // Inspect result.Stage, result.Recovery and the aggregate result.Error.
    // The scope is terminal after mutation begins; repair external state explicitly.
}
```

The scoped counterpart is `Navigation.TeardownResult(regions, ownsModel)`. **All lifecycle-origin submissions must use the protected scoped Navigation handle, including after awaits.** The raw root service remains external-only; awaiting raw-root teardown/navigation from its owning callback can still self-deadlock. The scoped path rejects same-owned-Region/dependency-cycle teardown before fencing or any idempotent shortcut. Captured disposed/finalizing scoped handles cannot tear down a new scope.

Two distinct-name extension methods, no interface additions. `ScopeClosed`, `ViewRelease` and `FinalDisposal` are appended to Phase 4's existing enums, preserving previous numeric values. Unknown custom services return UnsupportedOperation without dispatch. Operational failures are result values; existing navigation methods retain throwing/cancellation behavior, with ObjectDisposedException after teardown closes their target admission.

### Who owns what?

- Native navigation records cache ownership by **actual Region and model reference**, including inactive Views and adopted partial commits. Ordinary navigation/Clear do not dispose that ownership.
- The **application/final navigation owner** requests release and explicitly affirms final-disposal authority for every captured cached model. The predicate is an ownership assertion, not an inference from DI resolution and not a security boundary.
- The DI bridge does not expose ownership transfer. Approving a container-owned/shared singleton without authority is invalid application usage. Applications must coordinate their provider lifetime with awaited teardown. Later calls to BaseViewModel.Dispose are once-only no-ops; this does not make arbitrary custom/provider shadow disposal safe.
- Every native cached owner of a shared model, including inactive or detached caches and retained partial cache records, must be selected together. Other cached owners cause preflight rejection, not best-effort disposal. Weak model-to-Region discovery does not retain roots and does not clear unrelated caches globally.
- Models or UI objects referenced outside native caches remain an application contract. Manual cache/dataSource mutation bypassing native tracking is not adopted as evidence of ownership. Untracked cached trees and foreign sources reject.
- Cache ownership starts when native commit records a View/model. A constructor or detached-preparation candidate that never reached that boundary remains with its existing DI owner, not silently adopted/disposed by cache teardown.

## Deliberately bounded subset

Supported: explicitly selected **native, non-global, single-view Regions**, flat cached trees, complete cached-model ownership, and ordinary native model managers without local/nested Region registrations. Explicit Region references avoid ancestor/global name lookup accidentally selecting a shell.

Unsupported before mutation:

- Parent caches containing nested Regions, even if empty or also listed. This phase does not invent dynamic descendant ownership or reserve undiscovered child scopes.
- Models retaining local/nested Region registrations; custom managers; global Regions whose registration owners cannot be proven; multi-view Regions.
- Untracked cached Views, foreign/moved cached trees, missing ownership authority, a model with another cached owner, or a busy shared model.
- Mixed live/released Region groups. Repeat released groups separately.

A **selected nested leaf** with a flat owned cache can be released independently. Its parent View/model, shell and wrapper remain alive. Parent teardown rejects until a separately authorized broader ownership mechanism exists. There is no recursive/global cache clearing.

Custom VisualElement implementations have no generic safe final-disposal contract. Teardown removes binding/dataSource/hierarchy references; custom UI resources must have explicit owners in the model/activation lifetime. It does not guess that every descendant IDisposable is owned or call arbitrary UI Dispose methods twice.

## Scheduler and quiescence

Teardown uses the **existing RegionNavigationOperations scheduler**, always FIFO, not a parallel coordinator or LatestRequestWins operation.

1. Snapshot the explicit Region set by reference identity. Reject scoped reentrancy/cycles before changing admission.
2. Reserve a terminal admission fence atomically with the ordinary queue reservation. Earlier admitted FIFO/latest work drains normally. New navigation/Clear submissions reject with ScopeClosed; they do not supersede teardown or construct new models.
3. Await every predecessor's actual completion. No callback is detached, stopped or assumed cancelled. An uncooperative callback can keep teardown pending indefinitely; FIFO teardown does not invalidate it at admission.
4. Preflight all caches and ownership **at owned execution time**. A preflight rejection reopens admission and preserves this request's UI/lifetimes. Requests rejected during that temporary fence are not replayed automatically.
5. Acquire final-model leases for the entire validated model set without an intervening await. This prevents an independent Region/factory from acquiring a model after preflight while another model's exit awaits.
6. Mark the selected Regions terminal, hide active Views, end all known activation lifetimes, and attempt one required Away per active logical model. Cleanup failure does not suppress another exit/model's attempt.
7. Remove bindings explicitly by public binding IDs (including pre-first-panel-update registrations), clear remaining bindings and subtree dataSources, then detach owned trees. Clear cache/model records before final model disposal.
8. Attempt each model's existing final Dispose once within lifecycle-origin tracking. Clear framework Region subscriptions, parent/manager/navigation references and ownership bookkeeping. Release final-model leases and the queue only after all safely owned stages have returned.

Model finalization leases are separate from Region ordering, not new navigation queues. Distinct unrelated Regions/models stay concurrent. Region tombstones in weak scheduler state reject stale navigation without retaining a completed operation/its activation set.

Exact-set concurrent teardown calls queue behind the first. Success repeats return NoOp; failure repeats retain the same failure result and never retry partially executed resources. An overlapping **different** in-flight Region set rejects without closing unrelated targets. The scheduler also checks terminal state when queued work reaches execution, covering admission transitions after a rejected teardown.

## Failure guarantees — no false success

- Missing authority/unsafe topology rejects before activation/final disposal with Preserved recovery. No final lease is acquired on partially validated ownership.
- After mutation begins, the scope remains terminal even if cleanup/Away/disposal fails. Attempt all known safely owned stages/models; aggregate observed failures, preserve the first failure stage, and return Failed / Incomplete. No success based merely on completed UniTask, and no rollback/resurrection of disposed models.
- If a foreign source, moved tree or newly inserted nested scope appears while exit awaits, report Incomplete and do **not** overwrite/detach an unowned tree or a running unreserved nested callback. Application repair may be required; do not assume the physical wrapper is empty on failure.
- Ownership metadata/cache references are released even on these reported failures. Original custom errors may themselves retain application references; there is no promise to roll back arbitrary user effects or repair corrupt UI ownership.
- Binding cleanup is observable before final Dispose in the mounted test. No frame sleep, private reflection, forced panel update or renderer manipulation is used. No claim about pixels, player or World Space acceptance.

### Important existing R3 bag limit

Public/protected API compatibility retains `protected ref DisposableBag Disposables`. That opaque R3 bag can stop at a throwing user disposable. Without enumerating private internals or retrying already executed effects, Faciem **cannot guarantee attempting later resources inside that bag**. This is an executable limitation characterization, not a passing leak-free-disposal claim.

Framework Region subscriptions are now separately exception-isolated. Teardown still attempts other model disposals, other Views, activation cleanup and required exits. A user bag failure returns Failed / Incomplete, drops the bag's framework-held references, and is **not retried** by repeated teardown or later provider Dispose. Remaining external user subscriptions may require application repair. Resource Dispose implementations should not throw; a broader safe final-resource API would need separate authorization.

Direct synchronous BaseViewModel.Dispose remains available, now once-only. It cannot await navigation quiescence and is **not** a substitute for awaited teardown of a live cache. Provider/application callers must follow the ownership protocol.

## Executable validation

`Tests/Editor/NavigationTeardownTests.cs`: **46 new distinct cases**: **45 supported/rejection/failure cases +1 opaque-R3-bag limitation characterization**. Real mounted EditorWindow Regions, imported A/B/C Views, configured models and deterministic completion-source gates. Partial-commit tests use an explicitly labelled real Region subclass fault before/after AddView; the preparation boundary test uses an internal synchronous fault seam. They do not claim a native engine construction failure.

Final combined suite: **198 Passed /0 Failed /0 Skipped =46 new + all152 unchanged Phase 1–4/compatibility/disposable-bag cases**. Cache references, fresh scopes, Clear retention, callbacks awaiting entry/exit, FIFO/latest fencing, repeats, stale handles/publication, shell/independent Regions, complete/inactive shared owners, leaf/parent nesting, dynamic nested/foreign ownership, cleanup/Away/Dispose aggregation, binding release and partial ownership boundaries are executable.

History, not hidden retries:

1. Initial 188: **187 Passed /1 Failed**. The strong binding-at-disposal assertion caught a pre-first-update binding left observable after ClearBindings alone. Initial source hashes/copies and failing trace retained.
2. Follow-up: **compile error /0 executed**, diagnostic Count property used instead of LINQ Count(); exact normalized artifact and compiler excerpt retained. Missing NUnit XML is not passing evidence. Fixed the diagnostic and snapshotted binding IDs for explicit removal.
3. Corrected 188: **188 Passed /0 Failed**. Retained repeated-subset observation; an exact source snapshot was not captured at that instant, so it is not final-source acceptance.
4. Final reviewed 198: **198 Passed /0 Failed**, after the explicit stale-handle and dynamic-ownership/concurrency regressions. Final source hashes captured before execution and verified after.

**198 distinct final cases; 574 test observations (573 passed,1 failed), four launches including the zero-test compile failure.** No test-runner retries, backend fallback, Editor closure/repoint, lock deletion or assertion suppression. Formatter and verification exit0. Graphics-enabled isolation is required by mounted panels. PlayMode/player/World Space/Display/audio acceptance intentionally not claimed.

[Exact runs](evidence/navigation-phase5/runs.json) · [46 lifecycle/ownership traces](evidence/navigation-phase5/traces) · [test contracts](evidence/navigation-phase5/contracts.json) · [preservation](evidence/navigation-phase5/preservation.json) · [integrity](evidence/navigation-phase5/artifact-integrity.json).

Normalized/NUnit reports are byte-exact. Any surplus EOF blank-line normalization of text traces has raw/exported hashes; raw exception-stack spaces use narrowly targeted evidence whitespace attributes, not relaxed source checks.

**Stop after the focused draft PR for independent review. No merge, deploy, Display integration or Phase 6.**
