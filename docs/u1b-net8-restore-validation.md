# U1b — .NET 8 restore route works; baseline compilation blocked

**Verdict: BLOCKED / NOT MERGE-READY.** Reviewed Faciem PR #1/source at
`39137546019fc7c852d51fc272516855926a85ad`; Utility remains pinned to
`d57864989f8b85332ebb8093da47602e68b39132`. No Faciem production fixes applied.

> **Historical attempt.** The [connected continuation](u1b-connected-validation.md) incorporates the user's NuGet fixes and verifies clean Editor compilation plus 24 distinct passing cases. The newer player preparation is separately blocked by the existing untitled scene. Do not treat the older TimeProvider failure below as the current Editor result.

## Historical result

| Gate | Observed outcome |
|---|---|
| Find compatible installed runtime | Found Unity 6000.6.5f1's bundled **Microsoft.NETCore.App 8.0.21**, SDK inventory 8.0.318 |
| Install isolated .NET 8 only if absent | **Not needed/not performed**; system dotnet inventory unchanged |
| Explicit CLI 4.5.0 net8.0 invocation | **Succeeded**, exit 0; no default net9.0 shim, major roll-forward or tool upgrade |
| Restore R3 root package | **R3 1.3.1**, genuine **netstandard2.1/R3.dll** bytes match official nupkg |
| Full R3 NuGet closure | **Not established**: only R3 is listed in packages.config and installed as a NuGet package |
| UPM/required module inputs | 22 packages registered, acyclic graph; **UIElements 1.0.0 explicitly requested and registered** |
| Actual Addressables version | **2.8.0**, not the historical consumer's 2.11.2 |
| Baseline Editor compilation | **Failed**: 27 distinct CS0246 diagnostic lines in R3.Unity; 25 TimeProvider, 2 ITimer |
| Narrow Faciem compatibility fixes | **Not applied**: requested clean-compilation prerequisite failed |
| Focused tests | **Not run**, no test result/pass/fail count claimed |
| Player build | **Not attempted**: clean-compilation prerequisite failed |

This supersedes only the earlier host-launch blocker, not the remaining
dependency/player/Inspector acceptance gates. It is not a completed U1b fix.

## Exact route and inputs

The host was explicitly selected from:

`C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/DotNetSdk/dotnet.exe`

It executed the isolated installed NuGetForUnity.Cli **4.5.0** DLL under
`tools/net8.0/any/NuGetForUnity.Cli.dll`, with arguments
`restore <Faciem>/Validation~/CleanConsumer`. Child-only environment selected
that DOTNET_ROOT, disabled multilevel lookup and used **LatestPatch**, not major
roll-forward. No global PATH/runtime change. No installation or alternate route.

The CLI root-package restore exited 0 in approximately 0.347 seconds, generated a
default public nuget.org V3 NuGet.config, and installed **only R3.1.3.1**. The
unchanged packages.config requests only the root package. Root restore success is
**not dependency resolution/closure success**. The default generated NuGet.config
is retained as a reproducible consumer input; no credential sources were added.

[Exact invocation, runtime inventories, output and hashes](../Documentation~/evidence/u1b-net8/u1b-net8-restore.json).
The published CLI's nuspec identifies source commit
`a7c6b49a0141a5bff9b1983e38137522ef61977d`; it is distinct from the pinned
NuGetForUnity Editor-package source revision.

The original project pins are unchanged: R3.Unity **a8d5fb3**, UniTask
**a77a1b4**, NuGetForUnity Editor **acc1c7b**, both Utility packages **d578649**.
The local Faciem code/asmdefs/package remained byte-identical to the reviewed
source at compile time. See the
[complete actual UPM lockfile](../Documentation~/evidence/u1b-net8/consumer-packages-lock.json):
Addressables **2.8.0**, profiling core **1.0.3**, Scriptable Build Pipeline **3.0.3**,
UTF **1.8.0**, NUnit **2.1.0**. Registration is not compatibility acceptance.

## Substantive blocker and exact compilation evidence

One isolated, headless, graphics-disabled compile used Unity **6000.6.5f1**
(`3ff58d469c8a`), Unity CLI **1.0.0-beta.13**, a 600-second bound and
`unity_launch_batchmode`; no test flags or GUI launch.

```text
Library\PackageCache\com.cysharp.r3@c98d1f01b3ca\Runtime\UnityTimeProvider.cs(19,38): error CS0246: The type or namespace name 'TimeProvider' could not be found (are you missing a using directive or an assembly reference?)
```

The 27 distinct diagnostics also report **ITimer**. Unity's native log reports
script compilation **8.484476 seconds** and exit **1**; the CLI/tool reports
exit **6**. The package-cache suffix is a UPM fingerprint; the lockfile still
confirms the exact Git revision **a8d5fb3c2d0afd124223084e94c9e0ee73497510**.

Installed R3's .NETStandard2.1 nuspec declares minimum dependencies on
**Microsoft.Bcl.TimeProvider 8.0.0**, **System.ComponentModel.Annotations 5.0.0**,
**System.Runtime.CompilerServices.Unsafe 6.0.0**, and
**System.Threading.Channels 8.0.0**. These are **declared bounds, not actual
resolved/installed versions**. No such NuGet package directories were restored.
The current failure confirms that TimeProvider/ITimer are not supplied to
R3.Unity compilation. It does not establish which other dependencies Unity may
already supply, or rule out subsequent Faciem/Bebop/editor/player blockers.
The .NET 8 tool host is separate from Unity's scripting runtime; its own BCL DLLs
were **not** copied into the consumer as a workaround.

Exact native log retained locally:
`Validation~/CleanConsumer/Logs/u1b-net8-editor-compile.log`.
Its hash/size, full distinct compiler diagnostics, exact-run artifact inspection,
restore records, manifests and lockfile are preserved in the
[machine report](u1b-net8-restore-validation.json) and
[evidence directory](../Documentation~/evidence/u1b-net8).
The full local native log is not committed; the complete compiler-diagnostic
excerpt is exact and the original's SHA-256 is recorded. Log-only artifact
inspection succeeded as an inspection; it did **not** establish a test pass.
No NUnit XML was requested or expected for this compile-only attempt.

## Stop boundary and preservation

Stopped on the failed baseline compile, with **no restore/compile retry**, package
pin guesses/upgrades, DLL shims, core type duplication, validation suppression,
framework patches, root-independent API, source fixes, tests or build. The next
review must establish a complete pinned R3 closure appropriate to Unity's
netstandard2.1 target before another validation attempt.

Post-attempt exact-copy status: **0 matching Unity processes**, native
`Temp/UnityLockfile` **present**. Left untouched; no Editor closure or relaunch.
Five new untracked documentation .meta files generated by this validation were
identified and removed only to restore the previously absent documentation
metadata state; their hashes are recorded. Existing assets/GUIDs were not changed.

No Utility or Midnight Wager modifications/Unity operations, unrelated refactor,
merge, auto-merge or deployment. Return for independent review.
