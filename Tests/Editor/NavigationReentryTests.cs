using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sim.Utility.PropertyModel;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Sim.Faciem.Tests
{
    [TestFixture]
    public sealed class NavigationReentryTests
    {
        private const NavigationEntryMode Restart = NavigationEntryMode.ReenterIfActive;
        private const NavigationPolicy Latest = NavigationPolicy.LatestRequestWins;
        private static readonly RegionName Main = RegionName.From("reentry/main");
        private static readonly RegionName Other = RegionName.From("reentry/other");
        private static readonly IPropertyKey<string> Input = PropertyKey.From<string>("Reentry/TestInput");
        private readonly List<Model> _models = new();
        private readonly List<ViewIdAsset> _assets = new();
        private readonly List<UniTaskCompletionSource> _gates = new();
        private readonly List<UniTask> _tasks = new();
        private readonly List<string> _trace = new();
        private readonly List<Region> _regions = new();
        private TestWindow _window;
        private EditorWindow _previous;
        private Region _main;
        private Region _other;
        private RegionManager _manager;
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
            _regions.Clear();
            _created = null;
            _previous = EditorWindow.focusedWindow;
            _window = ScriptableObject.CreateInstance<TestWindow>();
            _window.titleContent = new GUIContent("Faciem explicit re-entry tests");
            _window.Show();
            _main = AddRegion(Main);
            _other = AddRegion(Other);
            _manager = Manager(_main, _other);
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

        [TestCase(false)]
        [TestCase(true)]
        public void Ordinary_same_ViewId_preserves_activation_even_with_different_parameters(bool latest)
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            VisualElement view = View(_main, "A");
            NavigationActivation lifetime = a.Current;
            UniTask ordinary = latest
                ? _navigation.NavigateTo(_manager, Id("A"), Main, Parameters("different"), Latest)
                : _navigation.NavigateTo(_manager, Id("A"), Main, Parameters("different"));
            Finish(Track(ordinary));
            Assert.That(Current(_main, "A"), Is.SameAs(a));
            Assert.That(View(_main, "A"), Is.SameAs(view));
            Assert.That(a.Current, Is.SameAs(lifetime));
            Assert.That(a.ToCount, Is.EqualTo(2));
            Assert.That(a.AwayCount, Is.Zero);
            Assert.That(a.Cleanups, Is.Zero);
            Assert.That(a.LastInput, Is.EqualTo("different"));
            AssertSingle(_main, "A");
        }

        [TestCase(NavigationPolicy.Fifo)]
        [TestCase(NavigationPolicy.LatestRequestWins)]
        public void Explicit_reentry_reuses_cached_objects_with_fresh_activation_and_exact_order(NavigationPolicy policy)
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            VisualElement view = View(_main, "A");
            NavigationActivation first = a.Current;
            a.Transient = 99;
            Finish(Request("A", "second", Restart, policy));
            Assert.That(_trace, Is.EqualTo(new[]
            {
                "A:To:first:active/Flex", "A:To.return:first",
                "A:cleanup", "A:Away:inactive/None:null", "A:Away.return",
                "A:To:second:active/Flex", "A:To.return:second"
            }));
            Assert.That(View(_main, "A"), Is.SameAs(view));
            Assert.That(Current(_main, "A"), Is.SameAs(a));
            Assert.That(a.Current, Is.Not.SameAs(first));
            Assert.That(first.IsValid, Is.False);
            Assert.That(first.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(a.ToCount, Is.EqualTo(2));
            Assert.That(a.AwayCount, Is.EqualTo(1));
            Assert.That(a.Transient, Is.EqualTo(1));
            Assert.That(a.Disposals, Is.Zero);
            Assert.That(_models, Has.Count.EqualTo(1));
            Assert.That(_main.childCount, Is.EqualTo(1));
            AssertSingle(_main, "A");
            Finish(Clear());
            Finish(Clear());
            Assert.That(a.Cleanups, Is.EqualTo(2));
        }

        [Test]
        public void Captured_pre_reentry_work_cannot_publish_to_the_new_activation()
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            NavigationActivation first = a.Current;
            UniTaskCompletionSource gate = Gate();
            bool ran = false;
            bool published = true;
            UniTask work = Track(CompleteLate());
            async UniTask CompleteLate()
            {
                await gate.Task;
                ran = true;
                published = first.TryPublish(() => a.Publications++);
            }
            Finish(Request("A", "second", Restart));
            gate.TrySetResult();
            Finish(work);
            Assert.That(ran, Is.True);
            Assert.That(published, Is.False);
            Assert.That(a.Publications, Is.Zero);
            Assert.That(a.Current.TryPublish(() => a.Publications++), Is.True);
            Assert.That(a.Publications, Is.EqualTo(1));
        }

        [Test]
        public void Inactive_target_and_queued_state_changes_follow_ordinary_entry_without_extra_exit()
        {
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = (text, _) => text == "first" ? gate.Task : UniTask.CompletedTask;
            UniTask first = Request("A", "first");
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            UniTask b = Request("B", "between");
            UniTask restart = Request("A", "return", Restart);
            gate.TrySetResult();
            Finish(first);
            Finish(b);
            Finish(restart);
            Assert.That(a.AwayCount, Is.EqualTo(1), "A was inactive by execution time; no synthetic second Away.");
            Assert.That(a.ToCount, Is.EqualTo(2));
            Assert.That(a.Current, Is.Not.SameAs(old));
            Assert.That(Current(_main, "B").AwayCount, Is.EqualTo(1));
            AssertSingle(_main, "A");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Reentry_and_Clear_respect_FIFO_order(bool reentryFirst)
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            UniTaskCompletionSource gate = Gate();
            a.OnAway = () => gate.Task;
            UniTask first = reentryFirst ? Request("A", "restart", Restart) : Clear();
            UniTask second = reentryFirst ? Clear() : Request("A", "restart", Restart);
            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_main.ActiveViews, Is.Empty);
            gate.TrySetResult();
            Finish(first);
            Finish(second);
            Assert.That(a.ToCount, Is.EqualTo(2));
            Assert.That(a.AwayCount, Is.EqualTo(reentryFirst ? 2 : 1));
            Assert.That(a.Cleanups, Is.EqualTo(reentryFirst ? 2 : 1));
            if (reentryFirst)
            {
                Assert.That(_main.ActiveViews, Is.Empty);
            }
            else
            {
                AssertSingle(_main, "A");
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Reentry_and_another_View_respect_FIFO_order(bool reentryFirst)
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            UniTaskCompletionSource gate = Gate();
            a.OnAway = () => gate.Task;
            UniTask first = reentryFirst ? Request("A", "restart", Restart) : Request("B", "other");
            UniTask second = reentryFirst ? Request("B", "other") : Request("A", "restart", Restart);
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));
            gate.TrySetResult();
            Finish(first);
            Finish(second);
            Assert.That(a.ToCount, Is.EqualTo(2));
            Assert.That(a.AwayCount, Is.EqualTo(reentryFirst ? 2 : 1));
            AssertSingle(_main, reentryFirst ? "B" : "A");
        }

        [TestCase("A")]
        [TestCase("B")]
        [TestCase("Clear")]
        public void Superseded_queued_reentry_cannot_reset_current_activation(string winner)
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            UniTaskCompletionSource gate = Gate();
            a.OnTo = (text, _) => text == "blocker" ? gate.Task : UniTask.CompletedTask;
            UniTask blocker = Request("A", "blocker");
            UniTask restart = Request("A", "must-not-enter", Restart, Latest);
            UniTask newer = winner == "Clear" ? Clear(Latest) : Request(winner, "winner", NavigationEntryMode.Default, Latest);
            Assert.That(a.Current, Is.SameAs(old));
            Assert.That(old.IsValid, Is.True);
            Assert.That(a.Cleanups, Is.Zero);
            gate.TrySetResult();
            Finish(blocker);
            Cancelled(restart);
            Finish(newer);
            Assert.That(a.Inputs, Does.Not.Contain("must-not-enter"));
            Assert.That(a.AwayCount, Is.EqualTo(winner == "A" ? 0 : 1));
            if (winner == "A")
            {
                Assert.That(a.Current, Is.SameAs(old));
                Assert.That(a.Cleanups, Is.Zero);
            }
            else
            {
                Assert.That(a.Cleanups, Is.EqualTo(1));
            }
        }

        [TestCase("A")]
        [TestCase("B")]
        [TestCase("Clear")]
        public void Superseded_executing_exit_never_creates_its_new_activation(string winner)
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            UniTaskCompletionSource gate = Gate();
            a.OnAway = () => gate.Task;
            UniTask restart = Request("A", "must-not-enter", Restart, Latest);
            UniTask newer = winner == "Clear" ? Clear(Latest) : Request(winner, "winner", NavigationEntryMode.Default, Latest);
            Assert.That(old.IsValid, Is.False, "Started exit cannot be undone.");
            Assert.That(a.Current, Is.Null);
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(newer.Status, Is.EqualTo(UniTaskStatus.Pending));
            gate.TrySetResult();
            Cancelled(restart);
            Finish(newer);
            Assert.That(a.Inputs, Does.Not.Contain("must-not-enter"));
            Assert.That(a.AwayCount, Is.EqualTo(1));
            if (winner == "A")
            {
                Assert.That(a.Entries.Distinct().Count(), Is.EqualTo(2), "Only the winning request creates activation 2.");
                Assert.That(a.Current.IsValid, Is.True);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Superseded_reentry_entry_drains_without_ending_the_winners_activation(bool cooperative)
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            VisualElement view = View(_main, "A");
            UniTaskCompletionSource gate = Gate();
            bool stalePublication = false;
            a.OnTo = async (text, lifetime) =>
            {
                if (text == "restart")
                {
                    if (cooperative)
                    {
                        await gate.Task.AttachExternalCancellation(lifetime.CancellationToken);
                    }
                    else
                    {
                        await gate.Task;
                    }
                    stalePublication = lifetime.TryPublish(() => a.Publications++);
                }
            };
            UniTask restart = Request("A", "restart", Restart, Latest);
            NavigationActivation interrupted = a.Current;
            UniTask winner = Request("A", "winner", NavigationEntryMode.Default, Latest);
            Assert.That(interrupted.IsValid, Is.False);
            if (!cooperative)
            {
                Assert.That(winner.Status, Is.EqualTo(UniTaskStatus.Pending));
                Assert.That(a.Cleanups, Is.EqualTo(1));
            }
            gate.TrySetResult();
            Cancelled(restart);
            Finish(winner);
            Assert.That(a.Current, Is.Not.SameAs(interrupted));
            Assert.That(a.Current.IsValid, Is.True);
            Assert.That(a.Current.TryPublish(() => a.Publications++), Is.True);
            Assert.That(stalePublication, Is.False);
            Assert.That(a.Cleanups, Is.EqualTo(2));
            Assert.That(a.Entries.Distinct().Count(), Is.EqualTo(3));
            Assert.That(View(_main, "A"), Is.SameAs(view));
            AssertSingle(_main, "A");
        }

        [TestCase("Away")]
        [TestCase("To")]
        [TestCase("Cleanup")]
        public void Reentry_failures_end_owned_resources_and_allow_queue_recovery(string failure)
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            UniTaskCompletionSource gate = Gate();
            if (failure == "Away")
            {
                a.OnAway = async () =>
                {
                    await gate.Task;
                    throw new InvalidOperationException("controlled Away");
                };
            }
            else if (failure == "To")
            {
                a.OnTo = async (text, _) =>
                {
                    if (text == "restart")
                    {
                        await gate.Task;
                        throw new InvalidOperationException("controlled To");
                    }
                };
            }
            else
            {
                a.Current.Own(new Resource(() => throw new InvalidOperationException("controlled cleanup")));
            }
            UniTask restart = Request("A", "restart", Restart);
            UniTask b = Request("B", "recovery");
            gate.TrySetResult();
            if (failure == "Cleanup")
            {
                Assert.Throws<AggregateException>(() => Finish(restart));
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => Finish(restart));
            }
            Finish(b);
            Assert.That(a.Cleanups, Is.EqualTo(failure == "To" ? 2 : 1));
            Assert.That(a.Entries.All(lifetime => !lifetime.IsValid), Is.True);
            Assert.That(a.Disposals, Is.Zero);
            AssertSingle(_main, "B");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Scoped_lifecycle_reentry_is_rejected_before_superseding_legitimate_work(bool latest)
        {
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = async (text, lifetime) =>
            {
                if (text == "first")
                {
                    await gate.Task;
                    Assert.Throws<InvalidOperationException>(() => Finish(model.ScopedRestart(latest ? Latest : NavigationPolicy.Fifo)));
                    Assert.That(lifetime.IsValid, Is.True);
                }
            };
            UniTask a = Request("A", "first");
            UniTask b = Request("B", "legitimate", NavigationEntryMode.Default, Latest);
            gate.TrySetResult();
            Finish(a);
            Finish(b);
            Assert.That(Current(_main, "A").AwayCount, Is.EqualTo(1), "Only the legitimate B request exits A.");
            AssertSingle(_main, "B");
        }

        [Test]
        public void Scoped_handle_outside_a_callback_can_request_reentry()
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            Finish(Track(a.ScopedRestart(NavigationPolicy.Fifo)));
            Assert.That(a.Current, Is.Not.SameAs(old));
            Assert.That(a.AwayCount, Is.EqualTo(1));
            Assert.That(a.ToCount, Is.EqualTo(2));
        }

        [Test]
        public void Independent_regions_reenter_concurrently_without_cross_cancellation()
        {
            Finish(Request("A", "first"));
            Finish(Request("B", "other-first", manager: _manager, region: Other));
            Model a = Current(_main, "A");
            Model b = Current(_other, "B");
            NavigationActivation oldB = b.Current;
            UniTaskCompletionSource gate = Gate();
            a.OnAway = () => gate.Task;
            UniTask restartA = Request("A", "restart", Restart, Latest);
            Finish(Request("B", "other-restart", Restart, Latest, _manager, Other));
            Assert.That(restartA.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(b.Current, Is.Not.SameAs(oldB));
            Assert.That(b.Current.IsValid, Is.True);
            gate.TrySetResult();
            Finish(restartA);
            AssertSingle(_main, "A");
            AssertSingle(_other, "B");
        }

        [Test]
        public void Whole_shared_cohort_reentry_exits_once_and_preserves_each_cached_tree()
        {
            Region second = AddRegion(Main);
            RegionManager group = Manager(_main, second);
            Finish(Request("A", "first", manager: group));
            Model a = Current(_main, "A");
            VisualElement firstView = View(_main, "A");
            VisualElement secondView = View(second, "A");
            NavigationActivation old = a.Current;
            a.OnAway = () =>
            {
                Assert.That(_main.ActiveViews, Is.Empty);
                Assert.That(second.ActiveViews, Is.Empty);
                Assert.That(a.Current, Is.Null);
                return UniTask.CompletedTask;
            };
            Finish(Request("A", "restart", Restart, manager: group));
            Assert.That(a.AwayCount, Is.EqualTo(1), "One exit per logical shared model, not one reset per tree.");
            Assert.That(a.ToCount, Is.EqualTo(2));
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(Current(second, "A"), Is.SameAs(a));
            Assert.That(View(_main, "A"), Is.SameAs(firstView));
            Assert.That(View(second, "A"), Is.SameAs(secondView));
            Assert.That(a.Current, Is.Not.SameAs(old));
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
        }

        [Test]
        public void Partial_shared_cohort_reentry_rejects_without_resetting_any_member()
        {
            Region second = AddRegion(Main);
            RegionManager group = Manager(_main, second);
            Finish(Request("A", "first", manager: group));
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            // Change Parent through an ordinary scoped-size request; ownership must not depend on that Parent.
            Finish(Request("A", "ordinary"));
            Assert.Throws<InvalidOperationException>(() => Finish(Request("A", "bad-partial", Restart)));
            Assert.That(a.Current, Is.SameAs(old));
            Assert.That(old.IsValid, Is.True);
            Assert.That(a.Cleanups, Is.Zero);
            Assert.That(a.AwayCount, Is.Zero);
            Assert.That(a.Inputs, Does.Not.Contain("bad-partial"));
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
            Finish(Request("A", "full-recovery", Restart, manager: group));
            Assert.That(a.Current, Is.Not.SameAs(old));
        }

        [Test]
        public void Heterogeneous_cached_models_reject_group_reentry_before_mutation()
        {
            Region second = AddRegion(Main);
            Finish(Request("A", "first"));
            Finish(Request("A", "independent", manager: Manager(second)));
            Model first = Current(_main, "A");
            Model other = Current(second, "A");
            NavigationActivation firstLifetime = first.Current;
            NavigationActivation otherLifetime = other.Current;
            Assert.That(first, Is.Not.SameAs(other));
            Assert.Throws<InvalidOperationException>(() => Finish(Request("A", "invalid-group", Restart,
                manager: Manager(_main, second))));
            Assert.That(first.Current, Is.SameAs(firstLifetime));
            Assert.That(other.Current, Is.SameAs(otherLifetime));
            Assert.That(firstLifetime.IsValid && otherLifetime.IsValid, Is.True);
            Assert.That(first.Cleanups + other.Cleanups, Is.Zero);
            Assert.That(first.AwayCount + other.AwayCount, Is.Zero);
            Finish(Request("A", "valid-first", Restart));
            Finish(Request("A", "valid-other", Restart, manager: Manager(second)));
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
        }

        [Test]
        public void Shared_model_busy_in_an_unowned_region_cannot_be_reset()
        {
            Region second = AddRegion(Main);
            Finish(Request("A", "first", manager: Manager(_main, second)));
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            UniTaskCompletionSource gate = Gate();
            a.OnTo = (text, _) => text == "busy" ? gate.Task : UniTask.CompletedTask;
            UniTask busy = Request("A", "busy", manager: Manager(second));
            Assert.Throws<InvalidOperationException>(() => Finish(Request("A", "unsafe", Restart)));
            Assert.That(old.IsValid, Is.True);
            Assert.That(a.Cleanups, Is.Zero);
            gate.TrySetResult();
            Finish(busy);
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
        }

        [Test]
        public void Superseded_queued_group_reentry_does_not_partially_reset_the_cohort()
        {
            Region second = AddRegion(Main);
            RegionManager group = Manager(_main, second);
            Finish(Request("A", "first", manager: group));
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            UniTaskCompletionSource gate = Gate();
            a.OnTo = (text, _) => text == "blocker" ? gate.Task : UniTask.CompletedTask;
            UniTask blocker = Request("A", "blocker", manager: group);
            UniTask restart = Request("A", "obsolete", Restart, Latest, group);
            UniTask winner = Request("A", "winner", NavigationEntryMode.Default, Latest);
            gate.TrySetResult();
            Finish(blocker);
            Cancelled(restart);
            Finish(winner);
            Assert.That(a.Current, Is.SameAs(old));
            Assert.That(a.Cleanups, Is.Zero);
            Assert.That(a.AwayCount, Is.Zero);
            Assert.That(a.Inputs, Does.Not.Contain("obsolete"));
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Executing_group_reentry_supersession_drains_before_only_the_winner_activates(bool duringEntry)
        {
            Region second = AddRegion(Main);
            RegionManager group = Manager(_main, second);
            Finish(Request("A", "first", manager: group));
            Model a = Current(_main, "A");
            VisualElement firstView = View(_main, "A");
            VisualElement secondView = View(second, "A");
            UniTaskCompletionSource gate = Gate();
            if (duringEntry)
            {
                a.OnTo = (text, _) => text == "restart" ? gate.Task : UniTask.CompletedTask;
            }
            else
            {
                a.OnAway = () => gate.Task;
            }
            UniTask restart = Request("A", "restart", Restart, Latest, group);
            UniTask winner = Request("A", "winner", NavigationEntryMode.Default, Latest);
            Assert.That(winner.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(a.Cleanups, Is.EqualTo(1));
            gate.TrySetResult();
            Cancelled(restart);
            Finish(winner);
            Assert.That(a.Cleanups, Is.EqualTo(duringEntry ? 2 : 1));
            Assert.That(a.Entries.Distinct().Count(), Is.EqualTo(duringEntry ? 3 : 2));
            Assert.That(a.AwayCount, Is.EqualTo(1));
            Assert.That(View(_main, "A"), Is.SameAs(firstView));
            Assert.That(View(second, "A"), Is.SameAs(secondView));
            Assert.That(second.ActiveViews, Is.Empty);
            Assert.That(View(second, "A").style.display.value, Is.EqualTo(DisplayStyle.None));
            AssertSingle(_main, "A");
        }

        [Test]
        public void Mixed_group_exits_each_old_model_before_restoring_the_shared_requested_cache()
        {
            Region second = AddRegion(Main);
            RegionManager group = Manager(_main, second);
            Finish(Request("A", "first", manager: group));
            Model a = Current(_main, "A");
            VisualElement secondA = View(second, "A");
            Finish(Request("B", "mixed", manager: Manager(second)));
            Model b = Current(second, "B");
            int previousAwayA = a.AwayCount;
            b.OnAway = () =>
            {
                Assert.That(_main.ActiveViews, Is.Empty, "No early target show while a group exit is pending.");
                Assert.That(second.ActiveViews, Is.Empty);
                return UniTask.CompletedTask;
            };
            Finish(Request("A", "restart", Restart, manager: group));
            Assert.That(a.AwayCount, Is.EqualTo(previousAwayA + 1));
            Assert.That(b.AwayCount, Is.EqualTo(1));
            Assert.That(b.Cleanups, Is.EqualTo(1));
            Assert.That(a.Cleanups, Is.EqualTo(1), "The previously ended cohort is not cleaned twice.");
            Assert.That(Current(second, "A"), Is.SameAs(a));
            Assert.That(View(second, "A"), Is.SameAs(secondA));
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
        }

        [Test]
        public void Entry_mode_validation_and_custom_service_compatibility_are_explicit()
        {
            var custom = new CustomNavigation();
            Finish(custom.NavigateTo(_manager, Id("A"), Main, Parameters("default"), default));
            Finish(custom.NavigateTo(_manager, Id("A"), Main, Parameters("default"), NavigationPolicy.Fifo, NavigationEntryMode.Default));
            Assert.That(custom.Calls, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => custom.NavigateTo(_manager, Id("A"), Main, Parameters("restart"), NavigationPolicy.Fifo, Restart));
            Assert.Throws<ArgumentOutOfRangeException>(() => _navigation.NavigateTo(_manager, Id("A"), Main,
                Parameters("invalid"), NavigationPolicy.Fifo, (NavigationEntryMode)99));
            Assert.That(custom.Calls, Is.EqualTo(2));
            Assert.That(_models, Is.Empty);
            Assert.That(_main.ActiveViews, Is.Empty);
        }

        [Test]
        public void Multi_view_region_reentry_is_not_silently_treated_as_success()
        {
            Finish(Request("A", "first"));
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            _main.SupportMultipleViews = true;
            Assert.Throws<NotSupportedException>(() => Finish(Request("A", "unsupported", Restart)));
            Assert.That(a.Current, Is.SameAs(old));
            Assert.That(old.IsValid, Is.True);
            Assert.That(a.Cleanups, Is.Zero);
            Assert.That(a.AwayCount, Is.Zero);
            _main.SupportMultipleViews = false;
        }

        private static NavigationParameters Parameters(string input) => NavigationParameters.Empty.Add(Input, input);
        private static ViewId Id(string name) => ViewId.From("reentry/" + name);
        private Region AddRegion(RegionName name)
        {
            var region = new Region(name);
            _regions.Add(region);
            _window.rootVisualElement.Add(region);
            return region;
        }
        private static RegionManager Manager(params Region[] regions)
        {
            var manager = new RegionManager();
            foreach (Region region in regions)
            {
                manager.AddRegion(region);
            }
            return manager;
        }
        private UniTaskCompletionSource Gate()
        {
            var gate = new UniTaskCompletionSource();
            _gates.Add(gate);
            return gate;
        }
        private UniTask Request(string name, string input, NavigationEntryMode entryMode = NavigationEntryMode.Default,
            NavigationPolicy policy = NavigationPolicy.Fifo, RegionManager manager = null, RegionName? region = null)
        {
            return Track(_navigation.NavigateTo(manager ?? _manager, Id(name), region ?? Main, Parameters(input), policy, entryMode));
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
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending), "Never block Unity's main thread.");
            task.GetAwaiter().GetResult();
        }
        private static void Cancelled(UniTask task)
        {
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.Throws<OperationCanceledException>(() => Finish(task));
        }
        private static VisualElement View(Region region, string name)
        {
            Assert.That(region.TryGetView(Id(name), out VisualElement view), Is.True);
            return view;
        }
        private static Model Current(Region region, string name) => (Model)View(region, name).dataSource;
        private static void AssertSingle(Region region, string name)
        {
            Assert.That(region.ActiveViews, Is.EqualTo(new[] { Id(name) }));
            Assert.That(region.Children().Count(view => view.style.display.value != DisplayStyle.None), Is.EqualTo(1));
            Assert.That(View(region, name).panel, Is.Not.Null);
            Assert.That(Current(region, name).Current.IsValid, Is.True);
        }
        private string Visibility(Model model)
        {
            return string.Join(",", _regions.Where(region => region.TryGetView(Id(model.Name), out VisualElement view) &&
                ReferenceEquals(view.dataSource, model)).Select(region =>
                    (region.ActiveViews.Contains(Id(model.Name)) ? "active/" : "inactive/") + View(region, model.Name).style.display.value));
        }

        [TearDown]
        public void TearDown()
        {
            int primaryTraceCount = _trace.Count;
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
                    foreach (Region region in _regions)
                    {
                        Finish(Track(_navigation.Clear(Manager(region), ((IRegion)region).RegionName)));
                    }
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
                Directory.CreateDirectory("Logs/navigation-phase3");
                string file = Regex.Replace(TestContext.CurrentContext.Test.Name, "[^a-zA-Z0-9._-]", "_");
                File.WriteAllText("Logs/navigation-phase3/" + file + ".txt", "test=" + TestContext.CurrentContext.Test.Name +
                    "\noutcome=" + TestContext.CurrentContext.Result.Outcome.Status + "\nprimaryTraceCount=" + primaryTraceCount +
                    "\n" + string.Join("\n", _trace) + "\n" + string.Join("\n", _models.Select(model =>
                        model.Name + ":model=" + RuntimeHelpers.GetHashCode(model) + ":To=" + model.ToCount +
                        ":Away=" + model.AwayCount + ":Lifetimes=" + model.Entries.Distinct().Count() +
                        ":Cleanup=" + model.Cleanups + ":FinalDispose=" + model.Disposals +
                        ":ViewHashes=" + string.Join(",", model.Views.Select(view => RuntimeHelpers.GetHashCode(view))))));
                foreach (ViewIdAsset asset in _assets)
                {
                    Object.DestroyImmediate(asset);
                }
                if (_previous != null)
                {
                    _previous.Focus();
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
            private readonly NavigationReentryTests _fixture;
            internal Bridge(NavigationReentryTests fixture)
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
            private readonly NavigationReentryTests _fixture;
            internal readonly string Name;
            internal readonly List<NavigationActivation> Entries = new();
            internal readonly List<string> Inputs = new();
            internal readonly HashSet<VisualElement> Views = new();
            internal Func<string, NavigationActivation, UniTask> OnTo;
            internal Func<UniTask> OnAway;
            internal int ToCount;
            internal int AwayCount;
            internal int Cleanups;
            internal int Disposals;
            internal int Publications;
            internal int Transient;
            internal string LastInput;
            internal NavigationActivation Current => Activation;
            protected Model(string name, NavigationReentryTests fixture)
            {
                Name = name;
                _fixture = fixture;
                Disposables.Add(R3.Disposable.Create(() => Disposals++));
            }
            internal UniTask ScopedRestart(NavigationPolicy policy) =>
                Navigation.Navigate(Id(Name), Main, Parameters("scoped"), policy, Restart);
            protected override async UniTask NavigateTo(NavigationParameters parameters)
            {
                ToCount++;
                LastInput = parameters.Get(Input).HasValue ? parameters.Get(Input).Value : "empty";
                Inputs.Add(LastInput);
                Transient = 1;
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
                foreach (Region region in _fixture._regions)
                {
                    if (region.TryGetView(Id(Name), out VisualElement view) && ReferenceEquals(view.dataSource, this))
                    {
                        Views.Add(view);
                    }
                }
                string input = LastInput;
                _fixture._trace.Add(Name + ":To:" + input + ":" + _fixture.Visibility(this));
                if (OnTo != null)
                {
                    await OnTo(input, lifetime);
                }
                _fixture._trace.Add(Name + ":To.return:" + input);
            }
            protected override async UniTask NavigateAway()
            {
                AwayCount++;
                LastInput = string.Empty;
                Transient = 0;
                _fixture._trace.Add(Name + ":Away:" + _fixture.Visibility(this) + ":" + (Activation == null ? "null" : "non-null"));
                if (OnAway != null)
                {
                    await OnAway();
                }
                _fixture._trace.Add(Name + ":Away.return");
            }
        }
        private sealed class A : Model
        {
            internal A(NavigationReentryTests fixture) : base("A", fixture)
            {
            }
        }
        private sealed class B : Model
        {
            internal B(NavigationReentryTests fixture) : base("B", fixture)
            {
            }
        }
        private sealed class C : Model
        {
            internal C(NavigationReentryTests fixture) : base("C", fixture)
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
            public UniTask Clear(IRegionManager manager, RegionName region) => UniTask.CompletedTask;
        }
        public sealed class TestWindow : EditorWindow
        {
        }
    }
}
