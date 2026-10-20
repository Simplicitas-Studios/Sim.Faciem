# Phase 3 — explicit same-ViewId re-entry

**Draft for review; stop after Phase3.** Base main `2e2ccaa37031202cdc137752a958a21e1d540282`, containing merged [PR4](https://github.com/Simplicitas-Studios/Sim.Faciem/pull/4) through [PR3](https://github.com/Simplicitas-Studios/Sim.Faciem/pull/3). Neither prior worktree is changed. No Display source or dependency pin change.

Final isolated Unity6000.6.5f1 EditMode result: **96 Passed,0 Failed,0 Skipped** — **35 new re-entry cases + all61 Phase1/Phase2/compatibility/disposal cases**. An earlier93-case pass is retained as a repeated subset, not93 more distinct cases. No failing test, retry or backend switch occurred. Final C# whitespace formatter passes. Exact normalized JSON, NUnit XML,35 current traces, source hashes and preservation checks are in [evidence/navigation-phase3](evidence/navigation-phase3/).

## API: ordinary navigation versus re-entry

Existing interfaces and existing public overload signatures remain unchanged. Default behavior remains:

```text
NavigateTo(A while A is active)
    same cached View and VM
    same valid activation
    NavigateTo callback again
    no NavigateAway, no activation cleanup/replacement
```

Different NavigationParameters do **not** infer a new event, room or activation. Ordering policy also does not imply re-entry. Callers must explicitly choose `NavigationEntryMode.ReenterIfActive`.

Only one enum and two new public extension overloads are added, combining policy and entry mode:

```csharp
// External request: explicit activation restart, FIFO ordering.
await navigation.NavigateTo(manager, viewId, region, parameters,
    NavigationPolicy.Fifo, NavigationEntryMode.ReenterIfActive);

// Or opt into Phase2 supersession as well.
await navigation.NavigateTo(manager, viewId, region, parameters,
    NavigationPolicy.LatestRequestWins, NavigationEntryMode.ReenterIfActive);

// From a ViewModel command/event outside an in-flight lifecycle callback:
await Navigation.Navigate(viewId, region, parameters,
    NavigationPolicy.Fifo, NavigationEntryMode.ReenterIfActive);
```

`NavigationEntryMode.Default` explicitly selects ordinary behavior. New overloads require the policy and mode arguments rather than adding competing same-arity enum overloads: existing calls using a `default` policy literal stay unambiguous. Custom navigation services still support Default/Fifo forwarding, but explicit re-entry throws NotSupportedException rather than silently ignoring the request. Invalid enum values fail before reservation/mutation. There is no re-entry option on Clear.

No application identity, parameter comparison, server revision, room ID or gameplay routing is introduced.

## Execution and lifecycle order

The request captures its Region targets and joins Phase1's existing operation ordering. **Whether the requested View is already active is evaluated when the operation owns its Regions**, not guessed from submission-time state. If earlier requests have already navigated away, ReenterIfActive follows ordinary entry and does not manufacture an extra exit.

For an active target, after preflight:

1. Hide/deactivate all old active Views in the captured target group. Cached trees remain attached and dataSources remain owned.
2. End each old logical model activation exactly once: invalidate its captured handle, cancel its token, attempt all owned cleanup, then await NavigateAway.
3. Wait for all exits before restoring any target View. Shared old models receive **one Away callback per explicit group restart**, not one reset per physical tree. Distinct old models each exit once in deterministic first-Region/active-ID order.
4. Restore the exact requested cached View/model, set the same dataSource back as in ordinary navigation, and show/activate it. No new tree or replacement VM is constructed for a valid active re-entry.
5. Create a fresh activation, then invoke NavigateTo with the new parameters. The standard show-before-To order remains unchanged.

The single-Region asserted trace is:

```text
A:To:first:active/Flex
A:To.return:first
A:cleanup
A:Away:inactive/None:null
A:Away.return
A:To:second:active/Flex
A:To.return:second
```

Activation cleanup precedes Away; its protected Activation is null during exit. The new activation is available in To. Reference-equality assertions establish identical cached View/model objects; the new handle is reference-distinct. The old handle remains invalid after return and cannot publish into the new lifetime through TryPublish.

Re-entry has a deliberate hidden interval while an exit callback awaits. It is **not** a seamless visual crossfade, renderer/root replacement, tree detach/rebuild or final cache disposal.

## Serialization, supersession and stale work

The Region queue and supersession engine themselves are unchanged in this phase. Entry mode is request-local input carried through the existing service/scoped paths.

- FIFO re-entry, ordinary navigation and Clear execute in their reserved order, including async callbacks.
- A superseded **queued** re-entry never reaches restart logic: no exit, cleanup, activation replacement, entry or View mutation. A valid current activation remains intact unless the actual winning operation exits it. The tests include an ordinary same-ID winner that leaves the original activation valid with **zero** cleanup/Away.
- If re-entry has already begun exit, the old activation may already be ended. Supersession cannot undo that; it waits for Away to drain, then skips the obsolete new activation/entry. It does not resurrect the old lifetime.
- If the fresh entry is already awaiting, Phase2 invalidates its handle and signals cancellation but retains all Region ownership until the callback returns. Then that exact interrupted handle is cleaned and its owned targets hidden. The later winner creates/uses its own lifetime; obsolete cleanup cannot end the winner's handle.
- Cooperative entry can unwind on its token; uncooperative entry is never detached. A View already shown before supersession cannot be retroactively unshown, and arbitrary writes bypassing captured-handle TryPublish are not prevented. These Phase2 limitations remain explicit.
- Capture Activation before awaits and publish via that captured handle. Old work physically completing after re-entry is tested: it runs, but its guarded publication returns false.

There is no new public caller cancellation API. Supersession uses the existing opted-in policy; lifecycle faults/cancellations retain the existing exception/UniTask completion contract.

## Multi-Region and shared-model constraints

Re-entry must not reset a logical activation behind an unowned Region's back. The new path therefore preflights the **whole group before hiding or ending anything**:

- All target Regions must be single-view Regions. Explicit re-entry into SupportMultipleViews is NotSupportedException, not a silent success.
- When any requested target is active, all cached requested Views in that request must reference the **same cached model**. Heterogeneous caches reject with InvalidOperationException, preserving both models' current activations and exact identities. Use separate re-entry requests for independent cached models/Regions; they remain concurrent.
- Every currently active Region sharing an exiting model must be among the operation's captured targets. Partial shared-cohort restart rejects before reset. This includes other active ViewIds referencing that model, not only the requested ID.
- Phase2's busy-model guard still rejects concurrent lifecycle use of one model by independent operations. A different Parent manager does not bypass ownership.
- Empty/inactive targets follow ordinary entry semantics; the legacy multi-Region cache-selection behavior is not redesigned. The strict restart checks apply when an active target will actually be restarted.

To validate complete cohorts without private root discovery or relying on mutable Parent manager references, BaseViewModel records its framework-activated Views using **weak Region references**. Validation checks the Region's actual active IDs and cached dataSource. Stale/dead bindings are pruned during later activation tracking. This is internal ownership bookkeeping, not a new navigation registry, strong root lifetime, cache-disposal API or ScreenHost. Direct external corruption of Region caches/active IDs remains unsupported, as in Phase2.

A valid shared cohort keeps each existing tree and one model; it ends once and enters once with one fresh logical activation. Mixed captured groups can exit other old models before restoring the shared requested cache. Group supersession remains whole-operation on any overlap: already-started exits are drained and no subset is reactivated by the obsolete request. Only the winning request's captured targets activate.

## Failures and scoped restrictions

There is no navigation result redesign or rollback protocol:

- Under-ownership unsupported/group checks do not themselves hide Views or reset activations. As in Phase2, LatestRequestWins admission may already have invalidated older opted-in in-flight requests; that supersession is not rolled back if the newer request later fails preflight. A target model preparation may also have occurred under the legacy selection path; this phase does not add final disposal of prepared candidates.
- Exit/cleanup failure happens after the old Views are hidden. Old activation cleanup is attempted once; failed exit does not create a fresh activation or call To. No old lifetime is restored automatically.
- Entry failure ends the newly created activation. Ordinary Phase2 partial visibility/active-ID failure behavior remains; a failed entry does not invent an extra Away callback. Subsequent queued navigation can recover on the same cache.
- Supersession and callback/cleanup faults preserve Phase2 exception precedence; unexpected faults are not silently reported as successful cancellation. Queue ownership releases in finally only after the active callback/cleanup drains.
- Lifecycle and cleanup callbacks must use their scoped Navigation handle. Same-Region scoped re-entry is rejected, not deferred, even if explicitly requested or LatestRequestWins. Validation precedes supersession, so an unsafe nested request cannot cancel legitimate queued work. Raw root INavigationService remains external-only during lifecycle callbacks.

## Automated coverage and reproduction

`Tests/Editor/NavigationReentryTests.cs` uses the actual NavigationService and ViewModelConstructionService, real Region/RegionManager instances mounted in a temporary EditorWindow, real in-memory ViewIdAssets, and the existing imported A/B/C UXML test templates. No fake Region, empty-list replacement, private API invocation or application state is used. Races use controlled UniTaskCompletionSource gates, not timing sleeps. Only initial panel attachment has a bounded condition wait.

The35 cases cover:

- Ordinary same-ID default/Latest parameter updates retain the lifetime; explicit FIFO/Latest restart asserts exact trace, cached identities, transient reset and cleanup once.
- Old async work cannot publish; inactive/queued target-state changes avoid extra exits.
- Both FIFO orders against Clear and another View.
- Queued supersession by ordinary same-ID/another View/Clear, with no unwanted reset.
- Supersession during exit by all three winners; cooperative/uncooperative interrupted fresh entry cannot end the later winner.
- Away/To/cleanup failures and queue recovery; scoped lifecycle restrictions and valid scoped re-entry outside callbacks.
- Independent Regions, complete shared cohorts, mixed old-model groups, rejection of partial/heterogeneous/busy cohorts, queued and executing group supersession.
- Custom-service Default forwarding, unsupported explicit mode, invalid mode values, old `default` policy literal compatibility, and multi-view rejection.

Run EditMode selector `Sim.Faciem.Tests` in the isolated validation consumer. Graphics-enabled batchmode is required for these real EditorWindow panels, as established in earlier phases. All61 baseline tests remain unmodified and pass. Current traces distinguish primary assertions from fixture cleanup and include View/model identity hashes, lifetime counts and resource/final disposal counts; reference equality and active-ID/visibility assertions are authoritative.

NUnit XML and normalized JSON are copied byte-exact. One empty-model trace has a surplus EOF blank line removed for Git whitespace hygiene; raw/exported hashes and the transformation are recorded in runs.json, with the raw file retained in validation Logs.

No PlayMode/player/World Space gameplay campaign is claimed. Existing source/assets/metadata and prior phase worktrees are preserved. No final cache disposal, Display code/pin change, merge, deployment or integration. **Stop after Phase3 for independent review.**
