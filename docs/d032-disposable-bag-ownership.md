# D03.2 prerequisite — ViewModel disposable ownership

## Status / authorization

**Local fix prepared for independent review; NOT published or approved for Display.** The user explicitly authorized a separate minimal Faciem fix and regression tests after a native failing prerequisite. No push, merge, package upgrade or Display pointer change. Local branch: `fix/d032-disposable-bag-ownership`, worktree `sim-faciem-d032-disposal`, base **6607c4cae686ddc13de9ba1da979caa7dca30320**.

Display remains at main **19535255b73b330ed1efda07954da17edcd9047e** with this **unchanged** Faciem pin, Utility **7f79725d1716d3c71f2f66ca41d2f35bdfa6f836** and Injectio **e499f61abe9b736d337bc3d3043a9909235a20a7**. **D03.2 is still BLOCKED / NOT MERGE-READY.** Passing tests against the separate fixed source do not prove the pinned Display implementation passes.

## Defect and minimal change

R3 **1.3.1** supplies `DisposableBag` as a mutable **struct**. Faciem originally exposed it as a get-only, by-value auto-property:

```csharp
protected DisposableBag Disposables { get; } = new();
```

Calling `Disposables.Add(...)` mutates a temporary value copy, so the original bag never retains the new resources. `Disposables.Dispose()` also mutates a copy. This affects BaseViewModel, derived ViewModel<T> and the explicit IDisposableContainer registration path. The native Display prerequisite confirms that disposing through BaseViewModel leaves the shell subscribed: expected one projection read, observed two after a source event.

The production change is limited to **Runtime/BaseViewModel.cs**:

```csharp
private DisposableBag _disposables = new();

protected ref DisposableBag Disposables => ref _disposables;
```

Normal existing `Add` / `Dispose` calls now mutate the one instance-owned bag. Public non-virtual Dispose and interface ownership remain the existing implementations; no hidden derived Dispose, parallel lifetime container, reflection workaround, navigation hook or UI activation was introduced. R3 retains its own disposal/late-add semantics. No package/asmdef/version, original asset GUID, serialized GUID format, dependency or region/navigation behavior was otherwise changed.

**Compatibility limit:** the protected property retains its name/accessibility and normal source call syntax, but its CLR getter return signature changes to a managed reference. Dependent compiled assemblies require recompilation. This is not a binary-compatibility claim for precompiled consumers. The isolated Unity consumer rebuilt source-dependent assemblies successfully; review this protected-API change before publishing or approving a new pin.

## Exact red/green evidence

Unity **6000.6.5f1**, CLI **1.0.0-beta.13**; isolated closed projects, no graphics; no existing Editor closed or GUI launched. Each run has matching native NUnit and normalized records; no retry without an implementation change.

| Phase | Result |
|---|---|
| Pinned Display shell prerequisite | **0 passed /1 failed**: BaseViewModel disposal did not detach the subscription |
| Faciem baseline6607c4c + new regression fixture, before fix | **0 passed /8 failed** |
| Separate Faciem fixed source + existing focused fixtures | **32 passed /0 failed /0 skipped /0 inconclusive** |

Final32 cases: **8 new disposal**, **7 existing Faciem compatibility**, **12 Utility core**, **5 optional Utility/Faciem inspector**. Native final test execution duration **0.2400825s**, not import/launch time. New cases cover base/interface ownership and repeated disposal; generic ViewModel inheritance; IDisposableContainer registration; immediate late disposal; independent instances; bag growth; and R3 subscription detachment. A non-idempotent counting disposable prevents resource-level idempotence from masking multiple owner disposals.

Native red normalized artifact: `pi-unity-tests-editmode-20261018T144603381Z-ad7ded1e903f493b80d493b049a6b68f.json`.

Native green normalized artifact: `pi-unity-tests-editmode-20261018T144708154Z-e4eb451b07124a6ca1ad28934fd38ddf.json`, matching `unity-tests-editmode-20261018T144654944Z-e9b9d7ed0ca64fcf.xml`.

[Exact records, source hashes, warnings, consumer inputs and integrity](../Documentation~/evidence/d032-disposal/validation.json). Raw full logs remain local with size/hash; exact JSON/XML copies are retained. The green run has zero compiler errors. Existing package warnings remain attributed in the machine report; no out-of-scope warning suppression/fix. Formatter verification for the two C# files passed.

## Isolated consumer, not a Display override

The new disposable-ownership consumer is outside the Display and Faciem source repositories. It reads this local fixed Faciem worktree and the **unchanged** Utility checkout, using genuine already-tracked Display NuGet files copied byte-for-byte. R3.dll integrity was compared directly; no host BCL substitutions, package upgrade or fake assembly. Actual standalone resolution uses Addressables **2.11.2**, UTF **1.8.0** and the same immutable R3.Unity/UniTask pins. Optional Utility inspector integration is included only in this separate library regression consumer, not added to Display.

Display's own manifest, lockfile and submodule pointer were never redirected to the fixed checkout. The original pinned Display failure remains evidence, not a subsequently passing run. Full D03.2 initial/state/reconnect/stale/DI/cleanup tests and Display regression delivery are pending an approved dependency change. No player, PlayMode, binding/visual/native PanelRenderer, async navigation or cached-view lifetime acceptance is claimed. This does not reopen prior WebSocket, presenter-lifecycle or scene-preservation investigations.

## Review / stop boundary

Review the minimal ownership fix, protected ref-return signature, eight regression cases and exact32-case result locally. **Do not push, merge, update Display's pin or activate its shell/UI without new authorization.** After an approved upstream commit is explicitly selected for Display, resume the full D03.2 validation/delivery; do not substitute the standalone green result for it.
