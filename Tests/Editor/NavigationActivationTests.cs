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
    public sealed class NavigationActivationTests
    {
        private const NavigationPolicy Latest = NavigationPolicy.LatestRequestWins;
        private static readonly RegionName Main = RegionName.From("phase2/main");
        private static readonly RegionName Other = RegionName.From("phase2/other");
        private readonly List<Model> _models = new();
        private readonly List<ViewIdAsset> _assets = new();
        private readonly List<UniTaskCompletionSource> _gates = new();
        private readonly List<UniTask> _tasks = new();
        private readonly List<string> _trace = new();
        private Window _window;
        private EditorWindow _previousWindow;
        private RegionManager _manager;
        private Region _main;
        private Region _other;
        private INavigationService _navigation;
        private Action<Model> _created;

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
            _window = ScriptableObject.CreateInstance<Window>();
            _window.titleContent = new GUIContent("Faciem Phase 2 tests");
            _window.Show();
            _manager = new RegionManager();
            _main = AddRegion(Main);
            _other = AddRegion(Other);
            foreach (string name in new[] { "A", "B", "C" })
            {
                ViewIdAsset asset = ScriptableObject.CreateInstance<ViewIdAsset>();
                asset.name = name;
                asset.ViewId = Id(name);
                asset.View = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/com.sim.faciem/Tests/Editor/NavigationFixtures/" + name + ".uxml");
                Type type = name == "A" ? typeof(A) : name == "B" ? typeof(B) : typeof(C);
                asset.DataContext = new MonoScriptReference { TypeName = type.AssemblyQualifiedName };
                asset.ViewModel = new MonoScriptReference { TypeName = type.AssemblyQualifiedName };
                Assert.That(asset.View, Is.Not.Null);
                _assets.Add(asset);
            }
            _navigation = new NavigationService(new ViewModelConstructionService(new Bridge(this)), new Registry(_assets), null);
            for (int frame = 0; frame < 60 && _main.panel == null; frame++)
            {
                yield return null;
            }
            Assert.That(_main.panel, Is.Not.Null);
        }

        [Test]
        public void Queued_latest_skips_obsolete_model_view_and_callbacks()
        {
            UniTaskCompletionSource gate = Gate();
            OnCreate("A", model => model.OnTo = (_, _) => gate.Task);
            UniTask a = Navigate("A");
            UniTask b = Navigate("B", Latest);
            UniTask c = Navigate("C", Latest);
            Assert.That(a.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(b.Status, Is.EqualTo(UniTaskStatus.Pending), "Cancellation drains FIFO predecessors first.");
            Assert.That(Current(_main, "A").Current.IsValid, Is.True, "Opted-in requests cannot cancel FIFO work.");
            gate.TrySetResult();
            Finish(a);
            Cancelled(b);
            Finish(c);
            Assert.That(_models.Any(model => model.Name == "B"), Is.False);
            Assert.That(_main.TryGetView(Id("B"), out _), Is.False);
            AssertSingle(_main, "C");
        }

        [Test]
        public void Mixed_policy_keeps_fifo_requests_and_supersedes_only_opted_in_requests()
        {
            UniTaskCompletionSource gate = Gate();
            OnCreate("A", model => model.OnTo = (_, _) => gate.Task);
            UniTask firstA = Navigate("A");
            NavigationActivation first = Current(_main, "A").Current;
            UniTask b = Navigate("B", Latest);
            UniTask c = Navigate("C");
            UniTask lastA = Navigate("A", Latest);
            Assert.That(first.IsValid, Is.True);
            gate.TrySetResult();
            Finish(firstA);
            Cancelled(b);
            Finish(c);
            Finish(lastA);
            Assert.That(_models.Any(model => model.Name == "B"), Is.False);
            Assert.That(Current(_main, "C").ToCount, Is.EqualTo(1));
            AssertSingle(_main, "A");
        }

        [Test]
        public void Superseded_exit_is_drained_before_obsolete_target_is_skipped()
        {
            Finish(Navigate("A"));
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            UniTaskCompletionSource gate = Gate();
            a.OnAway = _ => gate.Task;
            UniTask b = Navigate("B", Latest);
            UniTask c = Navigate("C", Latest);
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(old.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(c.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_main.ActiveViews, Is.Empty);
            gate.TrySetResult();
            Cancelled(b);
            Finish(c);
            Assert.That(_models.Single(model => model.Name == "B").ToCount, Is.Zero);
            Assert.That(_main.TryGetView(Id("B"), out _), Is.False);
            AssertSingle(_main, "C");
        }

        [Test]
        public void Uncooperative_entry_is_invalidated_but_ownership_waits_for_callback_return()
        {
            UniTaskCompletionSource gate = Gate();
            bool latePublication = true;
            OnCreate("A", model => model.OnTo = async (_, lifetime) =>
            {
                await gate.Task;
                latePublication = lifetime.TryPublish(() => model.Publications++);
            });
            UniTask a = Navigate("A", Latest);
            Model modelA = Current(_main, "A");
            NavigationActivation first = modelA.Current;
            UniTask b = Navigate("B", Latest);
            Assert.That(first.IsValid, Is.False);
            Assert.That(first.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(modelA.Cleanups, Is.Zero, "Cleanup must not outrun an uncooperative callback.");
            int deferredCleanup = 0;
            first.Own(new Resource(() => deferredCleanup++));
            Assert.That(deferredCleanup, Is.Zero);
            Assert.That(b.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_models.Count, Is.EqualTo(1));
            AssertSingle(_main, "A", false);
            gate.TrySetResult();
            Cancelled(a);
            Finish(b);
            Assert.That(latePublication, Is.False);
            Assert.That(modelA.Publications, Is.Zero);
            Assert.That(modelA.Cleanups, Is.EqualTo(1));
            Assert.That(deferredCleanup, Is.EqualTo(1));
            AssertSingle(_main, "B");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Cooperative_entry_cancellation_unwinds_and_cleans_before_successor(bool clear)
        {
            UniTaskCompletionSource gate = Gate();
            OnCreate("A", model => model.OnTo = async (_, lifetime) =>
            {
                await gate.Task.AttachExternalCancellation(lifetime.CancellationToken);
            });
            UniTask a = Navigate("A", Latest);
            UniTask b = clear ? Clear(Latest) : Navigate("B", Latest);
            Cancelled(a);
            Finish(b);
            Assert.That(Current(_main, "A").Cleanups, Is.EqualTo(1));
            if (clear)
            {
                Assert.That(_main.ActiveViews, Is.Empty);
            }
            else
            {
                Assert.That(_trace.IndexOf("A:cleanup"), Is.LessThan(_trace.IndexOf("B:To")));
                AssertSingle(_main, "B");
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Latest_Navigate_and_Clear_supersede_each_other(bool clearIsNewer)
        {
            UniTaskCompletionSource gate = Gate();
            OnCreate("A", model => model.OnTo = (_, _) => gate.Task);
            UniTask blocker = Navigate("A");
            UniTask older = clearIsNewer ? Navigate("B", Latest) : Clear(Latest);
            UniTask newer = clearIsNewer ? Clear(Latest) : Navigate("B", Latest);
            gate.TrySetResult();
            Finish(blocker);
            Cancelled(older);
            Finish(newer);
            if (clearIsNewer)
            {
                Assert.That(_main.ActiveViews, Is.Empty);
                Assert.That(_models.Any(model => model.Name == "B"), Is.False);
            }
            else
            {
                AssertSingle(_main, "B");
            }
            Assert.That(Current(_main, "A").AwayCount, Is.EqualTo(1));
            Assert.That(Current(_main, "A").Cleanups, Is.EqualTo(1));
        }

        [Test]
        public void Executing_latest_Clear_drains_away_before_new_navigation_can_enter()
        {
            Finish(Navigate("A"));
            UniTaskCompletionSource gate = Gate();
            Model a = Current(_main, "A");
            a.OnAway = _ => gate.Task;
            UniTask clear = Clear(Latest);
            UniTask b = Navigate("B", Latest);
            Assert.That(clear.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(b.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(_models.Any(model => model.Name == "B"), Is.False);
            gate.TrySetResult();
            Cancelled(clear);
            Finish(b);
            Assert.That(a.AwayCount, Is.EqualTo(1));
            AssertSingle(_main, "B");
        }

        [Test]
        public void A_B_A_renews_activation_not_cached_objects_and_cleans_once()
        {
            Finish(Navigate("A"));
            VisualElement view = View(_main, "A");
            Model model = Current(_main, "A");
            NavigationActivation first = model.Current;
            Finish(Navigate("B"));
            Assert.That(first.IsValid, Is.False);
            Assert.That(first.CancellationToken.IsCancellationRequested, Is.True);
            int lateCleanup = 0;
            first.Own(new Resource(() => lateCleanup++));
            Assert.That(lateCleanup, Is.EqualTo(1));
            Finish(Navigate("A"));
            Assert.That(Current(_main, "A"), Is.SameAs(model));
            Assert.That(View(_main, "A"), Is.SameAs(view));
            Assert.That(model.Current, Is.Not.SameAs(first));
            Assert.That(first.TryPublish(() => model.Publications++), Is.False);
            Assert.That(model.Current.TryPublish(() => model.Publications++), Is.True);
            Finish(Clear());
            Finish(Clear());
            Assert.That(model.Cleanups, Is.EqualTo(2));
            Assert.That(model.FinalDisposals, Is.Zero);
        }

        [Test]
        public void Repeated_active_same_id_preserves_activation_without_forced_reentry()
        {
            Finish(Navigate("A", Latest));
            Model model = Current(_main, "A");
            NavigationActivation lifetime = model.Current;
            Finish(Navigate("A", Latest));
            Assert.That(model.Current, Is.SameAs(lifetime));
            Assert.That(model.ToCount, Is.EqualTo(2));
            Assert.That(model.AwayCount, Is.Zero);
            Assert.That(model.Cleanups, Is.Zero);
            Assert.That(model.Entries.Distinct().Count(), Is.EqualTo(1));
            AssertSingle(_main, "A");
        }

        [Test]
        public void Independent_regions_do_not_supersede_or_cancel_each_other()
        {
            UniTaskCompletionSource gate = Gate();
            OnCreate("A", model => model.OnTo = (_, _) => gate.Task);
            UniTask a = Navigate("A", Latest);
            NavigationActivation lifetime = Current(_main, "A").Current;
            Finish(Track(_navigation.NavigateTo(_manager, Id("B"), Other, NavigationParameters.Empty, Latest)));
            Assert.That(lifetime.IsValid, Is.True);
            Assert.That(a.Status, Is.EqualTo(UniTaskStatus.Pending));
            AssertSingle(_other, "B");
            gate.TrySetResult();
            Finish(a);
            AssertSingle(_main, "A");
        }

        [Test]
        public void Queued_multi_region_request_is_superseded_as_a_whole_on_any_overlap()
        {
            Region second = AddRegion(Main, false);
            var group = new RegionManager();
            group.AddRegion(_main);
            group.AddRegion(second);
            var secondOnly = new RegionManager();
            secondOnly.AddRegion(second);
            UniTaskCompletionSource gate = Gate();
            OnCreate("A", model => model.OnTo = (_, _) => gate.Task);
            UniTask a = Navigate("A");
            UniTask b = Track(_navigation.NavigateTo(group, Id("B"), Main, NavigationParameters.Empty, Latest));
            UniTask c = Track(_navigation.NavigateTo(secondOnly, Id("C"), Main, NavigationParameters.Empty, Latest));
            gate.TrySetResult();
            Finish(a);
            Cancelled(b);
            Finish(c);
            Assert.That(_models.Any(model => model.Name == "B"), Is.False);
            Assert.That(second.TryGetView(Id("B"), out _), Is.False);
            AssertSingle(_main, "A");
            AssertSingle(second, "C");
            Finish(Track(_navigation.Clear(secondOnly, Main)));
        }

        [Test]
        public void Executing_multi_region_entry_keeps_all_ownership_until_cancelled_callback_drains()
        {
            Region second = AddRegion(Main, false);
            var group = new RegionManager();
            group.AddRegion(_main);
            group.AddRegion(second);
            UniTaskCompletionSource gate = Gate();
            OnCreate("A", model => model.OnTo = (_, _) => gate.Task);
            UniTask a = Track(_navigation.NavigateTo(group, Id("A"), Main, NavigationParameters.Empty, Latest));
            Model model = Current(_main, "A");
            Assert.That(Current(second, "A"), Is.SameAs(model), "Legacy group model sharing remains unchanged.");
            UniTask b = Navigate("B", Latest);
            Assert.That(b.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(model.Current.IsValid, Is.False);
            gate.TrySetResult();
            Cancelled(a);
            Finish(b);
            Assert.That(model.Cleanups, Is.EqualTo(1));
            Assert.That(second.ActiveViews, Is.Empty);
            Assert.That(View(second, "A").style.display.value, Is.EqualTo(DisplayStyle.None));
            AssertSingle(_main, "B");
        }

        [Test]
        public void Shared_model_cohort_ends_once_without_mutating_an_unowned_region()
        {
            Region second = AddRegion(Main, false);
            var group = new RegionManager();
            group.AddRegion(_main);
            group.AddRegion(second);
            var secondOnly = new RegionManager();
            secondOnly.AddRegion(second);
            Finish(Track(_navigation.NavigateTo(group, Id("A"), Main, NavigationParameters.Empty)));
            Model a = Current(_main, "A");
            NavigationActivation cohort = a.Current;
            Assert.That(Current(second, "A"), Is.SameAs(a));
            Finish(Navigate("B"));
            Assert.That(cohort.IsValid, Is.False);
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(second.ActiveViews, Is.EqualTo(new[] { Id("A") }));
            Assert.That(View(second, "A").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(a.Current, Is.Null, "Shared models have one logical activation, not independent per-Region state.");
            Finish(Track(_navigation.NavigateTo(secondOnly, Id("A"), Main, NavigationParameters.Empty)));
            Assert.That(a.Current, Is.Not.SameAs(cohort));
            Assert.That(a.Current.IsValid, Is.True);
            Assert.That(a.AwayCount, Is.EqualTo(1), "No extra forced re-entry callback.");
            Finish(Track(_navigation.Clear(secondOnly, Main)));
            Assert.That(a.Cleanups, Is.EqualTo(2));
        }

        [TestCase("A")]
        [TestCase("B")]
        [TestCase("Clear")]
        public void Concurrent_callbacks_on_one_shared_model_are_rejected_without_ending_its_busy_activation(string request)
        {
            Region second = AddRegion(Main, false);
            var group = new RegionManager();
            group.AddRegion(_main);
            group.AddRegion(second);
            var secondOnly = new RegionManager();
            secondOnly.AddRegion(second);
            Finish(Track(_navigation.NavigateTo(group, Id("A"), Main, NavigationParameters.Empty)));
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            UniTaskCompletionSource gate = Gate();
            a.OnTo = (_, _) => gate.Task;
            UniTask owner = Navigate("A", Latest);
            try
            {
                UniTask conflicting = request == "Clear"
                    ? Track(_navigation.Clear(secondOnly, Main, Latest))
                    : Track(_navigation.NavigateTo(secondOnly, Id(request), Main, NavigationParameters.Empty, Latest));
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Finish(conflicting));
                Assert.That(error.Message, Does.Contain("in-flight lifecycle"));
                Assert.That(owner.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(lifetime.IsValid, Is.True);
                Assert.That(a.Cleanups, Is.Zero);
                Assert.That(second.ActiveViews, Is.EqualTo(new[] { Id("A") }));
            }
            finally
            {
                gate.TrySetResult();
                Finish(owner);
            }
            Finish(Track(_navigation.Clear(secondOnly, Main)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Lifecycle_exceptions_end_activation_and_do_not_poison_successors(bool entry)
        {
            UniTaskCompletionSource gate = Gate();
            OnCreate(entry ? "B" : "A", model =>
            {
                if (entry)
                {
                    model.OnTo = async (_, _) =>
                    {
                        await gate.Task;
                        throw new InvalidOperationException("controlled entry failure");
                    };
                }
                else
                {
                    model.OnAway = async _ =>
                    {
                        await gate.Task;
                        throw new InvalidOperationException("controlled exit failure");
                    };
                }
            });
            Finish(Navigate("A"));
            UniTask b = Navigate("B");
            UniTask c = Navigate("C");
            gate.TrySetResult();
            Assert.Throws<InvalidOperationException>(() => Finish(b));
            Finish(c);
            Assert.That(Current(_main, "A").Cleanups, Is.EqualTo(1));
            if (entry)
            {
                Model failed = Current(_main, "B");
                Assert.That(failed.Entries.Single().IsValid, Is.False);
                Assert.That(failed.Cleanups, Is.EqualTo(1));
            }
            AssertSingle(_main, "C");
        }

        [Test]
        public void Cleanup_attempts_every_resource_once_even_when_cancellation_and_disposal_throw()
        {
            Finish(Navigate("A"));
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            int attempted = 0;
            lifetime.CancellationToken.Register(() => throw new InvalidOperationException("observer failure"));
            lifetime.Own(new Resource(() =>
            {
                attempted++;
                throw new InvalidOperationException("resource failure");
            }));
            lifetime.Own(new Resource(() => attempted++));
            Assert.Throws<AggregateException>(() => Finish(Clear()));
            Assert.That(attempted, Is.EqualTo(2));
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(lifetime.IsValid, Is.False);
            Finish(Navigate("B"));
            Finish(Clear());
            Assert.That(attempted, Is.EqualTo(2));
        }

        [Test]
        public void Throwing_cancellation_observer_does_not_lose_new_request_or_release_old_ownership_early()
        {
            UniTaskCompletionSource gate = Gate();
            OnCreate("A", model => model.OnTo = (_, lifetime) =>
            {
                lifetime.CancellationToken.Register(() => throw new InvalidOperationException("cancel observer"));
                return gate.Task;
            });
            UniTask a = Navigate("A", Latest);
            UniTask b = Navigate("B", Latest);
            Assert.That(b.Status, Is.EqualTo(UniTaskStatus.Pending));
            gate.TrySetResult();
            Assert.Throws<AggregateException>(() => Finish(a));
            Finish(b);
            AssertSingle(_main, "B");
        }

        [Test]
        public void Captured_old_handle_fences_stale_completion_after_return_to_same_model()
        {
            Finish(Navigate("A"));
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            UniTaskCompletionSource gate = Gate();
            bool bodyRan = false;
            bool published = true;
            UniTask late = Track(CompleteLate());
            async UniTask CompleteLate()
            {
                await gate.Task;
                bodyRan = true;
                published = old.TryPublish(() => a.Publications++);
            }
            Finish(Navigate("B"));
            Finish(Navigate("A"));
            Assert.That(a.Current, Is.Not.SameAs(old));
            gate.TrySetResult();
            Finish(late);
            Assert.That(bodyRan, Is.True, "Cancellation cannot forcibly stop user code.");
            Assert.That(published, Is.False);
            Assert.That(a.Publications, Is.Zero);
            Assert.That(a.Current.TryPublish(() => a.Publications++), Is.True);
            Assert.That(a.Publications, Is.EqualTo(1));
        }

        [Test]
        public void Publication_requires_owning_unity_thread()
        {
            Finish(Navigate("A"));
            NavigationActivation lifetime = Current(_main, "A").Current;
            Assert.Throws<InvalidOperationException>(() =>
                System.Threading.Tasks.Task.Run(() => lifetime.TryPublish(() => { })).GetAwaiter().GetResult());
            Assert.That(lifetime.IsValid, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Rejected_scoped_latest_request_cannot_supersede_valid_queued_work(bool clear)
        {
            UniTaskCompletionSource gate = Gate();
            OnCreate("A", model => model.OnTo = async (_, lifetime) =>
            {
                await gate.Task;
                Assert.Throws<InvalidOperationException>(() => Finish(clear ? model.ScopedClear() : model.ScopedNavigate("C")));
                Assert.That(lifetime.IsValid, Is.True);
            });
            UniTask a = Navigate("A");
            UniTask b = Navigate("B", Latest);
            gate.TrySetResult();
            Finish(a);
            Finish(b);
            Assert.That(_models.Any(model => model.Name == "C"), Is.False);
            AssertSingle(_main, "B");
        }

        [Test]
        public void Activation_cleanup_uses_scoped_reentrancy_guard()
        {
            Finish(Navigate("A"));
            Model a = Current(_main, "A");
            bool rejected = false;
            a.Current.Own(new Resource(() =>
            {
                Assert.Throws<InvalidOperationException>(() => Finish(a.ScopedClear()));
                rejected = true;
            }));
            Finish(Navigate("B"));
            Assert.That(rejected, Is.True);
            AssertSingle(_main, "B");
        }

        [Test]
        public void Explicit_fifo_overloads_support_custom_services_and_latest_does_not_silently_fallback()
        {
            var custom = new CustomNavigation();
            Finish(custom.NavigateTo(_manager, Id("A"), Main, NavigationParameters.Empty, NavigationPolicy.Fifo));
            Finish(custom.Clear(_manager, Main, NavigationPolicy.Fifo));
            Assert.That(custom.Calls, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => custom.Clear(_manager, Main, Latest));
            Assert.Throws<ArgumentOutOfRangeException>(() => custom.Clear(_manager, Main, (NavigationPolicy)99));
            Assert.That(custom.Calls, Is.EqualTo(2));
        }

        private void OnCreate(string name, Action<Model> configure)
        {
            _created = model =>
            {
                if (model.Name == name)
                {
                    configure(model);
                }
            };
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

        private static ViewId Id(string name) => ViewId.From("phase2/" + name);
        private static VisualElement View(Region region, string name)
        {
            Assert.That(region.TryGetView(Id(name), out VisualElement view), Is.True);
            return view;
        }
        private static Model Current(Region region, string name) => (Model)View(region, name).dataSource;
        private static void AssertSingle(Region region, string name, bool valid = true)
        {
            Assert.That(region.ActiveViews, Is.EqualTo(new[] { Id(name) }));
            Assert.That(region.Children().Count(view => view.style.display.value != DisplayStyle.None), Is.EqualTo(1));
            Assert.That(View(region, name).panel, Is.Not.Null);
            if (valid)
            {
                Assert.That(Current(region, name).Current.IsValid, Is.True);
            }
        }
        private UniTaskCompletionSource Gate()
        {
            var gate = new UniTaskCompletionSource();
            _gates.Add(gate);
            return gate;
        }
        private UniTask Navigate(string name, NavigationPolicy policy = NavigationPolicy.Fifo)
        {
            return Track(_navigation.NavigateTo(_manager, Id(name), Main, NavigationParameters.Empty, policy));
        }
        private UniTask Clear(NavigationPolicy policy = NavigationPolicy.Fifo) => Track(_navigation.Clear(_manager, Main, policy));
        private UniTask Track(UniTask task)
        {
            UniTask preserved = task.Preserve();
            _tasks.Add(preserved);
            return preserved;
        }
        private static void Finish(UniTask task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending), "Never block a pending Unity callback.");
            task.GetAwaiter().GetResult();
        }
        private static void Cancelled(UniTask task)
        {
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.Throws<OperationCanceledException>(() => Finish(task));
        }

        [TearDown]
        public void TearDown()
        {
            try
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
                foreach (Model model in _models)
                {
                    model.Dispose();
                }
                Directory.CreateDirectory("Logs/navigation-phase2");
                string name = Regex.Replace(TestContext.CurrentContext.Test.Name, "[^a-zA-Z0-9._-]", "_");
                File.WriteAllText("Logs/navigation-phase2/" + name + ".txt", "outcome=" +
                    TestContext.CurrentContext.Result.Outcome.Status + "\n" + string.Join("\n", _trace) +
                    "\nmodels=" + _models.Count + "\n" + string.Join("\n", _models.Select(model =>
                        model.Name + ":To=" + model.ToCount + ":Away=" + model.AwayCount +
                        ":Lifetimes=" + model.Entries.Distinct().Count() + ":Cleanup=" + model.Cleanups +
                        ":FinalDispose=" + model.FinalDisposals + ":Publications=" + model.Publications)));
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
            private readonly NavigationActivationTests _fixture;
            internal Bridge(NavigationActivationTests fixture)
            {
                _fixture = fixture;
            }
            public T ResolveInstance<T>() where T : class => (T)ResolveInstance(typeof(T));
            public object ResolveInstance(Type type)
            {
                if (type == typeof(INavigationService))
                {
                    return _fixture._navigation;
                }
                Model model = type == typeof(A) ? new A(_fixture) : type == typeof(B) ? new B(_fixture) : new C(_fixture);
                _fixture._models.Add(model);
                _fixture._created?.Invoke(model);
                return model;
            }
        }
        private abstract class Model : BaseViewModel
        {
            private readonly NavigationActivationTests _fixture;
            internal readonly string Name;
            internal readonly List<NavigationActivation> Entries = new();
            internal Func<Model, NavigationActivation, UniTask> OnTo;
            internal Func<Model, UniTask> OnAway;
            internal int ToCount;
            internal int AwayCount;
            internal int Cleanups;
            internal int FinalDisposals;
            internal int Publications;
            internal NavigationActivation Current => Activation;
            protected Model(string name, NavigationActivationTests fixture)
            {
                Name = name;
                _fixture = fixture;
                Disposables.Add(R3.Disposable.Create(() => FinalDisposals++));
            }
            internal UniTask ScopedNavigate(string name) => Navigation.Navigate(Id(name), Main, Latest);
            internal UniTask ScopedClear() => Navigation.Clear(Main, Latest);
            protected override async UniTask NavigateTo(NavigationParameters parameters)
            {
                ToCount++;
                _fixture._trace.Add(Name + ":To");
                NavigationActivation lifetime = Activation;
                Assert.That(lifetime, Is.Not.Null);
                if (!Entries.Contains(lifetime))
                {
                    lifetime.Own(new Resource(() =>
                    {
                        Cleanups++;
                        _fixture._trace.Add(Name + ":cleanup");
                    }));
                }
                Entries.Add(lifetime);
                if (OnTo != null)
                {
                    await OnTo(this, lifetime);
                }
                _fixture._trace.Add(Name + ":To.return");
            }
            protected override async UniTask NavigateAway()
            {
                AwayCount++;
                _fixture._trace.Add(Name + ":Away");
                Assert.That(Activation, Is.Null, "Cleanup precedes Away.");
                if (OnAway != null)
                {
                    await OnAway(this);
                }
            }
        }
        private sealed class A : Model
        {
            internal A(NavigationActivationTests fixture) : base("A", fixture)
            {
            }
        }
        private sealed class B : Model
        {
            internal B(NavigationActivationTests fixture) : base("B", fixture)
            {
            }
        }
        private sealed class C : Model
        {
            internal C(NavigationActivationTests fixture) : base("C", fixture)
            {
            }
        }
        private sealed class Resource : IDisposable
        {
            private readonly Action _dispose;
            internal Resource(Action dispose)
            {
                _dispose = dispose;
            }
            public void Dispose() => _dispose();
        }
        private sealed class CustomNavigation : INavigationService
        {
            internal int Calls;
            public UniTask NavigateTo(IRegionManager manager, ViewId id, RegionName region, NavigationParameters parameters)
            {
                Calls++;
                return UniTask.CompletedTask;
            }
            public UniTask Clear(IRegionManager manager, RegionName region)
            {
                Calls++;
                return UniTask.CompletedTask;
            }
        }
        public sealed class Window : EditorWindow
        {
        }
    }
}
