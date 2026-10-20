# Navigation serialization — Phase 1

## Scope and results

**Framework-only change, draft for review.** Base `4189d2bf67f3702debf3a6029e40b56f0b519802`. References: Display [D05.2 / PR25](https://github.com/KingdomHeards3/MafiosisPartyBangers-Display/pull/25), [D05.3 / PR26](https://github.com/KingdomHeards3/MafiosisPartyBangers-Display/pull/26).

Final isolated Unity6000.6.5f1 EditMode run: **33 Passed,0 Failed,0 Skipped** — **18 navigation cases +15 existing compatibility/disposal cases**. The old navigator failed the real-View FIFO regression before integration: the later request had already succeeded while the earlier request was awaiting an exit. After integration, requests wait in order and the final real Region has only the last View active/visible, with no duplicate IDs. Final C# whitespace formatter check passes.

Evidence is under [evidence/navigation-phase1](evidence/navigation-phase1/): normalized final results and NUnit XML, exact run history,18 current lifecycle traces, tested-source hashes and preservation checks. It is real Editor-panel navigation evidence, not a new World Space/Display/player acceptance claim.

## Ordering contract

Use the existing public `INavigationService.NavigateTo` and `Clear` signatures. No new result, cancellation, re-entry or disposal API is introduced.

1. A request resolves its manager/name to concrete Region instances at submission. It captures a deduplicated **reference-identity** snapshot of those targets. A missing Region/ViewId retains the old silent no-op behavior.
2. NavigateTo and Clear reserve a place in the **same FIFO operation order for every overlapping Region**. View lookup, cached-model selection/construction and mutations occur after predecessor completion, not while waiting.
3. Ownership spans the whole operation, including awaited NavigateAway and NavigateTo callbacks. A subsequent conflicting operation cannot mutate that Region during either callback.
4. Disjoint Regions proceed independently, even when their names match or their managers differ. Different NavigationService instances targeting the same actual Region also participate in the same ordering.
5. A request targeting several Regions reserves all of them atomically in submission order. This avoids acquiring overlapping Region groups in opposing lock order. A group waiting on one target also reserves its other targets; this intentional head-of-line blocking applies only to overlapping target sets.
6. Failure faults that request's UniTask, but releases operation ownership in finally. Successors still run: a predecessor's exception does not poison the completion barrier. This does not roll back already-mutated UI state or introduce a new failure outcome policy.

The shared bookkeeping monitor protects only short reservation/graph/release operations. It is never held across user code or an await, and is **not a global navigation lock**. Weak tables are keyed by actual Region/model identity; completed tails and wait edges are cleared. Navigation remains a Unity-main-thread UI API; the scheduler does not marshal worker-thread calls.

FIFO means mutation/callback bodies complete in order for shared targets. UniTask continuations may run inline: it does not promise that one caller's post-await code resumes before another caller's continuation. Target identities are captured at submission; later manager registration changes do not retarget a queued request. There is no latest-request-wins suppression: **all valid queued requests run**.

## Lifecycle reentrancy: scoped handle required

This contract was explicitly selected for Phase1: **inside NavigateTo/NavigateAway callbacks, use the existing protected ViewModel `Navigation` handle**. It carries the calling model's identity through `ViewModelNavigationService`; the framework tracks which operation owns that model's in-flight callback, including across UniTask awaits.

- A scoped Navigate or Clear targeting a Region owned by that callback is **rejected**, not deferred. The returned UniTask faults with `InvalidOperationException` before another operation is reserved or a View/model is mutated.
- Scoped navigation into an independent Region is allowed and can be awaited. If the proposed operation would form an indirect wait cycle (for example A callback → B callback → A), it is rejected too. The short-lived operation dependency graph exists only for ordering/deadlock prevention, not gameplay state or cancellation.
- After the callback completes, the same scoped handle can issue ordinary queued navigation; no stale callback ownership remains.
- Calls through raw root `INavigationService` are **external submissions**. They queue even while a callback is pending. They carry no caller identity, so the framework cannot infer whether the caller is an external event or a lifecycle continuation. **Do not await the raw service on an owned Region from a lifecycle callback**: that bypasses the scoped contract and can self-deadlock. Use the scoped handle for all lifecycle-origin navigation.
- This does not rely on AsyncLocal/ExecutionContext flowing through UniTask. A custom INavigationService implementation retains the original forwarding behavior; the origin checks described here apply to Faciem's own NavigationService, not arbitrary decorators/third-party navigators.

There is no public signature change. The only intentional behavioral rejection is an unsafe scoped lifecycle re-entry/dependency cycle. Public interface/property XML comments document the caller contract. Lifecycle exceptions remain ordinary exceptions; no typed result API is added.

## Preserved behavior

- Cached Views and ViewModels are reused. A→B→A tests use reference equality on mounted real templates and model dataSources, not singleton construction that could conceal re-creation.
- The old View is hidden/deactivated **before** NavigateAway. The target is activated/shown **before** NavigateTo. Awaited entry is now part of operation ownership, but its visibility order is unchanged.
- Already-active NavigateTo still calls To again without Away. This is **not** forced same-ViewId re-entry; callers still need a later explicit re-entry policy if required.
- Clear deactivates/awaits Away, retains cached trees/dataSources/models and does not Dispose. Repeated empty Clear does not repeat Away.
- Existing multi-view TODO, model construction/type lookup, nested Region registration, public Region direct-activation methods and existing failure/partial-mutation behavior are not redesigned here. In particular, this does not sanitize a cache already corrupted by unrelated direct mutations, nor stop user callbacks from mutating UI directly.
- Per-Region model scoping and final cache disposal remain unchanged. The scheduler's callback ownership is not an activation lifetime, and no activation cancellation token or per-entry disposal is introduced.

## Automated tests

`Tests/Editor/NavigationSerializationTests.cs` uses:

- Actual Region/RegionManager and NavigationService, real ViewModelConstructionService through a fixture DI bridge, three real in-memory ViewIdAssets and three imported UXML templates.
- A real mounted EditorWindow panel. No fake Region or empty Region list substitutes for the new navigation tests. Existing narrow compatibility tests remain unchanged.
- Controlled UniTaskCompletionSource gates for exit/entry waits and callback re-entry. No timing sleeps. Only initial panel attachment has a bounded condition wait.
- Public scoped Navigation from recording BaseViewModels. Reference equality, active-ID uniqueness and exactly one visible child are asserted for each completed single-Region sequence. Temporary fixture models are disposed by fixture teardown; no production cache-disposal behavior is claimed.

The18 cases cover:

| Area | Cases |
| --- | ---: |
| A→B→A reuse and exact lifecycle ordering | 1 |
| Concurrent A→B then A→C FIFO / final single active View | 1 |
| NavigateTo then Clear / Clear then NavigateTo | 2 |
| Awaited entry blocks later construction/mutation | 1 |
| Independent Regions with the same name | 1 |
| Same Region through different managers and service instances | 1 |
| To/Away × scoped Navigate/Clear re-entry **after an await** | 4 |
| Scoped calls after callback completion | 1 |
| Await independent child Region / reject indirect cycle | 2 |
| Entry/exit exceptions release queued successors | 2 |
| Clear retains cache; repeated active To compatibility | 1 |
| Overlapping multi-Region groups | 1 |

Root external requests deliberately queue during gated callbacks, proving the guard does not misclassify ordinary concurrent requests as reentrancy. Exception tests observe expected faults and prove the successor still completes. There are no ignored expected failures in the final selection.

### Validation environment and history

An isolated consumer outside the package and Display checkouts resolves this package locally. It uses existing pinned R3/UniTask dependencies and existing validation NuGet configuration. Display's Editor/project was neither closed nor repointed. The mounted EditorWindow requires an active graphics device, so successful runs use graphics-enabled **batchmode**, not an Editor GUI launch.

History is retained rather than presented as a clean first attempt:

1. Incomplete new-consumer NuGet configuration let NuGetForUnity remove copied R3 assemblies: compile/run error, no NUnit result, no navigation evidence. Fixed only the new consumer configuration.
2. No-graphics attempt reached setup but could not initialize the real EditorWindow:1 setup failure, not a framework regression. No error suppression was added; subsequent runs enable required graphics.
3. Graphics-enabled baseline: **0 Passed /1 Failed** on the intended FIFO regression (`Expected Pending; actual Succeeded`).
4. Initial integration had a local `regions` name collision: compile error, no NUnit result. Renamed the inner-Region local only.
5. First integrated navigation selection: **17/17 Passed**. This is an earlier repeated subset, not17 additional distinct tests.
6. Final selection after the scoped-handle compatibility test and alias-manager strengthening: **33/33 Passed**, including18 navigation cases and all15 preexisting Faciem tests.

Reproduce with EditMode filter `Sim.Faciem.Tests` in a closed isolated Unity6000.6.5f1 consumer with `com.sim.faciem` enabled in manifest testables. Enable graphics for these mounted EditorWindow fixtures; no screenshot/player campaign is required. Normalized JSON and NUnit XML bind counts and failure stages to exact runs.

## Deliberately not implemented

No latest-wins cancellation/supersession, active-entry tokens, forced same-screen re-entry, new result/failure APIs, final View/model cache disposal, Display adapter/ScreenHost, gameplay change or dependency-pin update. FIFO may temporarily show an obsolete queued target; serialization alone does **not** provide a latest-accepted-state or stale-callback guarantee.

Stop after Phase1 for independent review. No merge, deployment, Display dependency update or Phase2 work.
