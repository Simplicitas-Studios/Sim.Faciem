using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Sim.Faciem.Tests
{
    [TestFixture]
    public sealed class NavigationSerializationTests
    {
        private static readonly RegionName Main = RegionName.From("serialization/main");
        private static readonly RegionName Other = RegionName.From("serialization/other");
        private readonly List<RecordingModel> _models = new();
        private readonly List<ViewIdAsset> _assets = new();
        private readonly List<UniTaskCompletionSource> _gates = new();
        private readonly List<UniTask> _tasks = new();
        private readonly List<string> _trace = new();
        private TestWindow _window;
        private EditorWindow _previousWindow;
        private RegionManager _manager;
        private Region _main;
        private Region _other;
        private NavigationService _navigation;
        private IViewModelConstructionService _construction;
        private Registry _registry;
        private Action<RecordingModel> _created;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _models.Clear();
            _assets.Clear();
            _gates.Clear();
            _tasks.Clear();
            _trace.Clear();
            _created = null;
            _previousWindow = EditorWindow.focusedWindow;
            _window = ScriptableObject.CreateInstance<TestWindow>();
            _window.titleContent = new GUIContent("Faciem serialization tests");
            _window.Show();
            _manager = new RegionManager();
            _main = AddRegion(Main);
            _other = AddRegion(Other);
            AddAsset("A", typeof(AModel));
            AddAsset("B", typeof(BModel));
            AddAsset("C", typeof(CModel));
            _registry = new Registry(_assets);
            _construction = new ViewModelConstructionService(new Bridge(this));
            _navigation = new NavigationService(_construction, _registry, null);
            for (int frame = 0; frame < 60 && _main.panel == null; frame++)
            {
                yield return null;
            }
            Assert.That(_main.panel, Is.Not.Null);
            Assert.That(_other.panel, Is.SameAs(_main.panel));
        }

        [Test]
        public void A_B_A_reuses_real_mounted_views_models_and_lifecycle_order()
        {
            Finish(Navigate("A"));
            VisualElement a = View(_main, "A");
            RecordingModel model = Model(_main, "A");
            Finish(Navigate("B"));
            Finish(Navigate("A"));
            Assert.That(View(_main, "A"), Is.SameAs(a));
            Assert.That(Model(_main, "A"), Is.SameAs(model));
            Assert.That(_models, Has.Count.EqualTo(2));
            Assert.That(a.panel, Is.Not.Null);
            Assert.That(a.parent, Is.SameAs(_main));
            Assert.That(View(_main, "B").Q<Label>("caption"), Is.Not.SameAs(a.Q<Label>("caption")));
            Assert.That(_trace, Is.EqualTo(new[]
            {
                "A:To.begin:active:Flex", "A:To.end",
                "A:Away.begin:inactive:None", "A:Away.end",
                "B:To.begin:active:Flex", "B:To.end",
                "B:Away.begin:inactive:None", "B:Away.end",
                "A:To.begin:active:Flex", "A:To.end"
            }));
            AssertSingle(_main, "A");
        }

        [Test]
        public void Overlapping_navigations_wait_in_FIFO_order()
        {
            Finish(Navigate("A"));
            UniTaskCompletionSource gate = Gate();
            Model(_main, "A").OnAway = () => gate.Task;
            UniTask b = Navigate("B");
            UniTask c = Navigate("C");
            try
            {
                Assert.That(b.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(c.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(_main.ActiveViews, Is.Empty);
                Assert.That(_models.Count(model => model.Name == "C"), Is.Zero);
            }
            finally
            {
                gate.TrySetResult();
                Finish(b);
                Finish(c);
            }
            AssertSingle(_main, "C");
            Assert.That(_trace.IndexOf("B:To.end"), Is.LessThan(_trace.IndexOf("C:To.begin:active:Flex")));
            Assert.That(_main.childCount, Is.EqualTo(3));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Navigate_and_Clear_share_the_same_queue(bool navigateFirst)
        {
            Finish(Navigate("A"));
            UniTaskCompletionSource gate = Gate();
            Model(_main, "A").OnAway = () => gate.Task;
            UniTask first = navigateFirst ? Navigate("B") : Clear();
            UniTask second = navigateFirst ? Clear() : Navigate("B");
            try
            {
                Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));
            }
            finally
            {
                gate.TrySetResult();
                Finish(first);
                Finish(second);
            }
            if (navigateFirst)
            {
                Assert.That(_main.ActiveViews, Is.Empty);
                Assert.That(View(_main, "B").style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            else
            {
                AssertSingle(_main, "B");
            }
        }

        [Test]
        public void Awaited_entry_finishes_before_next_request_constructs_or_mutates()
        {
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = () => model.Name == "A" ? gate.Task : UniTask.CompletedTask;
            UniTask a = Navigate("A");
            UniTask b = Navigate("B");
            try
            {
                AssertSingle(_main, "A");
                Assert.That(a.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(b.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(_models, Has.Count.EqualTo(1));
            }
            finally
            {
                gate.TrySetResult();
                Finish(a);
                Finish(b);
            }
            AssertSingle(_main, "B");
            Assert.That(_trace.IndexOf("A:To.end"), Is.LessThan(_trace.IndexOf("A:Away.begin:inactive:None")));
        }

        [Test]
        public void Independent_regions_with_the_same_name_can_navigate_concurrently()
        {
            Region independent = AddRegion(Main, false);
            var independentManager = new RegionManager();
            independentManager.AddRegion(independent);
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = () => model.Name == "A" ? gate.Task : UniTask.CompletedTask;
            UniTask a = Navigate("A");
            try
            {
                UniTask b = Track(_navigation.NavigateTo(independentManager, Id("B"), Main, NavigationParameters.Empty));
                Finish(b);
                Assert.That(a.Status, Is.EqualTo(UniTaskStatus.Pending));
                AssertSingle(independent, "B");
                AssertSingle(_main, "A");
            }
            finally
            {
                gate.TrySetResult();
                Finish(a);
                Finish(Track(_navigation.Clear(independentManager, Main)));
                independentManager.RemoveRegion(independent);
            }
        }

        [Test]
        public void Different_navigation_service_instances_still_serialize_the_same_region()
        {
            Finish(Navigate("A"));
            UniTaskCompletionSource gate = Gate();
            Model(_main, "A").OnAway = () => gate.Task;
            var second = new NavigationService(_construction, _registry, null);
            UniTask b = Navigate("B");
            var secondManager = new RegionManager();
            secondManager.AddRegion(_main);
            UniTask c = Track(second.NavigateTo(secondManager, Id("C"), Main, NavigationParameters.Empty));
            try
            {
                Assert.That(c.Status, Is.EqualTo(UniTaskStatus.Pending));
            }
            finally
            {
                gate.TrySetResult();
                Finish(b);
                Finish(c);
            }
            AssertSingle(_main, "C");
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Same_region_scoped_reentrancy_after_await_is_rejected(bool fromAway, bool clear)
        {
            UniTaskCompletionSource gate = Gate();
            string rejection = null;
            Func<RecordingModel, UniTask> callback = async model =>
            {
                await gate.Task;
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                    Finish(clear ? model.ScopedClear(Main) : model.ScopedNavigate(Id("C"), Main)));
                rejection = error.Message;
            };
            if (fromAway)
            {
                Finish(Navigate("A"));
                RecordingModel model = Model(_main, "A");
                model.OnAway = () => callback(model);
            }
            else
            {
                _created = model => model.OnTo = () => callback(model);
            }
            UniTask outer = Navigate(fromAway ? "B" : "A");
            Assert.That(outer.Status, Is.EqualTo(UniTaskStatus.Pending));
            gate.TrySetResult();
            Finish(outer);
            Assert.That(rejection, Does.Contain("cannot reenter"));
            Assert.That(_models.Any(model => model.Name == "C"), Is.False);
            AssertSingle(_main, fromAway ? "B" : "A");
        }

        [Test]
        public void Scoped_navigation_after_callback_completion_is_an_ordinary_request()
        {
            Finish(Navigate("A"));
            RecordingModel a = Model(_main, "A");
            Finish(Track(a.ScopedNavigate(Id("B"), Main)));
            AssertSingle(_main, "B");
            Finish(Track(a.ScopedClear(Main)));
            Assert.That(_main.ActiveViews, Is.Empty);
        }

        [Test]
        public void Lifecycle_can_await_navigation_to_an_independent_child_region()
        {
            _created = model =>
            {
                if (model.Name == "A")
                {
                    model.OnTo = () => model.ScopedNavigate(Id("B"), Other);
                }
            };
            Finish(Navigate("A"));
            AssertSingle(_main, "A");
            AssertSingle(_other, "B");
            Assert.That(_trace.IndexOf("B:To.end"), Is.LessThan(_trace.IndexOf("A:To.end")));
        }

        [Test]
        public void Indirect_cross_region_lifecycle_cycle_is_rejected_without_deadlock()
        {
            bool rejected = false;
            _created = model =>
            {
                if (model.Name == "A")
                {
                    model.OnTo = () => model.ScopedNavigate(Id("B"), Other);
                }
                if (model.Name == "B")
                {
                    model.OnTo = () =>
                    {
                        Assert.Throws<InvalidOperationException>(() => Finish(model.ScopedNavigate(Id("C"), Main)));
                        rejected = true;
                        return UniTask.CompletedTask;
                    };
                }
            };
            Finish(Navigate("A"));
            Assert.That(rejected, Is.True);
            AssertSingle(_main, "A");
            AssertSingle(_other, "B");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Lifecycle_failure_does_not_poison_queued_successors(bool failEntry)
        {
            UniTaskCompletionSource gate = Gate();
            _created = model =>
            {
                if (model.Name == (failEntry ? "B" : "A"))
                {
                    Func<UniTask> failure = async () =>
                    {
                        await gate.Task;
                        throw new InvalidOperationException("controlled lifecycle failure");
                    };
                    if (failEntry)
                    {
                        model.OnTo = failure;
                    }
                    else
                    {
                        model.OnAway = failure;
                    }
                }
            };
            Finish(Navigate("A"));
            UniTask b = Navigate("B");
            UniTask c = Navigate("C");
            Assert.That(c.Status, Is.EqualTo(UniTaskStatus.Pending));
            gate.TrySetResult();
            Assert.Throws<InvalidOperationException>(() => Finish(b));
            Finish(c);
            AssertSingle(_main, "C");
        }

        [Test]
        public void Clear_retains_cache_and_already_active_navigation_still_calls_To()
        {
            Finish(Navigate("A"));
            VisualElement view = View(_main, "A");
            RecordingModel model = Model(_main, "A");
            Finish(Navigate("A"));
            Assert.That(model.ToCount, Is.EqualTo(2));
            Assert.That(model.AwayCount, Is.Zero);
            Finish(Clear());
            Finish(Clear());
            Assert.That(model.AwayCount, Is.EqualTo(1));
            Assert.That(model.Disposals, Is.Zero);
            Assert.That(View(_main, "A").dataSource, Is.SameAs(model));
            Finish(Navigate("A"));
            Assert.That(View(_main, "A"), Is.SameAs(view));
            Assert.That(Model(_main, "A"), Is.SameAs(model));
            Assert.That(_models, Has.Count.EqualTo(1));
            AssertSingle(_main, "A");
        }

        [Test]
        public void Overlapping_region_groups_reserve_shared_identity_without_lock_order_deadlock()
        {
            var group = new RegionManager();
            Region second = AddRegion(Main, false);
            group.AddRegion(second);
            group.AddRegion(_main);
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = () => model.Name == "A" ? gate.Task : UniTask.CompletedTask;
            UniTask a = Track(_navigation.NavigateTo(group, Id("A"), Main, NavigationParameters.Empty));
            UniTask b = Navigate("B");
            try
            {
                Assert.That(a.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(b.Status, Is.EqualTo(UniTaskStatus.Pending));
            }
            finally
            {
                gate.TrySetResult();
                Finish(a);
                Finish(b);
            }
            AssertSingle(_main, "B");
            AssertSingle(second, "A");
            Finish(Track(_navigation.Clear(group, Main)));
        }

        private Region AddRegion(RegionName name, bool register = true)
        {
            var region = new Region(name);
            _window.rootVisualElement.Add(region);
            if (register)
            {
                _manager.AddRegion(region);
            }
            return region;
        }

        private void AddAsset(string name, Type model)
        {
            ViewIdAsset asset = ScriptableObject.CreateInstance<ViewIdAsset>();
            asset.name = name;
            asset.ViewId = Id(name);
            asset.View = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.sim.faciem/Tests/Editor/NavigationFixtures/" + name + ".uxml");
            Assert.That(asset.View, Is.Not.Null);
            asset.DataContext = new MonoScriptReference { TypeName = model.AssemblyQualifiedName };
            asset.ViewModel = new MonoScriptReference { TypeName = model.AssemblyQualifiedName };
            _assets.Add(asset);
        }

        private static ViewId Id(string name)
        {
            return ViewId.From("serialization/" + name);
        }

        private UniTaskCompletionSource Gate()
        {
            var gate = new UniTaskCompletionSource();
            _gates.Add(gate);
            return gate;
        }

        private UniTask Navigate(string name)
        {
            return Track(_navigation.NavigateTo(_manager, Id(name), Main, NavigationParameters.Empty));
        }

        private UniTask Clear()
        {
            return Track(_navigation.Clear(_manager, Main));
        }

        private UniTask Track(UniTask task)
        {
            UniTask preserved = task.Preserve();
            _tasks.Add(preserved);
            return preserved;
        }

        private static void Finish(UniTask task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending), "Do not block Unity's main thread.");
            task.GetAwaiter().GetResult();
        }

        private static VisualElement View(Region region, string name)
        {
            Assert.That(region.TryGetView(Id(name), out VisualElement view), Is.True);
            return view;
        }

        private static RecordingModel Model(Region region, string name)
        {
            return (RecordingModel)View(region, name).dataSource;
        }

        private static void AssertSingle(Region region, string name)
        {
            Assert.That(region.ActiveViews, Is.EqualTo(new[] { Id(name) }));
            Assert.That(region.ActiveViews.Distinct().Count(), Is.EqualTo(region.ActiveViews.Count));
            Assert.That(View(region, name).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(region.Children().Count(view => view.style.display.value != DisplayStyle.None), Is.EqualTo(1));
            Assert.That(View(region, name).panel, Is.Not.Null);
        }

        private void Observe(RecordingModel model, bool entering)
        {
            Region region = _window.rootVisualElement.Children().OfType<Region>().First(candidate =>
                candidate.TryGetView(Id(model.Name), out VisualElement view) && ReferenceEquals(view.dataSource, model));
            _trace.Add(model.Name + (entering ? ":To.begin:" : ":Away.begin:") +
                (region.ActiveViews.Contains(Id(model.Name)) ? "active:" : "inactive:") +
                View(region, model.Name).style.display.value);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                foreach (RecordingModel model in _models)
                {
                    model.OnTo = null;
                    model.OnAway = null;
                }
                foreach (UniTaskCompletionSource gate in _gates)
                {
                    gate.TrySetResult();
                }
                foreach (UniTask task in _tasks)
                {
                    Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
                }
                if (_navigation != null)
                {
                    Finish(Clear());
                    Finish(Track(_navigation.Clear(_manager, Other)));
                }
            }
            finally
            {
                if (_window != null)
                {
                    _window.Close();
                }
                foreach (RecordingModel model in _models)
                {
                    model.Dispose();
                }
                Directory.CreateDirectory("Logs/navigation-phase1");
                string test = TestContext.CurrentContext.Test.Name;
                File.WriteAllText("Logs/navigation-phase1/" + Regex.Replace(test, "[^a-zA-Z0-9._-]", "_") + ".txt",
                    "test=" + test + "\noutcome=" + TestContext.CurrentContext.Result.Outcome.Status + "\n" +
                    string.Join("\n", _trace) + "\nmodels=" + _models.Count + "\nresource-disposals=" +
                    string.Join(",", _models.Select(model => model.Name + ":" + model.Disposals)));
                foreach (ViewIdAsset asset in _assets)
                {
                    Object.DestroyImmediate(asset);
                }
                if (_previousWindow != null)
                {
                    _previousWindow.Focus();
                }
            }
        }

        private sealed class Registry : Internal.IViewIdRegistry
        {
            private readonly List<ViewIdAsset> _assets;
            internal Registry(List<ViewIdAsset> assets)
            {
                _assets = assets;
            }
            public bool TryGetViewId(ViewId id, out ViewIdAsset asset)
            {
                asset = _assets.FirstOrDefault(candidate => candidate.ViewId.Equals(id));
                return asset != null;
            }
        }

        private sealed class Bridge : IDIContainerBridge
        {
            private readonly NavigationSerializationTests _fixture;
            internal Bridge(NavigationSerializationTests fixture)
            {
                _fixture = fixture;
            }
            public T ResolveInstance<T>() where T : class
            {
                return (T)ResolveInstance(typeof(T));
            }
            public object ResolveInstance(Type type)
            {
                if (type == typeof(INavigationService))
                {
                    return _fixture._navigation;
                }
                RecordingModel model;
                if (type == typeof(AModel))
                {
                    model = new AModel(_fixture);
                }
                else if (type == typeof(BModel))
                {
                    model = new BModel(_fixture);
                }
                else if (type == typeof(CModel))
                {
                    model = new CModel(_fixture);
                }
                else
                {
                    throw new InvalidOperationException(type.FullName);
                }
                _fixture._models.Add(model);
                _fixture._created?.Invoke(model);
                return model;
            }
        }

        private abstract class RecordingModel : BaseViewModel
        {
            private readonly NavigationSerializationTests _fixture;
            internal readonly string Name;
            internal Func<UniTask> OnTo;
            internal Func<UniTask> OnAway;
            internal int ToCount;
            internal int AwayCount;
            internal int Disposals;
            protected RecordingModel(string name, NavigationSerializationTests fixture)
            {
                Name = name;
                _fixture = fixture;
                Disposables.Add(R3.Disposable.Create(() => Disposals++));
            }
            internal UniTask ScopedNavigate(ViewId id, RegionName region)
            {
                return Navigation.Navigate(id, region);
            }
            internal UniTask ScopedClear(RegionName region)
            {
                return Navigation.Clear(region);
            }
            protected override async UniTask NavigateTo(NavigationParameters parameters)
            {
                ToCount++;
                _fixture.Observe(this, true);
                if (OnTo != null)
                {
                    await OnTo();
                }
                _fixture._trace.Add(Name + ":To.end");
            }
            protected override async UniTask NavigateAway()
            {
                AwayCount++;
                _fixture.Observe(this, false);
                if (OnAway != null)
                {
                    await OnAway();
                }
                _fixture._trace.Add(Name + ":Away.end");
            }
        }

        private sealed class AModel : RecordingModel
        {
            internal AModel(NavigationSerializationTests fixture) : base("A", fixture)
            {
            }
        }
        private sealed class BModel : RecordingModel
        {
            internal BModel(NavigationSerializationTests fixture) : base("B", fixture)
            {
            }
        }
        private sealed class CModel : RecordingModel
        {
            internal CModel(NavigationSerializationTests fixture) : base("C", fixture)
            {
            }
        }
        public sealed class TestWindow : EditorWindow
        {
        }
    }
}
