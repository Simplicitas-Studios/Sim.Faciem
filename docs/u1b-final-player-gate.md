# U1b final player gate and hyperlink review

## Verdict

**NOT MERGE-READY. Native player compilation PASSED; the strict scene-preservation harness guard FAILED.** Stop for independent review. These are different outcomes: do not describe the rejected eval/harness as successful or infer preservation from the build result.

Production code, assembly metadata, package inputs and the seven Faciem tests are unchanged from **cfc7ec284e32c13690186a92601259db06b6c75c**. Utility remains **0c308f7df1aebf6ad31b6e1141da37f3665e48a1**, unchanged. Only consumer build setup/fixtures and evidence/documentation changed. No Midnight Wager work, root-independent API, package upgrade, merge or deployment.

## Hyperlink decision

Reviewed `Runtime/Controls/HyperLinkLabel.cs` and the official **6000.6** public API documentation. The control's serialized/UXML `InstanceIds` remains `List<int>`. On the actual **6000.6.5f1** Editor, public static metadata confirms `EditorGUIUtility.PingObject(int)` and `AssetDatabase.GetAssetPath(int)` have `Obsolete(IsError=true)`, as do the previously checked legacy object lookup and int/EntityId conversions. Supported ping overloads accept `UnityEngine.Object` or `EntityId`; `EditorUtility.EntityIdToObject` also requires `EntityId`.

The [EntityId contract](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/EntityId.html) explicitly warns against assumptions about integer representation, bit layout, hash codes and integer/string serialization. Its supported ulong APIs do not establish a safe migration contract from the existing legacy int list. No private reflection, unsafe/representation-dependent conversion or obsolete-error call was used. Public method metadata inspection was read-only.

**User decision: “Defer ping restoration (Recommended)”.** Existing data and disabled Editor ping remain unchanged; this limitation is explicitly accepted for U1b. Restoring ping requires a separately approved supported Object/EntityId/reference migration. No production or serialized-schema change was made. Hover/link handling remains as at cfc7ec2; this is not a claim that ping works.

Public sources: [PingObject](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/EditorGUIUtility.PingObject.html), [EntityIdToObject](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/EditorUtility.EntityIdToObject.html), [GetAssetPath](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/AssetDatabase.GetAssetPath.html). Exact scope/decision: [API review record](../Documentation~/evidence/u1b-final-player/hyperlink-public-api-review.json).

## Setup and the single native build

Same already-open exact `Validation~/CleanConsumer`, Unity **6000.6.5f1**, Pipeline **0.8.0-exp.1**, CLI **1.0.0-beta.13**, PID46808/port7801. Explicit mutable local packages, not a new immutable Git-pin consumer campaign. Addressables remains **2.8.0**; unchanged manifest/lock/NuGet inputs are copied into evidence.

1. The historical cfc7ec2 attempt never reached BuildPipeline because additive Editor scene creation was blocked by an untitled scene.
2. This turn's isolated preview-scene setup probe was rejected: `EditorSceneManager.SetActive failed; you can not save a preview scene`. Its `finally` closed the owned preview and deleted its probe path. Typed inspection still showed the original clean active untitled scene/two roots. This was **not a player build**. Runtime-only `SceneManager.CreateScene` was not used as an Edit Mode workaround.
3. Final setup avoids scene creation/open/save altogether. A byte-identical prior Unity-authored Utility validation scene is kept as `SceneFixtures~/UtilitySmoke.unity`, with its original smoke component and script GUID. The helper copies it to **only its owned** `Assets/U1bBuildSmoke.unity`, synchronously imports it as a SceneAsset, builds from that explicit path and deletes the temporary asset in `finally`. It refuses to overwrite either an existing scene or its meta. The fixture/component are test setup, not new production models. The source scene was a local generated/ignored asset, **not tracked at the Utility commit**; its exact input hash is preserved, and this copied fixture is now checked in. The component and its meta are byte-identical to their tracked Utility inputs.
4. Exactly **one** connected BuildPipeline invocation, host/handler deadline **600 seconds**. No retry, fallback, second Editor, closure or executable launch.

### Native BuildReport

| Field | Actual result |
|---|---|
| Result | **Succeeded** |
| Target/backend | **StandaloneWindows64 / Mono2x** |
| Options | Development + DetailedBuildReport |
| Build GUID | `b43eae783d7544118812c358187add25` |
| Start/end UTC | `2026-10-18T10:02:38.9137637Z` / `2026-10-18T10:03:05.7946229Z` |
| Duration | **26.8808592 seconds** |
| Errors/warnings | **0 / 3** |
| Reported total size | **173700119 bytes** |
| Output | `Validation~/CleanConsumer/Builds/FaciemConsumerSmoke.exe` (not launched) |

Exact native summary and all steps/messages: [native-buildreport.json](../Documentation~/evidence/u1b-final-player/native-buildreport.json). Genuine binary [LastBuild.buildreport](../Documentation~/evidence/u1b-final-player/LastBuild.buildreport): **134032 bytes**, SHA256 `5ea0c12f34a8421d17ca1f36a6d91b4b44a3bdac8e4f1292679f24fa6200dffa`, byte-identical to the current native `Library/LastBuild.buildreport`. Executable: **667136 bytes**, SHA256 `b70442f90f50cd91f7af7bf2272602645fc5d02c4d3156fb221fc27c328d2436`. Full produced-file hashes retained; binaries other than the native report remain local.

All three native warnings, without suppression or out-of-scope fixes:

- Pipeline: no RuntimePipelineConfig asset; Pipeline disabled in Player builds. No runtime configuration was created.
- Utility `Runtime/SerializableGuid.cs(21,21)`, **CS0414**: `_cashedHashCode` assigned but unused (existing warning).
- Faciem `Runtime/Controls/ListBinding/SerializedListReference.cs(9,22)`, **UAC1009**: `ItemSource` uses unsupported serialization type `System.Collections.IList`. Native compilation succeeded; this warning is not proof that list serialization/binding works.

The four expected runtime/shared assemblies have no `UNITY_EDITOR` define or UnityEditor references in the captured player compilation graph. Optional **Sim.Utility.Faciem.Editor.dll** and **Sim.Utility.Editor.dll** are absent from produced Managed output. Actual built output includes R3, TimeProvider, Channels and System.Runtime.CompilerServices.Unsafe DLLs; this is build-output evidence, not a new NuGet Unsafe pin or player execution claim. Existing **Sim.Faciem.Controls.Editor.dll** is also present: its asmdef has `includePlatforms: []`. No blanket assertion that every Editor-suffixed assembly is excluded; no production asmdef change was authorized or made here.

### Failed preservation guard — not hidden

After the successful native BuildReport was written, the helper's `finally` recorded **CHANGED** and threw:

> Original scene state changed during smoke build; inspect without retrying.

The aggregate guard checks original scene handles/loading, dirty flags, root **object identity/order**, scene count and active scene identity. It does not record which individual clause failed. The authoritative native Editor log confirms **`Loaded scene 'Temp/__Backupscenes/0.backup'` after build success**: Unity itself backed up/restored the open scene during BuildPipeline. This explains why keeping pre-build object identities is not a safe acceptance assumption, but does **not** establish that every requested scene-preservation condition passed.

Before/after typed inspections both show **one loaded, clean, active untitled scene, two roots**; the owned temporary scene/meta are gone. The helper does not call SaveScene/OpenScene/NewScene on the user scene, and no repair/save/replacement was attempted after failure. These matching observations are not full object/content preservation proof. The final eval is **rejected / COMMAND_FAILED**, despite the separate native BuildReport being Succeeded. No failed guard was weakened or retrospectively relabeled green. This remaining scene-preservation/harness issue needs independent review before merge readiness can be asserted; no second build was run to fix or conceal it.

[Exact outcome](../Documentation~/evidence/u1b-final-player/dispatch-outcome.json), [scene observations](../Documentation~/evidence/u1b-final-player/scene-observations.json), [selected native log lines](../Documentation~/evidence/u1b-final-player/native-editor-log-excerpt.txt). The full exact build log segment remains local with hash/size in the machine report. An initial global Editor.log offset was not the running Editor's log; authoritative project log path was obtained from `Application.consoleLogPath`, and only the correct project log was used for final evidence.

## Editor and optional Inspector evidence

No production code changed: retain **24/24 distinct Editor cases** at **cfc7ec284e32c13690186a92601259db06b6c75c**, using Utility **0c308f7df1aebf6ad31b6e1141da37f3665e48a1**. Exact three final artifact names/hashes/counts are in [the current machine report](u1b-final-player-gate.json), referencing the preserved [connected evidence](../Documentation~/evidence/u1b-connected/). They were **not all rerun this turn**.

Consumer setup compilation: **11.8 seconds / zero errors** after switching to the saved fixture (an earlier preview setup compiled cleanly in11.7s). Fresh serial connected optional Utility Inspector fixture: **5/5 passed**, zero failed/inconclusive, **11.771 seconds**, unchanged original assertions and explicit R3.dll reference. [Normalized run](../Documentation~/evidence/u1b-final-player/optional-inspector-tests.json), original local artifact `pi-unity-tests-editmode-20261018T100209958Z-f9d5c59770e04a68bc3b1f81041e7e5e.json`. These are five repeats of the existing24, **not29 distinct tests**. All **8/8 Utility static contracts** passed again. Smoke helper whitespace formatter verified; no production/GUID edits.

The optional tests remain valid for template/control lookup, original template/style GUIDs, initial ViewModel/command identity, valid/invalid GUID input, assembly identity and original namespace. They **do not** establish full mounted/bound Inspector acceptance. Remaining manual gaps: real edit/apply propagation; regeneration click and serialized GUID propagation; readonly behavior; actual stylesheet/visual presentation; mount/unmount/disposal lifecycle; IMGUI coexistence. Native player compilation also does not establish any of those Editor interactions. No player launch, runtime navigation/content/device/IL2CPP acceptance is claimed.

## Review boundary

Hyperlink deferral is explicitly accepted; native player compilation is now proved. The requested strong scene-preservation acceptance is **not established** by this run. Keep PR #1 draft/open, no auto-merge. Review the failed guard/native backup behavior and the separately documented manual Inspector gaps. Do not retry, save/replace the scene, broaden production scope, merge or deploy as part of this task.
