# Typed View USS selection

Import this sample through Package Manager → Sim.Faciem → Samples → **Typed View USS selection**.

1. Create a `ViewIdAsset` using your existing Faciem asset-registration workflow.
2. Set an application ViewId, `View = SelectedItem.uxml`, and both `DataContext` / `ViewModel` to `SelectedItemViewModel`.
3. Set optional **TypedView** to `SelectedItemView`. Use the existing Faciem Addressables ViewId discovery/registration (or your existing explicit registry). No behavior DI registration or second provider is needed.
4. Navigate the asset into an independently owned native non-global single-view Region with existing Faciem navigation.
5. Your presentation-state producer updates the already constructed model's `SelectedIndex.Value` (0–3, or -1 for none). Initial 0 is applied immediately. The typed View queries the existing tree and adds/removes `item--selected`; USS supplies appearance.
6. Clear/away unsubscribes but retains the cached tree/model/behavior. A native return or explicit re-entry replays the latest value. Final release uses existing native `TeardownResult` and complete ownership rules.

Do not `new` another ViewModel/VisualElement/controller in screen-host code. Constructor queries are inert; subscriptions use `Bind` during native `OnBind`. This is a mechanical selected-item example, not game/reveal logic.

The validation-only imported-sample test executes these actual files through native navigation, verifies initial/reactive classes, Clear/inactive silence, cached replay and one model resolution, then native final release. See [API/ownership notes](../../Documentation~/typed-views-f05.md).
