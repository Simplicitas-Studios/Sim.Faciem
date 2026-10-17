# U1 — dependency/player compatibility audit: blocked

**Not compatibility fixes or a player-ready release.** The requested dependency-closure stop condition was reached before package/source edits. This draft records the blockers and corrects two previously unresolved dependency assumptions; it does not implement the root API or change MVVM/navigation behavior.

> **Historical baseline.** Utility's standalone-core proposal is now available at `d57864989f8b85332ebb8093da47602e68b39132`. The [U1b continuation](u1b-clean-consumer-blocker.md) verified dependency pins but stopped on an external NuGet CLI host-runtime prerequisite before any Faciem production change. The older Utility cycle/import evidence below describes its original pinned baseline, not the proposed split.

## Reference and scope

- Faciem source/main: **`52853a83b5db11cd85c63abe65f49e96db893599`**, package **1.0.0-alpha.1**.
- Utility source/main: **`29354fafd28a59896f6f7f541fbe137b5a3d6a6b`**, package **1.0.0-alpha.1**; inspected read-only, not modified.
- Requested merged Display reference: **`182bf785780a19377baf4b106c92b2ca8b2a9c4e`**. The merged [source review](https://github.com/KingdomHeards3/MafiosisPartyBangers-Display/blob/182bf785780a19377baf4b106c92b2ca8b2a9c4e/docs/display/d03-faciem-source-review.md) and [PR plan](https://github.com/KingdomHeards3/MafiosisPartyBangers-Display/blob/182bf785780a19377baf4b106c92b2ca8b2a9c4e/docs/display/d03-faciem-migration-plan.md) were verified by Git blob identity against the reviewed PR #12 documents. Full Utility implementation inspection below adds evidence not available in that assessment's metadata-only Utility review.
- No packages installed/imported, no Unity connection/launch/compile/test or player build, no Midnight Wager/Server/Angular changes, no root-readiness experiment. Only this repository's README and audit evidence/documentation change.

[Exact evidence, source pins and validation limits](u1-dependency-player-evidence.json).

## 1. Dependency direction is not a removable typo

The **current declared package graph is acyclic**:

```text
Sim.Utility package -> Sim.Faciem package -> Addressables 2.8.0
```

But the **actual source/assembly layering** is:

```text
Sim.Utility.Runtime -> no Faciem reference
Sim.Faciem.Runtime -> Sim.Utility.Runtime
Sim.Utility.Editor -> Sim.Faciem.Runtime + Sim.Faciem.Shared
```

This is not an assembly-definition cycle: Runtime and Editor are distinct assemblies. It is a cross-layer package dependency-direction conflict. Faciem's runtime-required Utility is absent from Faciem's dependencies, while Utility's package declares Faciem. Adding `com.sim.utility` to Faciem's package manifest would create a circular **declared package contract** without resolving this ownership decision. No UPM cycle rejection was executed/claimed.

Evidence at the pinned Utility revision:

- [package.json](https://github.com/Simplicitas-Studios/Sim.Utility/blob/29354fafd28a59896f6f7f541fbe137b5a3d6a6b/package.json) declares `com.sim.faciem: 1.0.0-alpha.1`.
- [Editor asmdef](https://github.com/Simplicitas-Studios/Sim.Utility/blob/29354fafd28a59896f6f7f541fbe137b5a3d6a6b/Editor/Sim.Utility.Editor.asmdef) references Faciem/Shared. [SerializableGuidEditorViewModel](https://github.com/Simplicitas-Studios/Sim.Utility/blob/29354fafd28a59896f6f7f541fbe137b5a3d6a6b/Editor/SerializableGuidEditorViewModel.cs) derives from Faciem Bindable and exposes a Faciem Command. [SerializableGuidEditorOfT](https://github.com/Simplicitas-Studios/Sim.Utility/blob/29354fafd28a59896f6f7f541fbe137b5a3d6a6b/Editor/SerializableGuidEditorOfT.cs) uses those controls/commands. **Removing the Utility dependency/asmdef references alone is unsafe.**
- Faciem's Runtime asmdef already references Utility Runtime GUID **73131d858481d53408ae35d0e5a78385**, and Faciem uses Utility Maybe/PropertyModel types.

Unity 6000.6 [Git dependency documentation](https://docs.unity3d.com/6000.6/Documentation/Manual/upm-git.html) explicitly allows Git dependencies in a **project manifest**, not between `package.json` manifests. Do not “fix” this by putting Git URLs in Faciem package dependencies or guessing registry publication/version availability. A consumer explicitly pinning both Git packages can supply the current source set, but that is not proof of a complete forward package contract or player compatibility.

## 2. New confirmed external player blocker

Utility's [Runtime/SerializableGuid.cs](https://github.com/Simplicitas-Studios/Sim.Utility/blob/29354fafd28a59896f6f7f541fbe137b5a3d6a6b/Runtime/SerializableGuid.cs#L3) has **unguarded `using UnityEditor;` at line 3**. Its Editor-specific members are guarded later, but the namespace import is not. Runtime [Sim.Utility.asmdef](https://github.com/Simplicitas-Studios/Sim.Utility/blob/29354fafd28a59896f6f7f541fbe137b5a3d6a6b/Runtime/Sim.Utility.asmdef) is not Editor-only.

This is a concrete static player-compilation prerequisite **outside the requested repository**. It remains even if Faciem's own Editor imports are fixed. No dummy UnityEditor assembly, copied Utility type, external-source edit, or package upgrade was used to mask it. No player compile was executed, so this is not a reported build run/failure.

**Stop decision:** a complete player-safe dependency closure cannot be delivered within this Faciem-only change without a separately approved Utility fix and dependency-direction decision. Per the instruction to stop rather than guess, no runtime/asmdef/package patch was attempted after this finding. The proposed individual fixes in section 5 remain unimplemented, not claimed as U1 delivery.

## 3. Two earlier unknowns now resolved

### Sim.Dispositio.Shared is supplied by Utility — do not add a package

This is a legacy **namespace**, not evidence that a separate Sim.Dispositio dependency is missing. Utility's runtime files define it:

- [MaybeExtensions.cs](https://github.com/Simplicitas-Studios/Sim.Utility/blob/29354fafd28a59896f6f7f541fbe137b5a3d6a6b/Runtime/MaybeExtensions.cs): ToMaybe/OrElse, used by Faciem Region's Maybe region-name fallback.
- FolderReference.cs, IdUsage.cs, IdUsageAttribute.cs, SerializableGuid.cs and StringExtensions.cs also use that namespace.

Do not remove the using as if it were necessarily stale, introduce another framework, or rename the namespace in U1. The required assembly is Utility Runtime.

### Bebop.Monads is stale assembly metadata, not a version to guess

Utility's pinned commit is **“Drop dependency to Bebop.Monads and use internal implementation.”** Its Runtime/Maybe.cs supplies Maybe<T>. A scan of Faciem/Utility C# source found no Bebop references. Faciem retains `Bebop.Monads.dll` entries in Runtime and Shared asmdefs; Utility does not declare that DLL.

This makes **removing the stale Faciem assembly entries a narrow future fix candidate**, not permission to install an arbitrary Bebop version. No removal was made after the closure stop. Validate the exact source/DLL closure in the resumed compatibility PR before marking it resolved in a player.

## 4. Exact dependency status

| Item | Observed version/source | Status |
| --- | --- | --- |
| Sim.Faciem | **1.0.0-alpha.1 / 52853a83b5db11cd85c63abe65f49e96db893599** | No changes to package version/dependencies |
| Sim.Utility | **1.0.0-alpha.1 / 29354fafd28a59896f6f7f541fbe137b5a3d6a6b** | Genuine runtime dependency with reverse package/Editor coupling; external runtime Editor import unresolved |
| Addressables | **2.8.0**, declared by Faciem | Not installed/restored/imported here; exact transitive graph and unresolved external asmdef GUIDs not guessed |
| R3.Unity | Not declared by Faciem package manifest; reference consumer locked Git **a8d5fb3c2d0afd124223084e94c9e0ee73497510** | Consumer prerequisite, **not an adopted/tested U1 compatibility pin** |
| R3 NuGet | Reference consumer **1.3.1**; Faciem requires R3.dll | No new version selection/restore; clean package closure still required |
| UniTask | Reference consumer locked Git **a77a1b4a89824fb6df293099de3060daf2a432c1** | Consumer prerequisite; not newly validated/upgraded |
| NuGetForUnity | Reference consumer locked Git **acc1c7bc9ea34c33b830e40316fca52553878d29** | Tooling prerequisite; not installed/changed |
| Bebop.Monads | No required source version found; stale Faciem DLL references | Do not add a speculative NuGet dependency |
| Sim.Dispositio.Shared | Namespace in pinned Utility Runtime | No separate package required |
| UI/Input assemblies | Existing Faciem runtime/shared/controls GUID references include UnityEngine.UI, **UnityEditor.UI**, R3.Unity, UniTask and InputSystem | Editor-only references in runtime-target asmdefs still need narrow fixes/actual player verification |
| External GUIDs | Runtime **9e24947de15b9834991c9d8411ea37cf**, **84651a3751eca9349aac36a66bba901b**; InternalEditor **69448af7b92c7f342b298e06a37122aa** | Carried as unresolved from the assessment/local reference map, not invented assembly names or newly installed packages |

Versions from the Display reference describe an existing consumer only. They do not make Faciem self-contained or demonstrate that an unchanged install works. No root consumer manifest/lockfile was generated for a guessed source set.

## 5. Confirmed Faciem-local fixes, queued not implemented

These are small follow-ups **after the external prerequisite decision**, not new behavior designs:

1. **NavigationService.cs:51:** current `viewAsset.DataContext.Script.GetClass()` uses a field excluded outside UNITY_EDITOR. Existing `MonoScriptReference.GetReferencedType()` resolves its serialized assembly-qualified TypeName without UnityEditor. Resume with the **DataContext type**, preserving existing DI interface-to-VM registration, not switching to ViewModel type or adding an assembly scan. Define a precise missing/unresolvable-type diagnostic and tests before navigation mutations; prove cache/DI/navigation semantics unchanged. Test serialization/stripping in a real player, not an Editor fallback that hides empty TypeName.
2. **Runtime/Shared Editor boundaries:** guard namespace imports in ViewIdAsset, converter registries and Shared/VisualElementsExtensions; remove only confirmed Editor-only references from runtime-target asmdefs. Converter registries' non-Editor initialization attributes also need UnityEngine qualification/import. Compile both branches; do not turn runtime code Editor-only to hide failures.
3. **Addressables initialization:** FaciemSetup.InitializeOnLoad assumes non-null settings; AddressableHelper creation/group paths also assume settings/default group. Guard missing settings without auto-creating settings/groups/assets. Asset postprocessing should not introduce new setup on import. Preserve normal existing-settings behavior under explicit tests; separate any broader opt-in behavior change from compatibility guards.
4. **Stale assembly metadata:** remove Bebop DLL references only after source/closure assertions; retain Utility-provided namespace usage. Do not remove necessary Utility or R3 references.

Tests to add in the resumed fix PR: player-safe type resolution success/missing/unknown type and existing DataContext DI path; existing-settings/missing-settings Addressables guard (including no new project files or groups); runtime/editor compile branches; clean target-player build. These tests were **not added/run here**, since no behavior changed and the prerequisite gate stopped implementation. A source-audit pass must not be relabeled as compatibility acceptance.

## 6. Scope requiring approval before continuation

- A separate **Sim.Utility** PR to fix its runtime Editor import and decide the dependency direction for its Faciem-dependent Editor inspectors. Possible relocation/split must preserve inspector functionality and existing namespaces/types; do not simply drop them or choose a broad package architecture in this blocked PR.
- Then choose reviewed registry/consumer Git pins and exact NuGet versions, resolve the complete asmdef graph in a clean isolated consumer, and resume only the narrow Faciem fixes above. Do not modify Midnight Wager to make U1 build.
- Faciem README claims **MIT** and links LICENSE, but that file is missing in the pinned tree and the package lacks a license field. Utility's pinned tree likewise has no license/copying file. Complete license notices/copyright and transitive terms need confirmation; do not infer absence of permission merely from API license=null or invent a new license.
- No new root-independent API, UIDocument lifecycle redesign, navigation concurrency/cache/command redesign, package upgrades, root rebind experiments, merge or deployment.

## 7. Validation and honest outcome

**Audit only, closure blocked.** Pins, merged reference blob IDs, package dependency direction, Utility's namespace provider and genuine Editor coupling, stale Bebop mentions, exact runtime Editor-import location and docs-only scope were checked. Dotnet SDK **10.0.202** is available; its existence is not a Unity/player build result. No C# code was compiled, no focused runtime tests passed, no package import was attempted, and no player/backend/stripping acceptance is established.

This PR therefore contains **no confirmed compatibility fixes**. It is the explicitly requested fallback: a blocked audit/evidence draft for independent review before widening scope, not a completed U1 or a silent change to dependencies. Original Runtime/Shared/Editor/InternalEditor source, assembly definitions and package.json are byte-for-byte Git-blob unchanged from the Faciem pin. Midnight Wager remains on its existing clean worktree and package state. Return for independent review; **no merge/deployment**.
