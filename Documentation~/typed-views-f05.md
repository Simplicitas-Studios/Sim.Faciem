# F05 — optional typed View behaviors

Based on reviewed Faciem main `2959e7be2304808fdb244b35c655778004322973`. Draft for independent review, not a Display migration or deployment.

## API and association

`View<TViewModel>` is an ordinary behavior/controller object, **not** a `VisualElement`. It wraps exactly the UXML root and corresponding model that native navigation already prepares/caches. UXML/USS still own structure and appearance; the model exposes presentation state, while this optional object owns node queries and imperative UI behavior.

Add a public `(TViewModel, VisualElement)` constructor and override synchronous `OnBind()`:

```csharp
public sealed class SelectionView : View<SelectionViewModel>
{
    private readonly Label _item;

    public SelectionView(SelectionViewModel model, VisualElement root) : base(model, root)
    {
        _item = root.Q<Label>("item");
    }

    protected override void OnBind()
    {
        Bind(ViewModel.IsSelected, selected => _item.EnableInClassList("item--selected", selected));
    }
}
```

Here `IsSelected` is a replaying R3 state source, e.g. `ReactiveProperty<bool>`. `Bind` subscribes immediately, guards publication, and transfers the returned subscription to the **existing** `NavigationActivation.Own`. It does not poll, add a binding engine, or create a second activation/disposable bag. Non-replaying event streams naturally have no initial state; use a replaying source when initial visuals are required.

On the **existing `ViewIdAsset`**, retain `View`, `DataContext`, and `ViewModel`. Optionally select the behavior script in **`TypedView`**. Empty/null references preserve the declarative-only path. Code-authored registrations may set `TypedView.TypeName = typeof(SelectionView).AssemblyQualifiedName`. Existing model/asset registration, DI provider, and navigation APIs are unchanged. Do **not** register another model/provider, instantiate the behavior in application code, or insert a separate screen host/cache.

`TViewModel : BaseViewModel`. The actual resolved model must match it. The type must be concrete/closed and derive from `View<TViewModel>`. Its public constructor must take that generic model type and `VisualElement`, and forward those exact instances to `base`. Wrong types, missing constructors, or substituted model/root references fail native preparation rather than creating a replacement model/tree.

### Constructor adaptation

The existing `IDIContainerBridge.ResolveInstance(Type)` cannot provide runtime constructor arguments for an already resolved model and cloned root. Resolving a typed behavior through it could instantiate/resolve a second model. The smallest compatible adaptation is native two-argument constructor invocation **after** normal model resolution and detached tree preparation, **before** old UI exit/commit. No DI interface, Injectio behavior, service provider, or model construction lifetime changes. Arbitrary extra constructor service injection is not added.

Constructors only query/store structure: no subscriptions, tasks, event registration, or other effects. An exception prevents an object being returned, so no framework can recover resources secretly allocated by a throwing constructor. Model preparation ownership is unchanged: models not transferred into a native cache remain DI/bridge-owned until their real provider lifetime ends.

## Native lifecycle and ownership

| Native event | Typed behavior |
| --- | --- |
| Detached preparation | Construct once using existing model/root; no activation subscriptions. |
| First activation | Bind once after native activation/physical commit and before model `NavigateTo`; replay applies immediately. State changes in `NavigateTo` are then observed normally. |
| Ordinary same-ID navigation | Reuses activation and binding; still executes existing model entry semantics. |
| Clear / ordinary exit | Native invalidation fences callbacks, and native activation drain disposes subscriptions. Cached behavior/tree/model remain. |
| Return / explicit re-entry | Same cached behavior; new native activation, one new subscription set, immediate latest-state replay. |
| Obsolete preparation | Uncommitted controller base references released; no subscription was started and no model ownership is invented. |
| Explicit final teardown | Existing ownership validation and terminal FIFO drain first; native activation cleanup releases subscriptions, cache/controller associations and base references are cleared, existing native model finalization remains once-only. |

The existing Region cache entry carries the optional association; there is no second cache or navigation authority. Do not change asset type associations while a cache is alive; tear down that owned scope before changing registrations. The framework releases its own references; external/derived fields and deliberately retained callbacks are ordinary consumer references, not a claim of forced GC.

Supported typed subset: one independently owned **native, non-global single-view Region**, with no nested Regions inside the typed template. This deliberately matches the existing complete flat ownership/finalization contract. A selected independently owned nested *leaf* remains possible; owning/migrating a parent/nested hierarchy, shared multi-target/global behavior lifetime, custom `IRegion`, or multiple-view ownership is not invented here. Unsupported typed targets reject before old UI mutation. Existing declarative-only consumers retain their current supported behavior and APIs.

### Custom events / delayed work

Register custom event resources during synchronous `OnBind`, using a captured guarded callback and native-owned unsubscriber:

```csharp
protected override void OnBind()
{
    var state = ViewModel;
    Action<int> publish = Guard<int>(ApplySelection);
    state.Changed += publish;
    OwnBinding(Disposable.Create(() => state.Changed -= publish));
}
```

`Guard` captures the current activation, never whatever activation is current later. `Bind` uses this guard automatically. The guard checks native activation validity, live cache identity/data source, active ViewId, physical Region containment, and the captured panel when originally mounted. Run UI callbacks on the owning Unity thread. Invalidated, moved, detached-mounted, foreign-data-source, or finally released roots cannot receive these managed publications. A pending uncooperative callback still holds native operation ownership until it returns; cancellation is not forced execution termination.

For custom async work, capture `Activation`, its cancellation token, and a `Guard` callback **before** the await; publish through that captured callback **after** awaiting. Do not pass async-void callbacks to `Bind`, write directly to a saved root after an await, or obtain a fresh activation after an await. Arbitrary unguarded consumer code cannot be made safe by disposing a subscription. No extra View async lifecycle, detached task runner, navigation handle, or root-generation authority is added. Lifecycle navigation remains model-owned through protected scoped `Navigation`.

## Failures and compatibility

- Unresolvable/non-concrete typed metadata rejects during native validation.
- Constructor/root-query/argument failures occur in detached `ViewPreparation`: existing active UI is preserved; no cache transfer is claimed.
- `OnBind` and synchronous initial replay/application failures are native `Entry` failures, using existing containment and activation cleanup. R3 normally reports observer callback errors without throwing through `Subscribe`; `Bind` explicitly captures/rethrows initial replay errors **after** owning the subscription, preventing false success/leaks.
- Partial binding failure can leave an inert cached behavior/model, just like native entry failure; later valid navigation reuses it and binds a fresh activation.
- Errors from later notifications retain ordinary R3 error handling; they are not silently converted into another navigation operation. Custom subscription factories must be exception-safe if they throw before returning their disposable.
- Typed legacy `NavigateTo` uses the same preparation/containment path and rethrows the original error. Result APIs report native staged outcomes. No missing/no-op/task completion is relabeled as success.
- Existing terminal unsafe/moved/shared/incomplete ownership and opaque throwing-resource limitations remain. No automatic physical-root repair, Editor closure, cache ownership transfer, or destructive recovery.
- No existing public interface member or serialized field was renamed/removed. Optional metadata defaults empty; existing model/Region/cache policies and declarative binding ownership are unchanged.

## Working example

Import **Typed View USS selection** from Package Manager Samples, then follow [sample setup](../Samples~/TypedView/README.md). `SelectedItemViewModel.SelectedIndex` exposes state; `SelectedItemView` queries `item-0` through `item-3` and uses `EnableInClassList` to add/remove `item--selected`. The supplied USS owns styling. Its actual imported classes/UXML execute in the validation-only native integration test, not just a duplicated pseudo-example.

This is a mechanical selection example, **not** Midnight Wager reveal behavior. There are no Server/Angular/Display source changes, gameplay routing, accepted-state/protocol authority, private answer data, Utility/Injectio redesign, or effect migration.

## Evidence and limits

See [validation instructions and evidence](../Validation~/F05/README.md). Required native framework evidence is **224/224**: 198 existing cases unchanged, 25 new typed cases, and one actual imported sample case. Final focused compatibility is **874/874** =224 framework/sample +628 established production Display +22 unchanged actual World Space cases. Tests use real native navigation, cached UXML trees, mounted EditorWindow Regions, re-entry, Latest cancellation, Clear, and terminal ownership — no simulated View activation loop.

A broader 882-record campaign remains **uncertain**, not full-suite acceptance: 881 passing / zero failures / one preexisting ignored Display WebSocket test and Unity exit 0, but tool outcome `uncertain`. Earlier initial-replay failure and missing-consumer-oracle attempt are retained. Native EditorWindow evidence is not player/device, IL2CPP stripping, pixel, audio, or arbitrary destructive World Space root replacement acceptance. Reflection-created behavior types/constructors must be retained by the consumer's stripping configuration for player builds; no player build/stripping acceptance is claimed here. PlayMode is intentionally not run for this framework slice.
