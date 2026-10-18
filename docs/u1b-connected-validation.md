# U1b — narrow compatibility fixes and connected validation

**Verdict: ready for independent review, NOT MERGE-READY.** Editor compilation
and all 24 distinct focused cases pass. Player validation is not established:
preparation stopped before BuildPipeline on the existing untitled scene. The
user-supplied hyperlink Editor asset-ping suppression is a known behavior gap,
not a silent compatibility success.

## Inputs and approved scope

Start Faciem **3eb408fe39a0d437eef156595ae047ff3abaaeac**, Utility
**d57864989f8b85332ebb8093da47602e68b39132**. The user supplied NuGet/settings
and source fixes, requested editable local packages, and explicitly authorized
connected tooling plus restoration of the three disabled integration cases.
Their tracked changes were snapshotted before edits; the hash record is retained.
The companion Utility test-reference/evidence change is committed at
**0c308f7df1aebf6ad31b6e1141da37f3665e48a1** in Utility PR #1; runtime/schema
remain unchanged.
No Midnight Wager operations, Editor closure, second Editor or batchmode launch.

CleanConsumer remained open at the exact project path with Unity **6000.6.5f1**,
CLI **1.0.0-beta.13**, explicitly installed Pipeline **0.8.0-exp.1**, PID **46808**,
port **7801**. Runs were serial connected EditMode selections, not PlayMode.
These are **local mutable package** results, not a new immutable Git-pin campaign.
[Source hashes](../Documentation~/evidence/u1b-connected/source-sha256.json)
bind the resulting reviewed implementation; user-input hashes are also retained.

## Narrow changes

- Faciem declares the approved standalone **Utility 1.0.0-alpha.1**, **R3.Unity
  1.3.1**, **UniTask 2.5.11** and **UIElements 1.0.0** dependencies. The existing
  consumer's immutable Git overrides/local links remain explicit. NuGet R3 is
  separate from its UPM integration; no fictional UPM DLL package is introduced.
- Remove only two stale **Bebop.Monads.dll** precompiled metadata entries.
  No Bebop C# usage was found, and no replacement DLL is invented. Existing
  assembly identities/GUIDs and other GUID references are preserved.
- Guard five runtime/shared Editor imports; qualify both runtime converter
  registration attributes without changing converter logic.
- Navigation uses the existing `MonoScriptReference.GetReferencedType()` and
  serialized assembly-qualified name instead of Editor-only Script.GetClass.
  DI construction, cached model reuse, owner assignment and navigation callbacks
  are retained and tested; no root-independent API is added.
- Guard missing Addressables settings/default groups and helper/label paths.
  Missing prerequisites return null/false/no-op rather than generating project
  settings or groups. Preserve the user's FaciemSetup missing-settings warning.
- Restore Utility's original five integration cases/assertions and explicitly
  reference real R3.dll in its Editor test asmdef. This small repair belongs to
  Utility PR #1; no runtime model/schema is moved, duplicated or changed.
- Add seven bounded Faciem tests and an isolated-consumer smoke-build helper.
  Test-only doubles exercise existing interfaces, not substitute frameworks.

## Actual dependencies, not requested-version claims

The final lock contains **27 UPM packages**, **Addressables 2.8.0**,
Pipeline **0.8.0-exp.1**, UTF **1.8.0**, NUnit **2.1.0**. UIElements is explicit.
The user's NuGet inputs/installed directories are **R3 1.3.1**,
**Microsoft.Bcl.TimeProvider 8.0.0**, **System.ComponentModel.Annotations 5.0.0**,
**System.Threading.Channels 8.0.0**. Unsafe is not a NuGet package in this
consumer; do not claim complete player dependency closure from Editor success.
No speculative version upgrade or manual host BCL DLL copy was performed.

The [exact manifest, lockfile and NuGet configuration](../Documentation~/evidence/u1b-connected)
retain local package paths and immutable external Git revisions. Historical
Addressables 2.11.2 and earlier missing-TimeProvider attempts are not fresh results.

## Compilation, tests and formatting

- Baseline recompile reported scripts up to date, not a newly triggered compile.
- After compatibility fixes: **12.2s, zero compiler errors**.
- The new smoke helper initially used the wrong SerializableGuid namespace and
  failed with CS0246. It was corrected to the existing **Sim.Dispositio.Shared**
  type/string constructor; subsequent compile **10.8s, zero compiler errors**.
  The helper mistake is retained as a nonpassing observation, not hidden.

| Final serial selection | Executed/passed | Failed/skipped/inconclusive |
|---|---:|---:|
| Sim.Faciem.Tests.FaciemCompatibilityTests | 7 | 0 |
| Sim.Utility.Faciem.Tests.GuidInspectorIntegrationTests | 5 | 0 |
| Sim.Utility.Tests.SerializableGuidTests | 12 | 0 |

**24 distinct passing cases.** Six runs including baseline/overlap total
**48 passing observations**, not 48 distinct cases. Native test names/counts,
durations and exact normalized artifacts are retained in the
[machine report](u1b-connected-validation.json). No XML is claimed for these
connected runs. The seven Faciem cases cover serialized type resolution without
MonoScript, unknown type names, DI/cached navigation policy, missing settings
and persistent-asset helper paths. They are not mounted native UI acceptance.

Eight Utility static package contracts pass. `dotnet format whitespace` with
14 explicit absolute C# paths and verify-no-changes exits 0; no formatter-only
layout change is claimed as a new compile/test run.

## Player prerequisite failure: stop, not workaround

After the passing compile/test gates, one connected eval called
`FaciemPlayerSmokeBuild.Build()` with 600-second host/dispatcher bounds. The
helper requested an owned additive scene and failed at `NewScene`:

```text
Cannot create a new scene additively with an untitled scene unsaved.
```

**BuildPipeline.BuildPlayer was not reached.** There is no BuildReport,
player compilation result or produced/launched executable. The anticipated
Windows64/Mono2x configuration was read from existing settings, not imposed.
The helper restored/checked the original scene state; before/after typed
inspection showed exactly one loaded, active, clean, untitled scene with two
roots. The owned smoke scene does not remain. No user scene was saved, replaced
or closed. No retry, cancellation, backend switch, isolation workaround or
automatic Editor closure occurred. [Exact attempt/restoration record](../Documentation~/evidence/u1b-connected/build-attempt.json).

## Known behavior gap and evidence limits

The user's supplied HyperLinkLabel edit comments out asset pinging through its
serialized **List<int> InstanceIds**. It is preserved, not claimed as restored.
Bounded inspection of this Editor's **public API** confirms both implicit
int/EntityId conversions and `EditorUtility.InstanceIDToObject(int)` are marked
obsolete **with IsError=true**. No unsafe/private conversion or serialization
migration is invented to hide that limitation. Review/approve an explicit
behavior or serialized-ID migration decision separately before claiming full
Editor behavior preservation.

An ancillary public GUID-name query was rejected at Roslyn compilation because
the queried CompilationPipeline member was absent. No execution/mapping or
absence was inferred; those GUID references remain intact. An exact-version
documentation URL resolver also returned uncertain URL evidence, not a successful
documentation check. These are separately recorded, not product test passes.

Five Utility cases now pass, but full bound Inspector regeneration/readonly/
lifecycle/IMGUI acceptance is still not established. No IL2CPP, device,
Addressables-content, root/Display or redistribution/legal acceptance is claimed.
Existing license/notice questions are not resolved by successful compilation.

**Next gate:** explicitly resolve the user's unsaved-scene prerequisite before
another player attempt, then assess the remaining Editor/Inspector behavior
limits. Do not merge, auto-merge, deploy or silently retry. Stop for independent
review with both draft PRs clearly limited to their actual evidence.
