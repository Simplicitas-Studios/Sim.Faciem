using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Sim.Faciem.Tests
{
    public sealed class NavigationTypedViewTests
    {
        private static readonly RegionName Main = RegionName.From("typed/main");
        private readonly List<Model> _models = new();
        private readonly List<ToggleView> _views = new();
        private readonly List<ViewIdAsset> _assets = new();
        private readonly List<UniTaskCompletionSource> _gates = new();
        private Window _window;
        private EditorWindow _previous;
        private Region _region;
        private RegionManager _manager;
        private INavigationService _navigation;
        private Action<Model> _created;
        private bool _failBind;
        private bool _failConstruction;
        private bool _failApply;
        private int _modelResolutions;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _models.Clear();
            _views.Clear();
            _assets.Clear();
            _gates.Clear();
            _created = null;
            _failBind = false;
            _failConstruction = false;
            _failApply = false;
            _modelResolutions = 0;
            _previous = EditorWindow.focusedWindow;
            _window = ScriptableObject.CreateInstance<Window>();
            _window.Show();
            _region = new Region(Main);
            _window.rootVisualElement.Add(_region);
            _manager = new RegionManager();
            _manager.AddRegion(_region);
            foreach (string id in new[] { "A", "B", "C" })
            {
                ViewIdAsset asset = ScriptableObject.CreateInstance<ViewIdAsset>();
                asset.ViewId = Id(id);
                asset.View = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.sim.faciem/Tests/Editor/NavigationFixtures/Typed.uxml");
                asset.DataContext = new MonoScriptReference { TypeName = typeof(Model).AssemblyQualifiedName };
                asset.ViewModel = new MonoScriptReference { TypeName = typeof(Model).AssemblyQualifiedName };
                asset.TypedView = id == "A" ? new MonoScriptReference { TypeName = typeof(ToggleView).AssemblyQualifiedName } : null;
                Assert.That(asset.View, Is.Not.Null);
                _assets.Add(asset);
            }
            _navigation = new NavigationService(new ViewModelConstructionService(new Bridge(this)), new Registry(_assets), null);
            for (int frame = 0; frame < 60 && _region.panel == null; frame++)
            {
                yield return null;
            }
            Assert.That(_region.panel, Is.Not.Null);
        }

        private static ViewId Id(string id) => ViewId.From("typed/" + id);
        private ViewIdAsset Asset(string id) => _assets.Single(asset => asset.ViewId.Equals(Id(id)));
        private VisualElement Tree(string id = "A") => _region.TryGetView(Id(id), out VisualElement view) ? view : null;
        private Model Current(string id = "A") => (Model)Tree(id).dataSource;
        private ToggleView Behavior => (ToggleView)_region.OwnedEntries.Single(entry => entry.Id.Equals(Id("A"))).TypedView;
        private UniTask<NavigationResult> Request(string id = "A", NavigationPolicy policy = NavigationPolicy.Fifo,
            NavigationEntryMode entry = NavigationEntryMode.Default) => _navigation.NavigateToResult(_manager, Id(id), Main, NavigationParameters.Empty, policy, entry).Preserve();
        private static NavigationResult Done(UniTask<NavigationResult> task)
        {
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return task.GetAwaiter().GetResult();
        }
        private static void Success(UniTask<NavigationResult> task) => Assert.That(Done(task).Status, Is.EqualTo(NavigationStatus.Succeeded));
        private void Clear() => Success(_navigation.ClearResult(_manager, Main));
        private UniTaskCompletionSource Gate()
        {
            var gate = new UniTaskCompletionSource();
            _gates.Add(gate);
            return gate;
        }
        private void Selected(int index)
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.That(Tree().Q<Label>("item-" + i).ClassListContains("item--selected"), Is.EqualTo(i == index));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Empty_optional_field_preserves_declarative_only_cache_and_legacy_navigation(bool emptyReference)
        {
            Asset("A").TypedView = emptyReference ? new MonoScriptReference() : null;
            UniTask entry = _navigation.NavigateTo(_manager, Id("A"), Main, NavigationParameters.Empty);
            entry.GetAwaiter().GetResult();
            Model model = Current();
            VisualElement root = Tree();
            Assert.That(_region.OwnedEntries.Single().TypedView, Is.Null);
            Clear();
            Success(Request());
            Assert.That(Current(), Is.SameAs(model));
            Assert.That(Tree(), Is.SameAs(root));
            Assert.That(_views, Is.Empty);
            Assert.That(_modelResolutions, Is.EqualTo(1));
        }

        [Test]
        public void Typed_constructor_receives_exact_cached_model_and_existing_UXML_root_once()
        {
            Success(Request());
            ToggleView behavior = Behavior;
            Assert.That(behavior.SuppliedModel, Is.SameAs(Current()));
            Assert.That(behavior.SuppliedRoot, Is.SameAs(Tree()));
            Assert.That(typeof(VisualElement).IsAssignableFrom(behavior.GetType()), Is.False);
            Success(Request("B"));
            Success(Request());
            Assert.That(Behavior, Is.SameAs(behavior));
            Assert.That(_views.Count, Is.EqualTo(1));
            Assert.That(_modelResolutions, Is.EqualTo(2), "A and B only; typed construction never resolves another model.");
            Assert.That(Current().Observers, Is.EqualTo(1));
        }

        [Test]
        public void Replaying_state_applies_initial_classes_immediately_and_reacts_to_add_remove()
        {
            _created = model => model.Selected.Value = 2;
            Success(Request());
            Selected(2);
            Current().Selected.Value = 1;
            Selected(1);
            Current().Selected.Value = -1;
            Selected(-1);
            Assert.That(Behavior.Applies, Is.EqualTo(3));
        }

        [Test]
        public void Default_same_ID_updates_do_not_duplicate_binding_or_activation()
        {
            Success(Request());
            NavigationActivation activation = Current().Current;
            ToggleView behavior = Behavior;
            Success(Request());
            Assert.That(Current().Current, Is.SameAs(activation));
            Assert.That(behavior.Binds, Is.EqualTo(1));
            Assert.That(Current().Observers, Is.EqualTo(1));
            int calls = behavior.Applies;
            Current().Selected.Value = 3;
            Assert.That(behavior.Applies, Is.EqualTo(calls + 1));
            Selected(3);
        }

        [Test]
        public void Clear_unsubscribes_without_final_disposal_and_cache_return_replays_latest()
        {
            Success(Request());
            ToggleView behavior = Behavior;
            Model model = Current();
            VisualElement root = Tree();
            model.Selected.Value = 1;
            Clear();
            int calls = behavior.Applies;
            Assert.That(model.Observers, Is.Zero);
            Assert.That(behavior.IsDisposed, Is.False);
            Assert.That(model.IsDisposed, Is.False);
            model.Selected.Value = 3;
            Assert.That(behavior.Applies, Is.EqualTo(calls));
            Success(Request());
            Assert.That(Tree(), Is.SameAs(root));
            Assert.That(Current(), Is.SameAs(model));
            Assert.That(Behavior, Is.SameAs(behavior));
            Assert.That(model.Observers, Is.EqualTo(1));
            Selected(3);
        }

        [Test]
        public void Explicit_reentry_rebinds_once_and_old_captured_callback_cannot_use_new_activation()
        {
            Success(Request());
            ToggleView behavior = Behavior;
            Action<int> obsolete = behavior.Captured;
            NavigationActivation old = Current().Current;
            Success(Request(entry: NavigationEntryMode.ReenterIfActive));
            Assert.That(Current().Current, Is.Not.SameAs(old));
            Assert.That(behavior.Binds, Is.EqualTo(2));
            Assert.That(Current().Observers, Is.EqualTo(1));
            Current().Selected.Value = 0;
            int calls = behavior.Applies;
            obsolete(3);
            Assert.That(behavior.Applies, Is.EqualTo(calls));
            Selected(0);
        }

        [Test]
        public void Latest_invalidation_fences_subscriptions_before_uncooperative_entry_returns()
        {
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = () => gate.Task;
            UniTask<NavigationResult> first = Request(policy: NavigationPolicy.LatestRequestWins);
            Model model = Current();
            ToggleView behavior = Behavior;
            Assert.That(model.Observers, Is.EqualTo(1));
            int calls = behavior.Applies;
            UniTask<NavigationResult> obsolete = Request("B", NavigationPolicy.LatestRequestWins);
            UniTask<NavigationResult> winner = Request("C", NavigationPolicy.LatestRequestWins);
            model.Selected.Value = 3;
            behavior.Captured(2);
            Assert.That(behavior.Applies, Is.EqualTo(calls), "Subscription still owned until drain, but publications are invalid immediately.");
            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Pending));
            _created = null;
            gate.TrySetResult();
            Assert.That(Done(first).Status, Is.EqualTo(NavigationStatus.Superseded));
            Assert.That(Done(obsolete).Status, Is.EqualTo(NavigationStatus.Superseded));
            Success(winner);
            Assert.That(model.Observers, Is.Zero);
            Assert.That(_modelResolutions, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Detached_panel_fences_writes_and_explicit_reentry_reapplies_current_state()
        {
            Success(Request());
            ToggleView behavior = Behavior;
            Model model = Current();
            int calls = behavior.Applies;
            _region.RemoveFromHierarchy();
            model.Selected.Value = 2;
            Assert.That(behavior.Applies, Is.EqualTo(calls));
            _window.rootVisualElement.Add(_region);
            yield return null;
            Success(Request(entry: NavigationEntryMode.ReenterIfActive));
            Selected(2);
        }

        [Test]
        public void Moved_or_foreign_root_cannot_receive_guarded_mutations()
        {
            Success(Request());
            ToggleView behavior = Behavior;
            Model model = Current();
            VisualElement root = Tree();
            int calls = behavior.Applies;
            _window.rootVisualElement.Add(root);
            model.Selected.Value = 2;
            Assert.That(behavior.Applies, Is.EqualTo(calls));
            _region.Add(root); // Explicit harness restoration of its own tree, not framework containment/repair.
            root.dataSource = new object();
            model.Selected.Value = 3;
            Assert.That(behavior.Applies, Is.EqualTo(calls));
            root.dataSource = model;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Terminal_release_disposes_subscriptions_and_releases_controller_references_once(bool inactive)
        {
            Success(Request());
            ToggleView behavior = Behavior;
            Model model = Current();
            VisualElement root = Tree();
            if (inactive)
            {
                Success(Request("B"));
            }
            Success(_navigation.TeardownResult(new[] { _region }, candidate => _models.Any(known => ReferenceEquals(known, candidate))));
            Assert.That(model.Observers, Is.Zero);
            Assert.That(model.Disposals, Is.EqualTo(1));
            Assert.That(behavior.IsDisposed, Is.True);
            Assert.That(behavior.Model, Is.Null);
            Assert.That(behavior.VisualRoot, Is.Null);
            Assert.That(root.parent, Is.Null);
            Assert.That(root.dataSource, Is.Null);
            Assert.That(Done(Request()).Status, Is.EqualTo(NavigationStatus.ScopeClosed));
            Assert.That(Done(_navigation.TeardownResult(new[] { _region }, _ => true)).Status, Is.EqualTo(NavigationStatus.NoOp));
            Assert.That(model.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void Terminal_barrier_waits_for_entry_and_does_not_release_typed_resources_early()
        {
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = () => gate.Task;
            UniTask<NavigationResult> entry = Request(policy: NavigationPolicy.LatestRequestWins);
            Model model = Current();
            ToggleView behavior = Behavior;
            int calls = behavior.Applies;
            UniTask<NavigationResult> clear = _navigation.ClearResult(_manager, Main, NavigationPolicy.LatestRequestWins).Preserve();
            UniTask<NavigationResult> final = _navigation.TeardownResult(new[] { _region }, candidate => ReferenceEquals(candidate, model)).Preserve();
            model.Selected.Value = 2;
            Assert.That(behavior.Applies, Is.EqualTo(calls));
            Assert.That(final.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(behavior.IsDisposed, Is.False);
            Assert.That(model.IsDisposed, Is.False);
            Assert.That(model.Observers, Is.EqualTo(1));
            gate.TrySetResult();
            Assert.That(Done(entry).Status, Is.EqualTo(NavigationStatus.Superseded));
            NavigationResult cleared = Done(clear);
            Assert.That(cleared.Status == NavigationStatus.Succeeded || cleared.Status == NavigationStatus.NoOp, Is.True);
            Success(final);
            Assert.That(behavior.IsDisposed, Is.True);
            Assert.That(model.Observers, Is.Zero);
            Assert.That(model.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void Initial_replay_application_failure_is_a_native_entry_failure_not_silent_success()
        {
            _failApply = true;
            NavigationResult result = Done(Request());
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(result.Stage, Is.EqualTo(NavigationStage.Entry));
            Assert.That(result.Recovery, Is.EqualTo(NavigationRecovery.Contained));
            Assert.That(Current().Observers, Is.Zero);
            Assert.That(_region.ActiveViews, Is.Empty);
        }

        [Test]
        public void Model_entry_failure_also_cleans_already_bound_typed_subscriptions()
        {
            _created = model => model.OnTo = () => UniTask.FromException(new InvalidOperationException("Model entry fault"));
            NavigationResult result = Done(Request());
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(result.Stage, Is.EqualTo(NavigationStage.Entry));
            Assert.That(Current().Observers, Is.Zero);
            ToggleView view = Behavior;
            Current().OnTo = null;
            Success(Request());
            Assert.That(Behavior, Is.SameAs(view));
            Assert.That(Current().Observers, Is.EqualTo(1));
        }

        [Test]
        public void Typed_constructor_failure_preserves_active_UI_and_keeps_uncommitted_model_DI_owned()
        {
            Success(Request("B"));
            VisualElement previous = Tree("B");
            _failConstruction = true;
            NavigationResult failed = Done(Request());
            Assert.That(failed.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(failed.Stage, Is.EqualTo(NavigationStage.ViewPreparation));
            Assert.That(failed.Recovery, Is.EqualTo(NavigationRecovery.Preserved));
            Assert.That(_region.ActiveViews, Is.EqualTo(new[] { Id("B") }));
            Assert.That(Tree("B"), Is.SameAs(previous));
            Assert.That(Tree(), Is.Null);
            Assert.That(_models.Last().IsDisposed, Is.False, "No cache transfer; bridge/DI owner retains its candidate.");
        }

        [Test]
        public void Partial_OnBind_failure_is_contained_and_recovery_reuses_controller_without_leaking_subscriptions()
        {
            _failBind = true;
            NavigationResult failure = Done(Request());
            Assert.That(failure.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(failure.Stage, Is.EqualTo(NavigationStage.Entry));
            Assert.That(failure.Recovery, Is.EqualTo(NavigationRecovery.Contained));
            ToggleView behavior = Behavior;
            Model model = Current();
            Assert.That(model.Observers, Is.Zero);
            Assert.That(_region.ActiveViews, Is.Empty);
            _failBind = false;
            Success(Request());
            Assert.That(Behavior, Is.SameAs(behavior));
            Assert.That(model.Observers, Is.EqualTo(1));
            Assert.That(_views.Count, Is.EqualTo(1));
        }

        [Test]
        public void Abandoned_detached_controller_is_released_while_model_remains_DI_owned()
        {
            Success(Request("B"));
            UniTaskCompletionSource gate = Gate();
            Current("B").OnAway = () => gate.Task;
            UniTask<NavigationResult> prepare = Request(policy: NavigationPolicy.LatestRequestWins);
            ToggleView abandoned = _views.Single();
            Model candidate = abandoned.SuppliedModel;
            Assert.That(abandoned.Binds, Is.Zero);
            Assert.That(Tree(), Is.Null);
            UniTask<NavigationResult> winner = Request("C", NavigationPolicy.LatestRequestWins);
            gate.TrySetResult();
            Assert.That(Done(prepare).Status, Is.EqualTo(NavigationStatus.Superseded));
            Success(winner);
            Assert.That(abandoned.IsDisposed, Is.True);
            Assert.That(abandoned.VisualRoot, Is.Null);
            Assert.That(candidate.IsDisposed, Is.False);
            Assert.That(candidate.Observers, Is.Zero);
        }

        [Test]
        public void Typed_legacy_entry_throws_binding_failure_after_native_containment()
        {
            _failBind = true;
            UniTask task = _navigation.NavigateTo(_manager, Id("A"), Main, NavigationParameters.Empty);
            Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
            Assert.That(_region.ActiveViews, Is.Empty);
            Assert.That(Current().Observers, Is.Zero);
        }

        [TestCase("missing-type")]
        [TestCase("wrong-base")]
        [TestCase("abstract")]
        [TestCase("open-generic")]
        public void Invalid_typed_registration_is_rejected_before_model_construction(string invalid)
        {
            Asset("A").TypedView.TypeName = invalid == "missing-type" ? "Does.Not.Exist, Missing" :
                (invalid == "wrong-base" ? typeof(string) : invalid == "abstract" ? typeof(View<Model>) : typeof(View<>)).AssemblyQualifiedName;
            NavigationResult failed = Done(Request());
            Assert.That(failed.Status, Is.EqualTo(NavigationStatus.InvalidConfiguration));
            Assert.That(failed.Recovery, Is.EqualTo(NavigationRecovery.Preserved));
            Assert.That(_modelResolutions, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Unsupported_global_or_multiple_view_targets_do_not_change_existing_untyped_support(bool global)
        {
            _region.IsGlobal = global;
            _region.SupportMultipleViews = !global;
            Assert.That(Done(Request()).Status, Is.EqualTo(NavigationStatus.UnsupportedOperation));
            Assert.That(_modelResolutions, Is.Zero);
            _region.IsGlobal = false;
            _region.SupportMultipleViews = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Model model in _models)
            {
                model.OnTo = null;
                model.OnAway = null;
            }
            foreach (UniTaskCompletionSource gate in _gates)
            {
                gate.TrySetResult();
            }
            BaseViewModel[] cached = _region.OwnedModels;
            NavigationResult final = Done(_navigation.TeardownResult(new[] { _region }, model => _models.Any(known => ReferenceEquals(known, model))));
            Assert.That(final.Status == NavigationStatus.Succeeded || final.Status == NavigationStatus.NoOp, Is.True, final.Error?.ToString());
            Assert.That(_views.All(view => view.IsDisposed), Is.True);
            Assert.That(_models.All(model => model.Observers == 0), Is.True);
            Assert.That(cached.All(model => model.IsDisposed), Is.True, "Native teardown, not bridge cleanup, finalized cached models.");
            // Separate fixture bridge/DI owner releases only candidates that never transferred to the native cache.
            foreach (Model model in _models)
            {
                if (!model.IsDisposed)
                {
                    model.Dispose();
                }
                Assert.That(model.Disposals, Is.EqualTo(1));
            }
            _window.Close();
            foreach (ViewIdAsset asset in _assets)
            {
                Object.DestroyImmediate(asset);
            }
            if (_previous != null)
            {
                _previous.Focus();
            }
            yield return null;
        }

        public sealed class Model : BaseViewModel
        {
            internal readonly NavigationTypedViewTests Fixture;
            public readonly ReactiveProperty<int> Selected = new(-1);
            public readonly Observable<int> Changes;
            public Func<UniTask> OnTo;
            public Func<UniTask> OnAway;
            public int Observers;
            public int Disposals;
            public NavigationActivation Current => Activation;
            internal Model(NavigationTypedViewTests fixture)
            {
                Fixture = fixture;
                Changes = Observable.Create<int>(observer =>
                {
                    Observers++;
                    IDisposable subscription = Selected.Subscribe(observer.OnNext);
                    return Disposable.Create(() =>
                    {
                        subscription.Dispose();
                        Observers--;
                    });
                });
                Disposables.Add(Selected);
                Disposables.Add(Disposable.Create(() => Disposals++));
            }
            protected override async UniTask NavigateTo(NavigationParameters parameters)
            {
                if (OnTo != null)
                {
                    await OnTo();
                }
            }
            protected override UniTask NavigateAway() => OnAway?.Invoke() ?? UniTask.CompletedTask;
        }

        public sealed class ToggleView : View<Model>
        {
            private readonly VisualElement[] _items;
            internal readonly Model SuppliedModel;
            internal readonly VisualElement SuppliedRoot;
            public Action<int> Captured;
            public int Binds;
            public int Applies;
            public ToggleView(Model model, VisualElement root) : base(model, root)
            {
                if (model.Fixture._failConstruction)
                {
                    throw new InvalidOperationException("Typed constructor fixture fault");
                }
                SuppliedModel = model;
                SuppliedRoot = root;
                _items = Enumerable.Range(0, 4).Select(index => root.Q<Label>("item-" + index)).Cast<VisualElement>().ToArray();
                model.Fixture._views.Add(this);
            }
            protected override void OnBind()
            {
                Binds++;
                Captured = Guard<int>(Apply);
                Bind(ViewModel.Changes, Apply);
                if (ViewModel.Fixture._failBind)
                {
                    throw new InvalidOperationException("Typed OnBind fixture fault");
                }
            }
            private void Apply(int selected)
            {
                Applies++;
                if (SuppliedModel.Fixture._failApply)
                {
                    throw new InvalidOperationException("Initial visual application fault");
                }
                for (int index = 0; index < _items.Length; index++)
                {
                    _items[index].EnableInClassList("item--selected", index == selected);
                }
            }
        }

        private sealed class Window : EditorWindow
        {
        }
        private sealed class Registry : Internal.IViewIdRegistry
        {
            private readonly List<ViewIdAsset> _assets;
            internal Registry(List<ViewIdAsset> assets) => _assets = assets;
            public bool TryGetViewId(ViewId id, out ViewIdAsset asset)
            {
                asset = _assets.FirstOrDefault(candidate => candidate.ViewId.Equals(id));
                return asset != null;
            }
        }
        private sealed class Bridge : IDIContainerBridge
        {
            private readonly NavigationTypedViewTests _fixture;
            internal Bridge(NavigationTypedViewTests fixture) => _fixture = fixture;
            public T ResolveInstance<T>() where T : class => (T)ResolveInstance(typeof(T));
            public object ResolveInstance(Type type)
            {
                if (type == typeof(INavigationService))
                {
                    return _fixture._navigation;
                }
                _fixture._modelResolutions++;
                var model = new Model(_fixture);
                _fixture._models.Add(model);
                _fixture._created?.Invoke(model);
                return model;
            }
        }
    }
}
