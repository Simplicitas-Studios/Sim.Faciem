# U1b — clean-consumer preparation blocked by host tooling

> **Historical attempt at 3913754.** The explicitly authorized [.NET 8 continuation](u1b-net8-restore-validation.md) found Unity's existing compatible runtime, restored the R3 root package and attempted baseline compilation. The host-launch blocker is resolved; a missing NuGet transitive closure now blocks compilation. The statements below describe the earlier attempt, not the fresh result.

## Historical verdict: blocked; no compilation or compatibility acceptance

Faciem PR #1 reviewed at `7c7c9fc8a1a41477808b8569ee29e813e339963d`;
runtime/source baseline remains `52853a83b5db11cd85c63abe65f49e96db893599`.
Utility PR #1 reference is `d57864989f8b85332ebb8093da47602e68b39132`.

The proposed standalone Utility core removes the original package-direction
obstacle. Its optional editor integration is separately installed and may depend
on both core and Faciem. This continuation does **not** claim that Faciem's
missing forward package declaration, stale DLL metadata or runtime/editor code
has already been fixed.

**A new external host-tooling prerequisite stopped this task.** Per the explicit
“If another external blocker appears, stop and document it” boundary, no alternate
restore route, runtime installation, forced roll-forward, package upgrade or
production patch was attempted afterward. No Unity process was launched for U1b.

## Dependency findings, not a restored dependency closure

Authoritative metadata at the previously used reference pins was inspected:

| Part | Exact pin/version | Role |
|---|---|---|
| R3 NuGet | `R3 1.3.1` | Actual R3.dll/types; UPM wrapper alone does not supply it |
| R3.Unity | `a8d5fb3c2d0afd124223084e94c9e0ee73497510`, manifest `com.cysharp.r3 1.3.1` | Unity integration; GUID `77221876cc6b8244180b96e320b1bcd4` resolves to R3.Unity |
| UniTask | `a77a1b4a89824fb6df293099de3060daf2a432c1`, manifest `com.cysharp.unitask 2.5.11` | Async API; GUID `f51ebe6a0ceec4240a699833d6309b23` resolves to UniTask |
| NuGetForUnity | `acc1c7bc9ea34c33b830e40316fca52553878d29`, manifest `4.5.0` | Editor package-management tooling, not a Faciem runtime dependency |
| NuGetForUnity.Cli | NuGet tool `4.5.0` | Official pre-Unity restore path; host runtime blocker below |
| Utility | `d57864989f8b85332ebb8093da47602e68b39132` | Standalone core proposed by Utility PR #1 |

The pinned R3 README explicitly requires **both** NuGet R3 and the R3.Unity
package. R3 1.3.1's official nupkg contains netstandard2.0/netstandard2.1/net6.0/
net8.0 DLLs. Its nuspec's .NETStandard2.1 group declares TimeProvider 8.0.0,
Annotations 5.0.0, Unsafe 6.0.0 and Channels 8.0.0. These are **declared dependency
bounds**, not newly verified/resolved consumer pins. The package's source
repository commit is `f6eed2dd4208dc4ae171c601e799e85f82aca25e`.
No nupkg DLL was hand-injected or copied from Midnight Wager.

Faciem Runtime and Shared explicitly reference R3.dll. Both also contain stale
Bebop.Monads.dll entries; source scans found no Bebop C# usage. No fabricated
Bebop DLL is appropriate. Source/meta mapping also confirmed Addressables and
ResourceManager references against the earlier resolved package metadata.
Three GUIDs remain unresolved in this bounded source map:
`343deaaf83e0cee4ca978e7df0b80d21`,
`2bafac87e7f4b9b418d9448d219b01ab`,
`75469ad4d38634e559750d17036d5f7c`.
A bounded installed-metadata scan timed out; this does not prove absence or
justify deleting them or inventing their assembly names.

## Prepared isolated consumer — not imported or compiled

`Validation~/CleanConsumer` is excluded from the Faciem package import by the
tilde folder. Its project manifest explicitly includes:

- Faciem local working copy for future narrow edits; exact future source hashes
  must be captured when validation can actually run.
- Both Utility packages pinned to **d578649**, with optional integration using
  its approved subdirectory URL.
- The verified R3.Unity, UniTask and NuGetForUnity Git revisions above.
- Built-in **UIElements 1.0.0**, UTF **1.8.0**, and an explicit project request
  for **Addressables 2.8.0**.
- `Assets/packages.config` requests NuGet **R3 1.3.1**.

The declared Editor remains **6000.6.5f1 (3ff58d469c8a)**. No Unity upgrade.
The preparation-only local settings template came from the agent-created
Utility core validation consumer, not Midnight Wager, and is not committed.
No validation setting or assembly-version check was disabled.

**There is no current U1b UPM lockfile or actual resolved Addressables version**:
Unity/UPM was never started. The 2.8.0 request is not evidence it was tested.
Utility's previous integration attempt resolved 2.11.2; that historical observation
must not be carried forward as a new U1b result. Actual lockfile/version reporting
remains a gate for continuation.

## Exact external failure

Official NuGetForUnity.Cli **4.5.0** was installed into an isolated local tool
directory, not globally. The attempted documented restore command was:

```text
nugetforunity restore <Faciem-checkout>/Validation~/CleanConsumer
```

The tool's default-selected **net9.0 entrypoint** requires Microsoft.NETCore.App **9.0.0**. This host has SDK
**10.0.202** and Microsoft.NETCore.App **10.0.6**, but no .NET 9 runtime.
The process exited **2147516566** before managed restore code ran:

```text
You must install or update .NET to run this application.
Framework: 'Microsoft.NETCore.App', version '9.0.0' (x64)
The following frameworks were found:
  10.0.6 at [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
```

The published CLI also contains net7.0/net8.0 targets; neither was attempted.
The system runtime inventory does not include those major versions either.
The pinned Unity Editor tooling package and published NuGet CLI are distinct
artifacts, not assumed to be identical from their shared version number.

This is a **host tool launch failure**, not a NuGet network/closure failure,
Faciem compile failure, failed test, or Unity defect. It does not establish
compatibility of any queued source fix. Exact metadata/runtime/error evidence:
[machine report](u1b-clean-consumer-evidence.json) and
[evidence directory](../Documentation~/evidence/u1b/).

## Deferred fixes and next gate

None of the queued production changes was applied after the external stop:

- Declare the verified Utility/R3.Unity/UniTask/module dependencies, preserving
  package versions and the approved acyclic direction.
- Remove confirmed stale Bebop DLL entries without guessing unresolved GUID names.
- Guard confirmed unguarded runtime Editor imports and qualify runtime converter
  initialization attributes.
- Use the existing serialized-name type resolver instead of navigation's
  Editor-only MonoScript.Script.GetClass while preserving DI/navigation policy.
- Guard absent Addressables settings/default groups, including helper/label
  paths, without auto-creating project settings/assets or unrelated refactors.

**First obtain explicit authorization for an appropriate host-runtime/tooling
route.** Do not silently install .NET, force major roll-forward, change CLI
versions, switch restore backends or suppress validation. After that, complete
and record NuGet closure; perform narrow reviewed fixes and focused tests in the
UIElements-enabled isolated consumer; report actual UPM resolution; only attempt
a player build after clean compilation prerequisites pass.

No production/package/asmdef/test edit, R3 restore, Editor compilation, test run,
player build, root-independent API, Midnight Wager change, merge or deployment.
Stop for independent review.
