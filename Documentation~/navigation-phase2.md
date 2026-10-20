# Phase 2 — opted-in supersession and activation lifetimes

**Draft, framework-only; stop after Phase2.** Baseline [PR3 / Phase1](https://github.com/Simplicitas-Studios/Sim.Faciem/pull/3), commit `67895a8531ac3dec3595f49089859a40dd3b1421`. Phase1 is approved but unmerged; this PR is stacked on its branch, not merged into main. No Display pin or application code changes.

## Results

Final Unity6000.6.5f1 isolated graphics-enabled EditMode run: **61 Passed,0 Failed,0 Skipped** — **28 new Phase2 cases + all33 Phase1/compatibility/disposal cases**. Two earlier successful runs (55 and58 cases) are retained as repeated observations, not extra distinct cases. Final C# whitespace formatter passes. No failed test, retry or backend switch occurred during Phase2.

[Exact results/run history](evidence/navigation-phase2/runs.json), normalized JSON/NUnit XML,28 current lifecycle traces, tested-source hashes, preservation and integrity checks are under [evidence/navigation-phase2](evidence/navigation-phase2/). This is mounted EditorWindow-panel proof, not Display/World Space/player acceptance. No scene, gameplay routing, server revision or game identity is introduced.

## Minimal API additions

Existing INavigationService and IViewModelNavigationService **signatures are unchanged**. Existing calls remain FIFO. New extension overloads accept `NavigationPolicy.Fifo` or `NavigationPolicy.LatestRequestWins` for root NavigateTo/Clear and scoped Navigate/Clear.

```csharp
// External application intent. The old overload, without policy, remains FIFO.
try
{
    await navigation.NavigateTo(manager, viewId, region,
        NavigationParameters.Empty, NavigationPolicy.LatestRequestWins);
}
catch (OperationCanceledException)
{
    // This opted-in request was superseded; it is not a successful activation.
}

await navigation.Clear(manager, region, NavigationPolicy.LatestRequestWins);
```

Custom navigation implementations still support explicit Fifo forwarding. Latest on a custom implementation/decorator without Faciem's implementation throws NotSupportedException rather than silently pretending to supersede. Invalid policy values are rejected before reservation. No new navigation result/failure type is added: superseded requests use ordinary cancelled UniTask/OperationCanceledException; user lifecycle and cleanup faults remain observable exceptions.

BaseViewModel gains a protected `Activation` handle. `NavigationActivation` exposes:

- `CancellationToken`: cooperative invalidation signal, not forced termination.
- `IsValid`: this captured handle is still valid **and** current for its model.
- `TryPublish(Action)`: run a bounded synchronous write only if this captured handle is valid, on its owning Unity thread.
- `Own(IDisposable)`: per-activation cleanup ownership, separate from final-model Disposables.

```csharp
protected override async UniTask NavigateTo(NavigationParameters parameters)
{
    NavigationActivation entry = Activation; // Capture BEFORE the await.
    entry.Own(SubscribeToPresentationInput());
    string text = await LoadTextAsync(entry.CancellationToken);
    await UniTask.SwitchToMainThread();
    entry.TryPublish(() => Caption = text);
}
```

The subscription/load/property in the example are application operations, not new framework abstractions. Do not reread `Activation` after an await to authorize an old job: that could be a **new** lifetime on the same cached model. Capture the entry handle. Do not use async-void actions, await, or initiate navigation inside TryPublish. All UI/ownership/publication operations remain on the owning Unity thread; background publication throws. CancellationToken may be passed to cooperative work.

## Supersession contract

### Admission and queued requests

- Target Regions are captured by **reference identity**, using Phase1's atomic multi-Region reservations and FIFO barriers. Missing Region/ViewId remains the old silent no-op and is not admitted as a superseding request.
- A newer **LatestRequestWins** request marks every incomplete older **LatestRequestWins** request with any overlapping target Region obsolete. It never cancels a Fifo request. A later Fifo request also does not supersede opted-in requests. Applications wanting latest intent for a Region must consistently opt those intents in.
- Supersession is whole-operation: if old targets are `[R1,R2]` and new targets are `[R2]`, the old request is cancelled for **both** old targets, not partially executed. Disjoint requests are unaffected.
- Queued obsolete requests drain their predecessor barriers, then complete cancelled **before** model lookup/construction, View/dataSource mutation or lifecycle calls. Their reservation is not released early: that would let a successor overtake an older callback. A cancelled queued task can therefore remain pending behind a slow predecessor.
- Supersession is not undone if the newer request later fails. There is no automatic resurrection of an obsolete request, fallback route, back stack or new failure-result policy.

Scoped reentrancy/cycle validation runs **before** any supersession side effects. A rejected scoped request cannot cancel legitimate queued work.

### Operations already executing

Checkpoints after awaited exits and before target commit prevent a newly obsolete operation from continuing to mount/show/enter its target. Construction or other effects performed **before** supersession cannot be undone. For example, a target model may already have been prepared before an old screen's awaited Away; it is not entered after supersession and remains subject to the existing final disposal owner, not a new cache-disposal path.

If To is already running, the framework invalidates its captured activation and signals its token immediately, **outside the reservation lock**. It retains all Region ownership while awaiting that callback:

- Cooperative work can observe cancellation and unwind. Cleanup runs, the obsolete target is hidden in the captured owned Regions, and only then can the next request mutate them.
- Uncooperative work remains awaited. The token is cancelled and TryPublish is false, but cleanup and successor activation wait for the callback to return. No timeout, detached callback, forced completion, or early barrier release is used.
- An executing Away is allowed to drain fully; its previous activation has already ended. Supersession is checked afterward, before further target work. The framework does not abandon or separately cancel the exit callback.
- The stock **show-before-To** order is preserved. A View already shown before a newer request arrived may remain visible while its uncooperative callback drains. Cancellation cannot retroactively prevent that display, undo audio/network effects, or stop raw user writes bypassing the validity contract. The provably safe subset is **no obsolete queued/pre-commit activation**, fenced publication, and no overlapping Region mutation. A hidden asynchronous prepare/commit protocol would be an additional API contract, not silently introduced here.

An aborted entry is resource-cleaned and hidden without inventing an extra NavigateAway callback. Application-owned activation work belongs in Own; do not rely solely on Away to clean a failed/cancelled entry. Normal successful navigation away/Clear still calls Away after cleanup. Ordinary non-supersession entry faults preserve the previous partial visibility/active-ID behavior, but end the failed activation so its async publication is invalid.

## Activation lifecycle and cleanup

- Initial To gets a new handle before callback invocation. Already-active To retains the valid current handle and still calls To again without Away, as in Phase1. There is **no forced same-ViewId re-entry**.
- Navigation away/Clear hides the old View, ends its activation, then invokes Away. It cancels once and attempts every registered distinct resource once, without disposing the cached VM or clearing cached dataSources/trees. Repeated empty Clear performs no extra cleanup.
- Returning after exit creates a fresh activation on the **same cached View and VM**. The old handle remains invalid forever, even after return.
- Invalidation and resource cleanup are distinct: superseding an in-flight entry immediately cancels/invalidates, while cleanup waits until callback ownership can drain. Resources added during this interval join that deferred cleanup. Own after cleanup disposes the newly supplied resource immediately. Register each resource once; Own is an ownership registration, not object-reference deduplication.
- Cancellation notification can synchronously resume user code. Cleanup waits for notification to finish before disposing the token source/resources; reentrant completion does not release the old Region early. No cancellation callback runs under the bookkeeping lock.
- Throwing cancellation observers are retained as errors, not allowed to lose the newly reserved request. Cleanup attempts all resources; errors are aggregated and surfaced from the owning operation. An unexpected callback/cleanup fault takes precedence over a clean cancellation. Successor barriers still release in finally.
- Disposables and the existing final Dispose stay unchanged. This phase does not add final VM/cache/root-detach disposal. Owners must still Clear/quiesce navigation before final provider teardown.

## Multi-Region and shared-model contract

Atomic group reservations, whole-request supersession and Phase1 scoped reentrancy remain in force. Group cancellation drains the callback, cleans its activation once, and hides its target only in Regions the group already owns. It never reaches into unowned Regions to manipulate their Views.

The legacy navigator may assign **one cached model to several Region Views** for one request. That model has **one logical activation cohort**, not independent per-Region mutable state. Leaving any member ends the model's activation once; a retained View in another, unowned Region may remain visible but its old work is invalid. A later To there can create a fresh handle without an extra Away or forced re-entry. This behavior is explicitly tested.

Distinct Regions are independent only when their models are independent. An attempt to enter/leave/clear a shared model while another Region operation is still inside that model's lifecycle is rejected with InvalidOperationException **before touching that busy model's Views, parent link or activation cleanup**. This prevents an unrelated operation from disposing the busy callback's activation. A prepared unrelated target model may already have been constructed; no final-disposal redesign is attempted. Tests cover same-model To, another target, and Clear. Use distinct models for persistent shell and independent screen lifetimes.

Scoped callbacks and activation cleanup must use the existing ViewModel `Navigation` handle. Same-Region re-entry and wait cycles are rejected, including after awaits and from cleanup. Raw root INavigationService is external-only during callbacks; awaiting it from an owning lifecycle can still self-deadlock because it carries no caller identity. The Phase1 contract is not relaxed.

## Executable proof

`Tests/Editor/NavigationActivationTests.cs` adds28 cases using actual mounted Regions/Views, real in-memory ViewIdAssets and the existing imported A/B/C UXML templates. Recording models are transiently constructed through the real framework construction service. Controlled completion sources drive races; only initial panel attachment has a bounded condition wait.

Coverage includes:

- Obsolete queued requests: no model construction, no View, no callbacks.
- Mixed FIFO/latest compatibility and retained default behavior (all33 existing cases also pass).
- Supersession during awaited exit, cooperative/uncooperative entry, executing Clear, both queued Navigate/Clear orders.
- A→B→A exact cached identities with fresh lifetimes; repeated active same-ID handle retention.
- Deferred, immediate-late and exactly-once cleanup; cancellation/disposal failures and queue recovery.
- Independent Regions; whole queued/running multi-Region supersession; logical cohorts and rejection of concurrent shared-model callbacks.
- Entry/exit exceptions; captured old work physically completing after return but failing TryPublish; owner-thread enforcement.
- Scoped reentrancy rejected before queued supersession; cleanup reentrancy; custom-service Fifo support and explicit Latest rejection.

Run EditMode filter `Sim.Faciem.Tests` in the isolated validation consumer. Graphics are required by the real EditorWindow panel, as established in Phase1. Current lifecycle traces distinguish To/Away, activation cleanup, distinct handle counts, final model disposal, and publications. Identity and active-ID invariants are asserted in executable code, not inferred from repeated counts.

## Stop boundary

No Display-specific state, authority/revision/game routing, pin update, custom ScreenHost, forced re-entry, final cache disposal, new result API, scene or production integration. No merge/deploy. The safe in-flight subset and raw/scoped caller contract are explicit review limitations, not guarantees of forced cancellation or suppression of arbitrary user code. **Stop after Phase2.**
