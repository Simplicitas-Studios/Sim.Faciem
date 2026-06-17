# Sim.Faciem

> **A full MVVM framework for Unity's UI Toolkit — runtime and editor**

`Sim.Faciem` is a Unity 6 framework that brings a structured **MVVM (Model-View-ViewModel)** pattern to UI Toolkit. It provides a complete stack — Region-based navigation, a ViewModel lifecycle, reactive commands, two-way data binding, and editor-window support — all while staying as close to Unity's own built-in systems as possible.

---


## ✨ Why Use It?

Unity's UI Toolkit has robust data binding, but it stops short of a full application framework. There is no built-in answer for:

- **How do views get created and shown?** (Region/Navigation)
- **How are ViewModels constructed and injected?** (DI bridge)
- **How does a ViewModel react to navigation events?** (Lifecycle)
- **How do buttons connect to async operations?** (Commands)
- **How does the same pattern work in editor windows?** (Editor integration)

`Sim.Faciem` answers all of these questions while keeping your code decoupled and testable:

- **Regions** replace manual `VisualElement.Add/Remove` calls with a declarative navigation model inspired by Prism and Angular's `router-outlet`.
- **ViewId assets** decouple view discovery from code — views are loaded from Addressables, ViewModels are resolved from your DI container.
- **`ViewModel<T>`** provides change notifications that feed directly into UI Toolkit's binding system — no boilerplate subscriptions.
- **Commands** expose `CanExecute` and `IsVisible` as observables, keeping buttons automatically in sync.
- **Full editor support**: the same Region/ViewModel/Navigation pattern works inside editor windows, popups, and toolbar overlays.

---

## 📋 Table of Contents

- [Package Info](#-package-info)
- [Installation](#-installation)
  - [Step 1 — Prerequisites: R3 & UniTask](#step-1--prerequisites-r3--unitask)
  - [Step 2 — Install NuGetForUnity](#step-2--install-nugetforunity)
  - [Step 3 — Install Sim.Faciem](#step-3--install-simfaciem)
- [Architecture Overview](#-architecture-overview)
- [Core Concepts](#-core-concepts)
  - [ViewId & ViewIdAsset](#viewid--viewidasset)
  - [Region System](#region-system)
  - [ViewModel](#viewmodel)
  - [Bindable\<T\>](#bindablet)
  - [Navigation System](#navigation-system)
  - [Commands](#commands)
  - [DI Bridge](#di-bridge)
  - [FaciemUIDocument — Runtime Entry Point](#faciemuidocument--runtime-entry-point)
  - [FaciemBootstrapper](#faciembootstrapper)
- [Built-in UI Toolkit Controls](#-built-in-ui-toolkit-controls)
  - [Region](#region-uxmlelement)
  - [BindableButton](#bindablebutton)
- [VisualElement Observable Extensions](#-visualelement-observable-extensions)
- [Design-Time Data Context](#-design-time-data-context)
- [Source Code Generation](#-source-code-generation)
- [Editor Integration](#-editor-integration)
  - [Editor DI Container](#editor-di-container)
  - [FaciemEditorWindow](#faciemEditorwindow)
  - [FaciemPopupWindowContent](#faciempopupwindowcontent)
  - [FaciemToolbarOverlay](#faciemtoolbaroverlay)
  - [EditorViewIdAsset](#editorviewidasset)
- [Usage Examples](#-usage-examples)
  - [Example 1 — Minimal Runtime Setup with VContainer](#example-1--minimal-runtime-setup-with-vcontainer)
  - [Example 2 — Creating a ViewModel](#example-2--creating-a-viewmodel)
  - [Example 3 — Navigating to a View](#example-3--navigating-to-a-view)
  - [Example 4 — Commands with CanExecute](#example-4--commands-with-canexecute)
  - [Example 5 — Nested Bindable Objects](#example-5--nested-bindable-objects)
  - [Example 6 — Creating an Editor Window](#example-6--creating-an-editor-window)
- [Requirements Summary](#-requirements-summary)
- [License](#-license)

---


## 📦 Package Info

| Field | Value |
|---|---|
| Package name | `com.sim.faciem` |
| Version | `1.0.0-alpha.1` |
| Unity | 6000.0+ |
| Required dependencies | R3, UniTask, NuGetForUnity, Addressables |

---

## 🔧 Installation

### Step 1 — Prerequisites: R3 & UniTask

Sim.Faciem depends on R3 for reactive primitives. R3 requires UniTask and NuGetForUnity.

Add the following to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.github-glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity",
    "com.cysharp.unitask":                 "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.cysharp.r3":                      "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity"
  }
}
```

### Step 2 — Install NuGetForUnity

After Unity reimports, open **NuGet → Manage NuGet Packages** and install the `R3` NuGet package (version ≥ 1.x).

### Step 3 — Install Sim.Faciem

Add Sim.Faciem to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.sim.faciem": "https://github.com/Simplicitas-Studios/Sim.Faciem.git",
    "com.sim.utility": "https://github.com/Simplicitas-Studios/Sim.Utility.git",
  }
}
```

Your final `manifest.json` dependencies section should look similar to:

```json
{
  "dependencies": {
    "com.github-glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity",
    "com.cysharp.unitask":                 "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.cysharp.r3":                      "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity",
    "com.unity.addressables":              "2.x.x",
    "com.sim.faciem":                      "https://github.com/Simplicitas-Studios/Sim.Faciem.git"
  }
}
```

> ⚠️ The **Addressables** package (`com.unity.addressables`) is also required. It is typically already included in Unity 6 projects via the Package Manager.

---

## 🏗️ Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│  UIDocument / FaciemEditorWindow                                 │
│                                                                  │
│   ┌─────────────────────────────────────────────────────────┐    │
│   │  Region  (MainRegion)                                   │    │
│   │                                                         │    │
│   │   ┌────────────────────────────────────────────────┐    │    │
│   │   │  VisualTreeAsset  ←→  ViewModel<T>             │    │    │
│   │   │  (UXML view)           dataSource              │    │    │
│   │   │                                                │    │    │
│   │   │    ┌──────────────────────────────────────┐    │    │    │
│   │   │    │  Region  (SubRegion)                 │    │    │    │
│   │   │    │   ...nested views...                 │    │    │    │
│   │   │    └──────────────────────────────────────┘    │    │    │
│   │   └────────────────────────────────────────────────┘    │    │
│   └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│   NavigationService  ←→  ViewIdRegistry  ←→  Addressables        │
│                  ↑                                               │
│        DI Container  (VContainer / custom)                       │
└──────────────────────────────────────────────────────────────────┘
```

- **Views** are UXML files. They are loaded on demand via Addressables.
- **ViewModels** are plain C# classes resolved from your DI container. They implement `INotifyBindablePropertyChanged` so UI Toolkit's binding system reacts to `SetProperty` changes.
- **Regions** are `VisualElement` placeholders placed in UXML. The Navigation Service fills them with the correct view when you call `Navigate(viewId, regionName)`.
- **RegionManagers** form a hierarchy that mirrors the view hierarchy. A child view's navigation calls bubble up through parent `RegionManager` instances until the correct region is found.

---

## 🧩 Core Concepts

### ViewId & ViewIdAsset

A **`ViewIdAsset`** is a ScriptableObject that registers a View. It holds:

| Field | Type | Purpose |
|---|---|---|
| `ViewId` | `ViewId` struct | Unique identifier for this view |
| `View` | `VisualTreeAsset` | The UXML document |
| `DataContext` | `MonoScriptReference` | Interface inheriting `IDataContext` |
| `ViewModel` | `MonoScriptReference` | Class inheriting `ViewModel<T>` |
| `SourceCodeGeneration` | `bool` | Optionally generate a static accessor property |

Create one via **Assets → Create → Faciem → ViewIdAsset**. All `ViewIdAsset` assets must be **tagged with the Addressables label `ViewId`** so the `ViewIdDiscoveryService` can load them automatically at startup.

The `ViewId` struct acts as a key. You reference it in code via the optionally auto-generated static class:

```csharp
// Auto-generated when SourceCodeGeneration is enabled on the ViewIdAsset
public static class WellKnownViewIds
{
    public static ViewId MainMenuView { get; } = ViewId.From("MainMenuView");
}
```

---

### Region System

A **`Region`** is a `[UxmlElement]` custom `VisualElement` that serves as a named placeholder into which the navigation system places views dynamically.

Place `<Region>` elements directly in your UXML files and assign a **`RegionNameDefinition`** asset:

```xml
<!-- MainShell.uxml -->
<ui:UXML>
  <Sim.Faciem.Region region-name="MainRegion" style="flex-grow: 1;" />
</ui:UXML>
```

**`RegionNameDefinition`** is a ScriptableObject created via **Assets → Create → Faciem → Region**. It holds the region's string name and can optionally generate a static accessor:

```csharp
// Auto-generated when SourceCodeGeneration is enabled
public static class WellKnownRegionNames
{
    public static RegionName MainRegion { get; } = RegionName.From("MainRegion");
    public static RegionName SidePanel  { get; } = RegionName.From("SidePanel");
}
```

A **`RegionManager`** tracks all `Region` elements that have been registered under it and exposes a parent link for hierarchical lookup. Each view's ViewModel owns a `RegionManager`, and when the navigation service fills a region with a view, it links the new child `RegionManager` to the parent's.

**Region supports multiple views** (`SupportMultipleViews = true` on the `Region` element), but the default mode is single-view: navigating to a new view deactivates the old one.

---

### ViewModel

#### `ViewModel<T>`

The main base class for all ViewModels. It implements Unity's `INotifyBindablePropertyChanged` so changes automatically notify UI Toolkit bindings.

```csharp
using Cysharp.Threading.Tasks;
using Sim.Faciem;

public interface IMainMenuDataContext : IDataContext { }

public class MainMenuViewModel : ViewModel<MainMenuViewModel>, IMainMenuDataContext
{
    private string _title = "Main Menu";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value); // notifies UI Toolkit
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    protected override async UniTask NavigateTo()
    {
        // Called when this view becomes active
        IsLoading = true;
        await LoadDataAsync();
        IsLoading = false;
    }

    protected override UniTask NavigateAway()
    {
        // Called when this view is deactivated
        return UniTask.CompletedTask;
    }

    private async UniTask LoadDataAsync() { /* ... */ }
}
```

Key members of `ViewModel<T>`:

| Member | Description |
|---|---|
| `SetProperty<T>(ref T field, T value)` | Sets the backing field and fires `propertyChanged` for UI Toolkit binding |
| `Property` (`IViewModelPropertyObserver`) | Observe property changes as `Observable<TProperty>` |
| `AddNestedBindable<TProperty>(expr)` | Propagate change notifications from a nested `Bindable<T>` object |
| `Navigation` (`IViewModelNavigationService`) | Navigate to other views or clear regions |
| `Command` (`ICommandBuilderFactory`) | Create sync and async `Command` instances |
| `RegionManager` | The region manager for regions nested within this view |
| `Disposables` (`DisposableBag`) | Lifetime-tied disposable container |
| `NavigateTo()` | Override to run logic when the view becomes active |
| `NavigateAway()` | Override to run logic when the view is deactivated |

#### `BaseViewModel`

`BaseViewModel` is the non-generic base. Use it if you don't need `SetProperty` / property change notifications (e.g., the built-in `ShellViewModel`). It still provides `Navigation`, `Command`, `RegionManager`, and lifecycle methods.

---

### `Bindable<T>`

`Bindable<T>` is a **non-MonoBehaviour** base for nested data objects that participate in property change notifications. It is useful when a ViewModel holds a complex sub-object whose properties should be individually observable and bindable.

```csharp
using Sim.Faciem;

public class PlayerStats : Bindable<PlayerStats>
{
    private int _level = 1;
    public int Level
    {
        get => _level;
        set => SetProperty(ref _level, value);
    }

    private string _name = "Hero";
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}
```

Register it inside a ViewModel to forward its change notifications:

```csharp
public class GameViewModel : ViewModel<GameViewModel>
{
    public PlayerStats Player { get; } = new();

    public GameViewModel()
    {
        AddNestedBindable(vm => vm.Player);
        // Now UXML can bind to "Player.Level" and get live updates
    }
}
```

`Bindable<T>` also exposes:
- `Observe<TProperty>(expr)` — returns an `Observable<TProperty>` for code-side reactive subscriptions.
- `ObserveAnyChange()` — emits `T` (itself) whenever any property changes.

---

### Navigation System

Navigation is performed through **`IViewModelNavigationService`** (available as `Navigation` on every ViewModel):

```csharp
// Navigate to a view inside a named region
await Navigation.Navigate(WellKnownViewIds.SettingsView, WellKnownRegionNames.MainRegion);

// Clear all active views in a region
await Navigation.Clear(WellKnownRegionNames.SidePanel);
```

**How it works:**
1. The service walks up the `RegionManager` hierarchy from the calling ViewModel to find the region with the given name.
2. If the view has never been shown, its UXML is instantiated and its ViewModel is created via the DI container.
3. If the view already exists (previously deactivated), it is reactivated and its `NavigateTo()` is called again.
4. Any previously active views in the region have their `NavigateAway()` called first and are hidden.

The **`ShellViewModel`** is the root ViewModel. It is always created first and performs the initial navigation to your root view:

```csharp
// Built-in ShellViewModel (can be used as-is or overridden)
public class ShellViewModel : ViewModel<ShellViewModel>
{
    protected override async UniTask NavigateTo()
    {
        await Navigation.Navigate(FaciemBridge.RootViewId, WellKnownRegionNames.MainRegion);
    }
}
```

---

### Commands

Commands bridge ViewModel actions to UI elements (primarily `BindableButton`). They are built through the fluent `Command` property available on every ViewModel.

```csharp
public class LoginViewModel : ViewModel<LoginViewModel>
{
    // Synchronous command
    public Command OpenSettingsCommand { get; }

    // Async command
    public Command LoginCommand { get; }

    private bool _canLogin;
    public bool CanLogin
    {
        get => _canLogin;
        set => SetProperty(ref _canLogin, value);
    }

    public LoginViewModel()
    {
        OpenSettingsCommand = Command
            .Execute(() => Debug.Log("Open Settings"))
            .Build();

        LoginCommand = Command
            .ExecuteAsync(async ct =>
            {
                await DoLoginAsync(ct);
            })
            .WithCanExecute(Property.Observe(vm => vm.CanLogin))
            .WithIsVisible(Observable.Return(true))
            .Build();
    }
}
```

**Command builder API:**

| Method | Description |
|---|---|
| `Command.Execute(Action)` | Create a sync command builder |
| `Command.Execute<T>(Action<T>)` | Create a typed sync command builder |
| `Command.ExecuteAsync(Func<CancellationToken, UniTask>)` | Create an async command builder |
| `.WithCanExecute(Observable<bool>)` | Sets when the command (and the button) is enabled |
| `.WithIsVisible(Observable<bool>)` | Sets when the button is visible |
| `.Build()` | Returns the `Command` instance |

Commands are implicitly convertible: `Command myCmd = Command.Execute(...).WithCanExecute(...);`

Bind a `Command` to a `BindableButton` in UXML using Unity's standard data binding:
```xml
<Sim.Faciem.BindableButton command="{Binding LoginCommand}" text="Login" />
```

---

### DI Bridge

Sim.Faciem is DI-agnostic. It communicates with your container through two interfaces:

**`IDIRegistrationBridge`** — used at startup to register services:
```csharp
void RegisterSingleton<TInterface, TImpl>() where TImpl : class, TInterface;
void RegisterTransient(Type tInterface, Type tImpl);
```

**`IDIContainerBridge`** — used at runtime to resolve instances:
```csharp
T ResolveInstance<T>() where T : class;
object ResolveInstance(Type type);
```

Implement both on your DI container's registration context. The demo project uses VContainer:

```csharp
// VContainer example
public class GameLifetimeScope : LifetimeScope, IDIRegistrationBridge
{
    private IContainerBuilder _builder;

    protected override void Configure(IContainerBuilder builder)
    {
        _builder = builder;
        builder.Register<IDIContainerBridge, FaciemVContainerBridge>(Lifetime.Singleton);
        builder.RegisterEntryPoint<FaciemSetupStartup>();

        // Registers Faciem's own services and all ViewIdAssets from Addressables
        FaciemBootstrapper.RegisterServices(this);
    }

    public void RegisterSingleton<TInterface, TImpl>() where TImpl : class, TInterface
        => _builder.Register<TInterface, TImpl>(Lifetime.Singleton);

    public void RegisterTransient(Type tInterface, Type tImpl)
        => _builder.Register(tInterface, tImpl, Lifetime.Transient);
}

// Bridge from VContainer's resolver to Faciem's IDIContainerBridge
public class FaciemVContainerBridge : IDIContainerBridge
{
    private readonly IObjectResolver _resolver;
    public FaciemVContainerBridge(IObjectResolver resolver) => _resolver = resolver;
    public T ResolveInstance<T>() where T : class => _resolver.Resolve<T>();
    public object ResolveInstance(Type type) => _resolver.Resolve(type);
}

// Startup: assign the bridge so Faciem can resolve at runtime
public class FaciemSetupStartup : IStartable
{
    public FaciemSetupStartup(IDIContainerBridge bridge)
        => FaciemBridge.ContainerBridge = bridge;
    public void Start() { }
}
```

---

### `FaciemUIDocument` — Runtime Entry Point

Add `FaciemUIDocument` alongside Unity's `UIDocument` component on your root UI GameObject. Assign the `ViewIdAsset` for your shell/root view and it will:

1. Register itself as `FaciemBridge.RootViewId`.
2. Resolve `ShellViewModel` from the DI container.
3. Set the `UIDocument`'s `rootVisualElement.dataSource` to the shell ViewModel.
4. Register all `Region` elements found in the root document.
5. Call `ShellViewModel.NavigateTo()` to start the initial navigation.

---

### `FaciemBootstrapper`

`FaciemBootstrapper` registers all of Faciem's internal services and discovers `ViewIdAsset` instances from Addressables:

```csharp
// Synchronous (blocks — use only if your DI framework requires it)
FaciemBootstrapper.RegisterServices(registrationBridge);

// Asynchronous (preferred)
await FaciemBootstrapper.RegisterServicesAsync(registrationBridge);
```

It registers:
- `IViewModelConstructionService`
- `IViewIdRegistry`
- `INavigationService`
- `ShellViewModel`

All `ViewIdAsset` assets tagged with the Addressables label **`ViewId`** are loaded and their `IDataContext` / `ViewModel` types are registered as transient services.

---

## 🎛️ Built-in UI Toolkit Controls

All controls are `[UxmlElement]` types and can be placed directly in UXML files. They are data-binding-ready out of the box.

### `Region` (UxmlElement)

Dynamic view placeholder. Assign a `RegionNameDefinition` asset via the `region-name` attribute.

```xml
<Sim.Faciem.Region region-name="MainRegion" style="flex-grow: 1;" />
<Sim.Faciem.Region region-name="SidePanel" support-multiple-views="false" />
```

---

### `BindableButton`

Extends Unity's `Button` with a `Command` binding property. Automatically reflects `CanExecute` (enabled state) and `IsVisible` (display style) from the bound Command.

```xml
<Sim.Faciem.BindableButton command="{Binding SaveCommand}" text="Save" />
```

```csharp
// In ViewModel
public Command SaveCommand { get; }

public MyViewModel()
{
    SaveCommand = Command
        .Execute(Save)
        .WithCanExecute(Property.Observe(vm => vm.HasUnsavedChanges))
        .Build();
}
```

---


## 📡 VisualElement Observable Extensions

`VisualElementObservableExtensions` turns UI Toolkit events into R3 `Observable<T>` streams, enabling reactive event handling without manual callback registration/cleanup.

```csharp
// Subscribe to pointer events
visualElement.PointerDownAsObservable()
    .Subscribe(evt => Debug.Log($"Pointer down at {evt.position}"))
    .AddTo(this);

// React to value changes on any INotifyValueChanged<T>
myTextField.ObservableValueChanged()
    .Throttle(TimeSpan.FromMilliseconds(300))
    .Subscribe(change => Search(change.newValue))
    .AddTo(this);
```

**Available extensions:**

| Extension | Event |
|---|---|
| `ObservableValueChanged<T>()` | `ChangeEvent<T>` on `INotifyValueChanged<T>` |
| `AttachToPanelAsObservable()` | `AttachToPanelEvent` |
| `DetachFromPanelAsObservable()` | `DetachFromPanelEvent` |
| `PointerDownAsObservable()` | `PointerDownEvent` |
| `PointerUpAsObservable()` | `PointerUpEvent` |
| `PointerEnterAsObservable()` | `PointerEnterEvent` |
| `ClickAsObservable()` | `ClickEvent` |
| `MouseDownAsObservable()` | `MouseDownEvent` |
| `MouseUpAsObservable()` | `MouseUpEvent` |
| `MouseEnterAsObservable()` | `MouseEnterEvent` |
| `MouseLeaveAsObservable()` | `MouseLeaveEvent` |
| `MouseMoveObservable()` | `MouseMoveEvent` |
| `WheelEventAsObservable()` | `WheelEvent` |
| `FocusInAsObservable()` | `FocusInEvent` |
| `FocusOutAsObservable()` | `FocusOutEvent` |
| `BlurAsObservable()` | `BlurEvent` |
| `GeometryChangedAsObservable()` | `GeometryChangedEvent` |
| `KeyDownAsObservable()` | `KeyDownEvent` |
| `ObserveEvent<T>()` | Any `EventBase<T>` |

---

## 🎨 Design-Time Data Context

`DesignTimeDataContext` is a `ScriptableObject` base class intended for use in the **UI Builder**. Assign a concrete subclass as the data source in UI Builder to preview realistic data without running the game.

```csharp
[CreateAssetMenu(menuName = "Design Time/MainMenuContext")]
public class MainMenuDesignContext : DesignTimeDataContext
{
    public string Title => "Main Menu (Design)";
    public bool IsLoading => false;
}
```

---

## ⚙️ Source Code Generation

Both `ViewIdAsset` and `RegionNameDefinition` support **source code generation**. When enabled on the asset:

1. Check the **`SourceCodeGeneration`** toggle on the asset.
2. Assign a `MonoScript` target file where the static property should be generated.
3. Save/validate the asset — the property is written automatically.

This removes the need to type magic strings anywhere in your project:

```csharp
// Generated in WellKnownViewIds.cs
public static ViewId SettingsView { get; } = ViewId.From("SettingsView");

// Generated in WellKnownRegionNames.cs
public static RegionName MainRegion { get; } = RegionName.From("MainRegion");
```

---

## 🖥️ Editor Integration

Sim.Faciem provides first-class support for Unity Editor tooling. The same Region/ViewModel/Navigation pattern works seamlessly inside editor windows and panels.

### Editor DI Container

The editor uses its own lightweight DI container (`EditorInjector`) seeded by **`EditorServiceInstaller`** scriptable objects.

Create a custom installer to register your editor-only services:

```csharp
[CreateAssetMenu(menuName = "Faciem/Editor/MyInstaller")]
public class MyEditorInstaller : EditorServiceInstaller
{
    public override void Install(IEditorInjector injector)
    {
        injector.Register<IMyEditorService, MyEditorService>();
        injector.RegisterInstance<IMyConfig, IMyConfig>(myConfig);
    }
}
```

Create the ScriptableObject asset — it is automatically picked up and its `Install` method is called when the editor starts.

`IEditorInjector` also extends `IDIContainerBridge`, so you can resolve services anywhere in editor code:

```csharp
var service = EditorInjector.Instance.ResolveInstance<IMyEditorService>();
```

---

### `FaciemEditorWindow`

Base class for editor windows that participate in the Faciem navigation system.

```csharp
using Cysharp.Threading.Tasks;
using Plugins.Sim.Faciem.Editor;
using UnityEditor;

public class MyToolWindow : FaciemEditorWindow
{
    [MenuItem("Window/My Tool")]
    public static void ShowWindow() => GetWindow<MyToolWindow>("My Tool");

    protected override async UniTask NavigateTo()
    {
        // Called when the window opens. Navigation.Navigate() is available here.
        await Navigation.Navigate(WellKnownEditorViewIds.MyToolView, WindowRegionName);
    }

    protected override UniTask NavigateAway()
    {
        // Called when the window closes.
        return UniTask.CompletedTask;
    }
}
```

Configure the following serialized fields on the window (visible in the Inspector when the window asset is selected):

- **`_windowRegionName`** — the `RegionNameDefinition` asset for the root region of this window.
- **`_initialViewId`** — the `EditorViewIdAsset` to navigate to when the window opens.

---

### `FaciemPopupWindowContent`

Base class for `PopupWindowContent` panels with Faciem navigation.

```csharp
public class MyPopup : FaciemPopupWindowContent
{
    protected override RegionName RegionName => WellKnownRegionNames.PopupRegion;

    protected override async UniTask NavigateToPopup()
    {
        await Navigation.Navigate(WellKnownEditorViewIds.PopupView, RegionName);
    }
}
```

---

### `FaciemToolbarOverlay`

Base class for Unity toolbar overlays with separate toolbar and panel regions.

```csharp
[Overlay(typeof(SceneView), "My Overlay")]
public class MyOverlay : FaciemToolbarOverlay
{
    protected override RegionName PanelRegionName   => WellKnownRegionNames.OverlayPanel;
    protected override RegionName ToolbarRegionName => WellKnownRegionNames.OverlayToolbar;

    protected override async UniTask NavigateToPanel()
    {
        await Navigation.Navigate(WellKnownEditorViewIds.OverlayPanelView, PanelRegionName);
    }

    protected override async UniTask NavigateToToolbar()
    {
        await Navigation.Navigate(WellKnownEditorViewIds.OverlayToolbarView, ToolbarRegionName);
    }
}
```

---

### `EditorViewIdAsset`

A special subclass of `ViewIdAsset` for editor-only views. Use this in place of `ViewIdAsset` when the view/ViewModel should only exist in editor context.

---

## 🚀 Usage Examples

### Example 1 — Minimal Runtime Setup with VContainer

```csharp
// 1. Implement IDIRegistrationBridge on your LifetimeScope
public class GameLifetimeScope : LifetimeScope, IDIRegistrationBridge
{
    private IContainerBuilder _builder;

    protected override void Configure(IContainerBuilder builder)
    {
        _builder = builder;
        builder.Register<IDIContainerBridge, FaciemVContainerBridge>(Lifetime.Singleton);
        builder.RegisterEntryPoint<FaciemSetupStartup>();
        FaciemBootstrapper.RegisterServices(this);
    }

    public void RegisterSingleton<TInterface, TImpl>() where TImpl : class, TInterface
        => _builder.Register<TInterface, TImpl>(Lifetime.Singleton);

    public void RegisterTransient(Type tInterface, Type tImpl)
        => _builder.Register(tInterface, tImpl, Lifetime.Transient);
}

// 2. Add FaciemUIDocument to the UIDocument GameObject and assign your root ViewIdAsset
// 3. Done — Faciem bootstraps and navigates to your root view automatically
```

---

### Example 2 — Creating a ViewModel

```csharp
// Define the data context interface
public interface IScoreboardDataContext : IDataContext { }

public class ScoreboardViewModel : ViewModel<ScoreboardViewModel>, IScoreboardDataContext
{
    // Properties notified via SetProperty fire UI Toolkit bindings automatically
    private List<ScoreEntry> _scores = new();
    public List<ScoreEntry> Scores
    {
        get => _scores;
        set => SetProperty(ref _scores, value);
    }

    private bool _isRefreshing;
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public Command RefreshCommand { get; }

    public ScoreboardViewModel()
    {
        RefreshCommand = Command
            .ExecuteAsync(async ct =>
            {
                IsRefreshing = true;
                Scores = await FetchScoresAsync(ct);
                IsRefreshing = false;
            })
            .WithCanExecute(Property.Observe(vm => vm.IsRefreshing)
                .Select(r => !r))
            .Build();
    }

    protected override async UniTask NavigateTo()
    {
        await RefreshCommand.Execute(Unit.Default);
    }
}
```

---

### Example 3 — Navigating to a View

```csharp
public class MainMenuViewModel : ViewModel<MainMenuViewModel>, IMainMenuDataContext
{
    public Command OpenScoreboardCommand { get; }

    public MainMenuViewModel()
    {
        OpenScoreboardCommand = Command
            .ExecuteAsync(async ct =>
            {
                await Navigation.Navigate(
                    WellKnownViewIds.ScoreboardView,
                    WellKnownRegionNames.MainRegion);
            })
            .Build();
    }
}
```

---

### Example 4 — Commands with CanExecute

```csharp
public class FormViewModel : ViewModel<FormViewModel>, IFormDataContext
{
    private string _email = "";
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public Command SubmitCommand { get; }

    public FormViewModel()
    {
        // Button is only enabled when email is non-empty
        var canSubmit = Property.Observe(vm => vm.Email)
            .Select(email => !string.IsNullOrWhiteSpace(email));

        SubmitCommand = Command
            .Execute(Submit)
            .WithCanExecute(canSubmit)
            .Build();
    }

    private void Submit() => Debug.Log($"Submitting: {Email}");
}
```

---

### Example 5 — Nested Bindable Objects

```csharp
public class CharacterData : Bindable<CharacterData>
{
    private string _name = "Knight";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private int _health = 100;
    public int Health { get => _health; set => SetProperty(ref _health, value); }
}

public class HUDViewModel : ViewModel<HUDViewModel>, IHUDDataContext
{
    public CharacterData Player { get; } = new();

    public HUDViewModel()
    {
        // "Player.Name" and "Player.Health" now propagate change notifications to UI Toolkit
        AddNestedBindable(vm => vm.Player);
    }
}
```

In UXML:
```xml
<ui:Label text="{Binding Player.Name}" />
<ui:ProgressBar value="{Binding Player.Health}" high-value="100" />
```

---

### Example 6 — Creating an Editor Window

```csharp
public class AssetBrowserWindow : FaciemEditorWindow
{
    [MenuItem("Window/Asset Browser")]
    public static void Open() => GetWindow<AssetBrowserWindow>("Asset Browser");

    protected override async UniTask NavigateTo()
    {
        await Navigation.Navigate(
            WellKnownEditorViewIds.AssetBrowserView,
            WindowRegionName);
    }
}
```

---

## 📋 Requirements Summary

| Requirement | Source |
|---|---|
| Unity 6000.0+ | — |
| NuGetForUnity | `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity` |
| UniTask | `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` |
| R3 (Unity) | `https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity` |
| R3 (NuGet) | Install via NuGet → Manage NuGet Packages |
| Addressables | `com.unity.addressables` (via Package Manager) |
| DI Framework | Any — VContainer, Zenject, or custom via `IDIRegistrationBridge` / `IDIContainerBridge` |

---

## 📄 License

Sim.Faciem is licensed under the MIT License. See [LICENSE](LICENSE) for details.

---

*Made with ❤️ by [Simplicitas Studios](https://github.com/Simplicitas-Studios)*
