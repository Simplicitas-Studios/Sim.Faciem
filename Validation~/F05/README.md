# F05 validation

## Accepted evidence

**Final 874/874** on Unity 6000.6.5f1, isolated Unity CLI, graphics enabled:

- **198** unchanged existing Faciem cases (serialization, activation, re-entry, outcomes, teardown, compatibility and disposal).
- **25** new `NavigationTypedViewTests` cases using native navigation and mounted EditorWindow Regions.
- **1** actual imported `SelectedItemView` / `SelectedItemViewModel` / UXML sample execution, not a duplicated sample implementation.
- **628** previously accepted production Display cases, with the identical 41-fixture selection recorded from D05.10.
- **22** unchanged actual Display World Space cases: 15 D05.9 ownership/lifecycle +7 D05.10 Trivia scene cases. These are compatibility regressions with optional metadata absent, not a typed Display screen migration or typed destructive-root acceptance.

Separate required native-framework dispatch also **224/224** =198+25+1. The final 44 selectors and expected partitions are recorded in `Evidence/acceptance-selection.json`. A broader all-tests dispatch included eight unrelated extra cases and remains uncertain; it is **not** promoted to passing full-suite evidence.

## All attempts retained

| Attempt | Records | Outcome / explanation |
| --- | ---: | --- |
| Initial typed tests | 21 pass | Initial native cache/activation/constructor/OnBind coverage. |
| Extended typed tests | 24 pass /1 fail | Real new bug: R3's initial replay callback error did not throw through Subscribe, so navigation falsely succeeded. Fixed by owning the subscription and rethrowing the captured replay error through native entry containment. No log/assertion suppression. |
| Broad first campaign | 880 pass /1 fail /1 existing skip | Scratch consumer omitted unchanged `docs/display/evidence/d04-4/d044-characterization-matrix.json`; restored existing fixture payload, no production/test assertion change. |
| Broad restored campaign | 881 pass /0 fail /1 existing skip | **Uncertain** tool outcome despite Unity exit 0. Exact current-run JSON/XML/log inspected once. Existing ignored unanswered WebSocket handshake case remains ignored; no same-run/backend retry, forced close, or fallback. |
| Required native namespace | 224 pass | Independent relevant framework selection, no ignored transport case. |
| Final accepted selection | 874 pass | Prior 628-fixture Display contract +22 real scene cases +224 framework/sample; no failures/skips. |

Six dispatches, **2,908 observations =2,904 pass +2 fail +2 skip**, **not** 2,908 distinct cases. The broad inventory has 882 records; positive final acceptance has 874. Original failure sources, source hashes, JSON/NUnit/logs and raw artifact hashes are in `Evidence/`. Counts are derived from native records, not dispatch/task completion or screenshot inference.

Exact normalized artifacts (under `Evidence/Runs/`):

- `pi-unity-tests-editmode-20261021T195050731Z-910efec6888b42c8b8412e1831d95ea9.json`
- `pi-unity-tests-editmode-20261021T195641136Z-f784ed4c622445c7a56981ac1406b4bd.json`
- `pi-unity-tests-editmode-20261021T200133842Z-acf2d7ec825948c2893d6c15ff980845.json`
- `pi-unity-tests-editmode-20261021T200445315Z-6fd9555656bb41f58c0465abf64d2678.json` (**uncertain**)
- `pi-unity-tests-editmode-20261021T200611759Z-c5e6cc5f7c5e4e6f8e74ef5e227fd244.json` (**224 positive**)
- `pi-unity-tests-editmode-20261021T201141791Z-6e18b9d6c5b2436c8a7b3bdf5b223f9f.json` (**874 positive**)

## Scenario coverage

The 25 cases cover null/empty optional metadata and legacy declarative entry; exact model/root identity and one cached controller; immediate initial state and class addition/removal; default same-ID nonduplication; Clear/inactive silence/cache replay; explicit re-entry and obsolete captured callback refusal; Latest invalidation during gated native entry; detached-mounted/foreign/moved root refusal; active and inactive final release; terminal barrier waiting for callbacks without early release; throwing constructors, partial OnBind, initial replay and model entry failure containment/recovery; abandoned detached preparation; typed legacy error propagation; missing/wrong/abstract/open-generic metadata; unsupported global/multiple-view targets.

No manual activation calls simulate navigation. Native teardown proves cached models disposed **before** fixture DI-owner cleanup. The separate fixture bridge owner releases only uncommitted candidates that native cache ownership never acquired; those remain DI-owned as before, not a native disposal claim. Explicit test restoration of the harness-owned moved root is not framework repair or arbitrary destructive root replacement support.

## Reproduce in a new project copy

Use a clean committed F05 checkout and repositories containing the specified commits:

```text
python Validation~/F05/prepare_consumer.py --display <Display-repository> --utility <Utility-repository> --injectio <Injectio-repository> --consumer <NEW-consumer-path>
```

The script refuses an existing destination or dirty Faciem review source, archives approved Display main `9f166245d87da18dbca1c8de281831b6dad30272` (tree identical to reviewed `799f2a0c`), preserves Utility `bb56919e` / Injectio `e499f61a`, overlays the committed Faciem package, imports the unchanged sample, adds its validation-only test to the native friend assembly, and copies existing scene/oracle fixtures. No Unity launch, source checkout/index mutation, new DI provider, or Display pin/source modification. It records preparation/source hashes in the consumer. The committed script was also executed successfully into a separate new preparation-only copy: all 112 core C# source inputs matched, scene/oracle/sample fixtures were present, and Unity was not launched (`Evidence/repro-preparation-check.json`).

Check the **exact new copy's** status first. The historical consumer was closed with no lock or matching process before choosing isolation. With pi-unity, required native invocation is:

```json
{
  "path": "<NEW-consumer-path>",
  "testPlatform": "EditMode",
  "testFilters": ["Sim.Faciem.Tests"],
  "execution": "isolated",
  "useGraphics": true,
  "reportFormats": ["json", "nunit"],
  "timeoutSeconds": 1200
}
```

For the final compatibility contract use the unchanged 44 filters in `Evidence/acceptance-selection.json`. Do not replace an uncertain result with older/latest artifacts or reinterpret the wider skipped aggregate as a pass. Do not close/repoint an already-open Editor, delete lockfiles, or switch backends after uncertainty.

C# `dotnet format whitespace --folder --include <changed/source C# files> --verify-no-changes` passed using Unity's bundled SDK. Existing 113 C# files were compared to the reviewed native baseline: **4 intentionally changed**, **109 unchanged**, **all old test sources unchanged**; all **159 existing metadata files** retained their tracked content/GUIDs (canonical-LF comparison for Windows checkout line endings). Baseline worktree/index and Display review worktree remain unchanged. `Evidence/protection.json` records exact scope; native source hashes are canonical-LF plus raw disk hashes, artifact hashes are raw bytes.

PlayMode intentionally not run. No player/device/IL2CPP stripping, pixel/audio, arbitrary destructive root recovery, remaining Trivia/Last Chance phase migration, Server/Angular/protocol, provider/model lifetime redesign, merge or deployment acceptance is claimed.
