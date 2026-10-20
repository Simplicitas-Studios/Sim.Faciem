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
    public sealed class NavigationOutcomeTests
    {
        private static readonly RegionName Main = RegionName.From("outcome/main");
        private static readonly RegionName Other = RegionName.From("outcome/other");
        private static readonly IPropertyKey<string> Input = PropertyKey.From<string>("Outcome/Input");
        private readonly List<Model> _models = new();
        private readonly List<ViewIdAsset> _assets = new();
        private readonly List<Region> _regions = new();
        private readonly List<UniTaskCompletionSource> _gates = new();
        private readonly List<UniTask<NavigationResult>> _tasks = new();
        private readonly List<string> _trace = new();
        private TestWindow _window;
        private EditorWindow _previous;
        private Region _main;
        private Region _other;
        private RegionManager _manager;
        private INavigationService _navigation;
        private FaultNavigation _native;
        private Action<Model> _created;
        private Action<Type> _construct;
        private string _prepareFault;
        private Exception _fault;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _models.Clear();
            _assets.Clear();
            _regions.Clear();
            _gates.Clear();
            _tasks.Clear();
            _trace.Clear();
            _created = null;
            _construct = null;
            _prepareFault = null;
            _fault = new InvalidOperationException("injected primary failure");
            _previous = EditorWindow.focusedWindow;
            _window = ScriptableObject.CreateInstance<TestWindow>();
            _window.titleContent = new GUIContent("Faciem outcome/recovery tests");
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
            _native = new FaultNavigation(this, new ViewModelConstructionService(new Bridge(this)), new Registry(_assets));
            _navigation = _native;
            for (int frame = 0; frame < 60 && _main.panel == null; frame++)
            {
                yield return null;
            }
            Assert.That(_main.panel, Is.Not.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Missing_lookup_is_explicit_and_does_not_dispatch(bool missingRegion)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            NavigationResult result = Done(Track(_navigation.NavigateToResult(_manager,
                missingRegion ? Id("B") : Id("unknown"), missingRegion ? RegionName.From("absent") : Main, Parameters("missing"))));
            Assert.That(result.Status, Is.EqualTo(missingRegion ? NavigationStatus.MissingRegion : NavigationStatus.MissingViewId));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Null);
            Assert.That(a.Current, Is.SameAs(lifetime));
            Assert.That(a.AwayCount, Is.Zero);
            Assert.That(_models, Has.Count.EqualTo(1));
            AssertSingle(_main, "A");
            if (missingRegion)
            {
                Require(Track(_navigation.ClearResult(_manager, RegionName.From("absent"))), NavigationStatus.MissingRegion);
            }
        }

        [Test]
        public void Empty_Clear_is_NoOp_only_after_its_FIFO_predecessor_drains()
        {
            Require(Clear(), NavigationStatus.NoOp);
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = (_, _) => gate.Task;
            UniTask<NavigationResult> entry = Request("A");
            UniTask<NavigationResult> clear = Clear();
            Assert.That(clear.Status, Is.EqualTo(UniTaskStatus.Pending));
            gate.TrySetResult();
            Require(entry, NavigationStatus.Succeeded);
            Require(clear, NavigationStatus.Succeeded);
            Require(Clear(), NavigationStatus.NoOp);
            AssertBlank(_main);
        }

        [TestCase("policy")]
        [TestCase("entryMode")]
        [TestCase("manager")]
        [TestCase("id")]
        [TestCase("parameters")]
        [TestCase("template")]
        [TestCase("dataContext")]
        public void Invalid_configuration_preserves_the_current_View_and_activation(string fault)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            if (fault == "template")
            {
                _assets[1].View = null;
            }
            if (fault == "dataContext")
            {
                _assets[1].DataContext = new MonoScriptReference { TypeName = "Missing.Type, Missing.Assembly" };
            }
            NavigationResult result = Done(Track(_navigation.NavigateToResult(fault == "manager" ? null : _manager,
                fault == "id" ? null : Id("B"), Main, fault == "parameters" ? null : Parameters("invalid"),
                fault == "policy" ? (NavigationPolicy)99 : NavigationPolicy.Fifo,
                fault == "entryMode" ? (NavigationEntryMode)99 : NavigationEntryMode.Default)));
            Failure(result, NavigationStatus.InvalidConfiguration, NavigationStage.Validation, NavigationRecovery.Preserved);
            Assert.That(a.Current, Is.SameAs(lifetime));
            Assert.That(a.AwayCount, Is.Zero);
            Assert.That(_models, Has.Count.EqualTo(1));
            AssertSingle(_main, "A");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Multi_view_configuration_is_unsupported_not_success(bool clear)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            _main.SupportMultipleViews = true;
            NavigationResult result = Done(clear ? Clear() : Request("B"));
            Failure(result, NavigationStatus.UnsupportedOperation, NavigationStage.Validation, NavigationRecovery.Preserved);
            Assert.That(a.Current.IsValid, Is.True);
            Assert.That(a.Cleanups, Is.Zero);
            AssertSingle(_main, "A");
            _main.SupportMultipleViews = false;
        }

        [Test]
        public void Custom_services_remain_legacy_compatible_but_results_never_guess_success()
        {
            var custom = new CustomNavigation();
            INavigationService root = custom;
            Require(Track(root.NavigateToResult(_manager, Id("A"), Main, NavigationParameters.Empty)), NavigationStatus.UnsupportedOperation);
            Require(Track(root.ClearResult(_manager, Main)), NavigationStatus.UnsupportedOperation);
            var model = new A(this);
            model.Setup(new RegionManager(), root);
            IViewModelNavigationService scoped = new ViewModelNavigationService(model, root);
            Require(Track(scoped.NavigateResult(Id("A"), Main, NavigationParameters.Empty)), NavigationStatus.UnsupportedOperation);
            Require(Track(scoped.ClearResult(Main)), NavigationStatus.UnsupportedOperation);
            Assert.That(custom.Calls, Is.Zero);
            Finish(root.NavigateTo(_manager, Id("A"), Main, NavigationParameters.Empty));
            Finish(scoped.Navigate(Id("A"), Main));
            Assert.That(custom.Calls, Is.EqualTo(2));
            model.Dispose();
        }

        [Test]
        public void Result_and_legacy_calls_share_cached_trees_models_and_ordinary_same_ID_semantics()
        {
            Finish(_navigation.NavigateTo(_manager, Id("A"), Main, Parameters("legacy")));
            Model a = Current(_main, "A");
            VisualElement view = View(_main, "A");
            NavigationActivation first = a.Current;
            Require(Request("A", "ordinary"), NavigationStatus.Succeeded);
            Assert.That(a.Current, Is.SameAs(first));
            Assert.That(a.ToCount, Is.EqualTo(2));
            Assert.That(a.AwayCount, Is.Zero);
            Require(Request("B"), NavigationStatus.Succeeded);
            Require(Request("A", "return"), NavigationStatus.Succeeded);
            Assert.That(Current(_main, "A"), Is.SameAs(a));
            Assert.That(View(_main, "A"), Is.SameAs(view));
            Assert.That(a.Current, Is.Not.SameAs(first));
            Assert.That(a.Disposals, Is.Zero);
            Assert.That(_main.childCount, Is.EqualTo(2));
            AssertSingle(_main, "A");
        }

        [Test]
        public void Construction_failure_preserves_UI_but_does_not_undo_constructor_side_effects()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            int externalEffect = 0;
            _construct = type =>
            {
                if (type == typeof(B))
                {
                    externalEffect++;
                    throw _fault;
                }
            };
            NavigationResult result = Done(Request("B"));
            Failure(result, NavigationStatus.Failed, NavigationStage.Construction, NavigationRecovery.Preserved);
            Assert.That(result.Error, Is.SameAs(_fault));
            Assert.That(externalEffect, Is.EqualTo(1));
            Assert.That(a.Current, Is.SameAs(lifetime));
            Assert.That(a.AwayCount, Is.Zero);
            AssertSingle(_main, "A");
            _construct = null;
            Require(Request("B"), NavigationStatus.Succeeded);
        }

        [TestCase("before-create")]
        [TestCase("after-detached-binding")]
        public void Injected_synchronous_preparation_fault_preserves_current_UI(string boundary)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            _prepareFault = boundary;
            NavigationResult result = Done(Request("B"));
            Failure(result, NavigationStatus.Failed, NavigationStage.ViewPreparation, NavigationRecovery.Preserved);
            Assert.That(result.Error, Is.SameAs(_fault));
            Assert.That(a.Current, Is.SameAs(lifetime));
            Assert.That(a.Cleanups, Is.Zero);
            Assert.That(_main.TryGetView(Id("B"), out _), Is.False);
            AssertSingle(_main, "A");
            _prepareFault = null;
            Require(Request("B"), NavigationStatus.Succeeded);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Commit_fault_before_or_after_real_mount_is_contained_including_unrecorded_visible_tree(bool afterMount)
        {
            var region = new FaultRegion(Main, _fault);
            _regions.Add(region);
            _window.rootVisualElement.Add(region);
            RegionManager manager = Manager(region);
            Require(Request("A", manager: manager), NavigationStatus.Succeeded);
            Model a = Current(region, "A");
            VisualElement old = View(region, "A");
            region.FailAdd = true;
            region.AfterMount = afterMount;
            NavigationResult result = Done(Request("B", manager: manager));
            Failure(result, NavigationStatus.Failed, NavigationStage.ViewCommit, NavigationRecovery.Contained);
            Assert.That(result.Error, Is.SameAs(_fault));
            Assert.That(a.Current, Is.Null);
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(View(region, "A"), Is.SameAs(old));
            Assert.That(region.TryGetView(Id("B"), out VisualElement failed), Is.EqualTo(afterMount));
            if (afterMount)
            {
                Assert.That(failed.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(failed.dataSource, Is.TypeOf<B>());
            }
            AssertBlank(region);
            region.FailAdd = false;
            Require(Request("B", manager: manager), NavigationStatus.Succeeded);
            if (afterMount)
            {
                Assert.That(View(region, "B"), Is.SameAs(failed));
            }
            AssertSingle(region, "B");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Exit_failure_does_not_resurrect_an_ended_activation_and_does_not_touch_independent_Region(bool clear)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Require(Request("C", region: Other), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            Model c = Current(_other, "C");
            NavigationActivation old = a.Current;
            NavigationActivation independent = c.Current;
            VisualElement view = View(_main, "A");
            a.OnAway = () => throw _fault;
            NavigationResult result = Done(clear ? Clear() : Request("B"));
            Failure(result, NavigationStatus.Failed, NavigationStage.Exit, NavigationRecovery.Contained);
            Assert.That(result.Error, Is.SameAs(_fault));
            Assert.That(old.IsValid, Is.False);
            Assert.That(a.Current, Is.Null);
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(a.AwayCount, Is.EqualTo(1));
            Assert.That(c.Current, Is.SameAs(independent));
            Assert.That(independent.IsValid, Is.True);
            AssertSingle(_other, "C");
            AssertBlank(_main);
            a.OnAway = null;
            Require(Request("A", "recovery"), NavigationStatus.Succeeded);
            Assert.That(Current(_main, "A"), Is.SameAs(a));
            Assert.That(View(_main, "A"), Is.SameAs(view));
            Assert.That(a.Current, Is.Not.SameAs(old));
            Assert.That(old.TryPublish(() => a.Publications++), Is.False);
            Assert.That(a.Disposals, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Entry_failure_is_contained_and_recovery_reuses_cache(bool reentry)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            NavigationActivation first = a.Current;
            string target = reentry ? "A" : "B";
            if (reentry)
            {
                a.OnTo = (_, _) => throw _fault;
            }
            else
            {
                _created = model => model.OnTo = (_, _) => throw _fault;
            }
            NavigationResult result = Done(Request(target, mode: reentry ? NavigationEntryMode.ReenterIfActive : NavigationEntryMode.Default));
            Failure(result, NavigationStatus.Failed, NavigationStage.Entry, NavigationRecovery.Contained);
            Model failed = Current(_main, target);
            VisualElement view = View(_main, target);
            NavigationActivation interrupted = failed.Entries.Last();
            Assert.That(first.IsValid, Is.False);
            Assert.That(interrupted.IsValid, Is.False);
            Assert.That(failed.Current, Is.Null);
            AssertBlank(_main);
            failed.OnTo = null;
            _created = null;
            Require(Request(target, "recovery"), NavigationStatus.Succeeded);
            Assert.That(Current(_main, target), Is.SameAs(failed));
            Assert.That(View(_main, target), Is.SameAs(view));
            Assert.That(failed.Current, Is.Not.SameAs(interrupted));
            Assert.That(failed.Disposals, Is.Zero);
            AssertSingle(_main, target);
        }

        [TestCase("clear")]
        [TestCase("navigate")]
        [TestCase("reentry")]
        public void Cleanup_failure_attempts_all_resources_and_reports_its_stage_without_replaying_Away(string operation)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            NavigationActivation old = a.Current;
            int attempted = 0;
            old.Own(new Resource(() => attempted++));
            old.Own(new Resource(() =>
            {
                attempted++;
                throw _fault;
            }));
            old.Own(new Resource(() => attempted++));
            NavigationResult result = Done(operation == "clear" ? Clear() :
                Request(operation == "reentry" ? "A" : "B", mode: operation == "reentry" ? NavigationEntryMode.ReenterIfActive : NavigationEntryMode.Default));
            Failure(result, NavigationStatus.Failed, NavigationStage.Cleanup, NavigationRecovery.Contained);
            Assert.That(Errors(result.Error), Does.Contain(_fault));
            Assert.That(attempted, Is.EqualTo(3));
            Assert.That(a.AwayCount, Is.Zero);
            Assert.That(old.IsValid, Is.False);
            Assert.That(a.Current, Is.Null);
            AssertBlank(_main);
            Require(Request("A", "recovery"), NavigationStatus.Succeeded);
            Assert.That(attempted, Is.EqualTo(3));
            Assert.That(a.Disposals, Is.Zero);
        }

        [Test]
        public void Entry_and_containment_cleanup_failures_are_both_retained_without_early_successor_release()
        {
            var cleanupFault = new ArgumentException("injected cleanup failure");
            _created = model => model.OnTo = (_, lifetime) =>
            {
                lifetime.Own(new Resource(() => throw cleanupFault));
                throw _fault;
            };
            NavigationResult result = Done(Request("A"));
            Failure(result, NavigationStatus.Failed, NavigationStage.Entry, NavigationRecovery.Incomplete);
            Assert.That(Errors(result.Error), Does.Contain(_fault));
            Assert.That(Errors(result.Error), Does.Contain(cleanupFault));
            AssertBlank(_main);
            Assert.That(Current(_main, "A").Current, Is.Null);
            _created = null;
            Require(Request("B"), NavigationStatus.Succeeded);
            AssertSingle(_main, "B");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void User_cancellation_is_not_misreported_as_supersession(bool exit)
        {
            var token = new System.Threading.CancellationToken(true);
            var cancellation = new OperationCanceledException("application cancellation, not LatestRequestWins", token);
            if (exit)
            {
                Require(Request("A"), NavigationStatus.Succeeded);
                Current(_main, "A").OnAway = () => throw cancellation;
            }
            else
            {
                _created = model => model.OnTo = (_, _) => throw cancellation;
            }
            NavigationResult result = Done(exit ? Clear() : Request("A"));
            Failure(result, NavigationStatus.Cancelled, exit ? NavigationStage.Exit : NavigationStage.Entry, NavigationRecovery.Contained);
            Assert.That(result.Error, Is.InstanceOf<OperationCanceledException>());
            Assert.That(((OperationCanceledException)result.Error).CancellationToken, Is.EqualTo(token));
            AssertBlank(_main);
        }

        [TestCase("deactivate")]
        [TestCase("foreign-source")]
        [TestCase("reparent")]
        public void Callback_breaking_postconditions_is_not_success_and_foreign_state_is_not_overwritten(string corruption)
        {
            object foreign = new object();
            _created = model => model.OnTo = (_, _) =>
            {
                if (corruption == "deactivate")
                {
                    _main.DeactivateView(Id("A"));
                }
                else if (corruption == "foreign-source")
                {
                    View(_main, "A").dataSource = foreign;
                }
                else
                {
                    _other.Add(View(_main, "A"));
                }
                return UniTask.CompletedTask;
            };
            NavigationResult result = Done(Request("A"));
            Failure(result, NavigationStatus.Failed, NavigationStage.Verification,
                corruption == "reparent" ? NavigationRecovery.Incomplete : NavigationRecovery.Contained);
            Assert.That(_models[0].Current, Is.Null);
            if (corruption == "foreign-source")
            {
                Assert.That(View(_main, "A").dataSource, Is.SameAs(foreign));
                View(_main, "A").dataSource = _models[0];
            }
            if (corruption == "reparent")
            {
                Assert.That(View(_main, "A").parent, Is.SameAs(_other));
                Assert.That(View(_main, "A").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                _main.Add(View(_main, "A"));
            }
            else
            {
                AssertBlank(_main);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FIFO_successors_wait_for_the_actual_callback_return_even_when_it_will_fail(bool fail)
        {
            UniTaskCompletionSource gate = Gate();
            bool running = false;
            _created = model =>
            {
                if (model.Name == "A")
                {
                    model.OnTo = async (_, _) =>
                    {
                        running = true;
                        try
                        {
                            await gate.Task;
                            if (fail)
                            {
                                throw _fault;
                            }
                        }
                        finally
                        {
                            running = false;
                            _trace.Add("A:callback-drained");
                        }
                    };
                }
                else if (model.Name == "B")
                {
                    model.OnTo = (_, _) =>
                    {
                        Assert.That(running, Is.False);
                        return UniTask.CompletedTask;
                    };
                }
            };
            UniTask<NavigationResult> first = Request("A");
            UniTask<NavigationResult> next = Request("B");
            UniTask<NavigationResult> clear = Clear();
            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(next.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(clear.Status, Is.EqualTo(UniTaskStatus.Pending));
            // A distinct Region is not held by the pending main callback.
            Require(Request("C", region: Other), NavigationStatus.Succeeded);
            Assert.That(running, Is.True);
            gate.TrySetResult();
            Require(first, fail ? NavigationStatus.Failed : NavigationStatus.Succeeded);
            Require(next, NavigationStatus.Succeeded);
            Require(clear, NavigationStatus.Succeeded);
            AssertBlank(_main);
            AssertSingle(_other, "C");
        }

        [TestCase("navigate")]
        [TestCase("clear")]
        [TestCase("reentry")]
        public void Latest_queued_supersession_does_not_construct_or_restart_and_uncooperative_entry_retains_ownership(string winner)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            NavigationActivation stale = a.Current;
            UniTaskCompletionSource gate = Gate();
            a.OnTo = (input, _) => input == "blocked" ? gate.Task : UniTask.CompletedTask;
            UniTask<NavigationResult> first = Request("A", "blocked", NavigationPolicy.LatestRequestWins);
            UniTask<NavigationResult> obsolete = Request("B", policy: NavigationPolicy.LatestRequestWins);
            UniTask<NavigationResult> last = winner == "clear" ? Clear(NavigationPolicy.LatestRequestWins) :
                Request(winner == "reentry" ? "A" : "C", "winner", NavigationPolicy.LatestRequestWins,
                    winner == "reentry" ? NavigationEntryMode.ReenterIfActive : NavigationEntryMode.Default);
            Assert.That(stale.IsValid, Is.False);
            Assert.That(stale.TryPublish(() => a.Publications++), Is.False);
            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(obsolete.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(last.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_models.Any(model => model.Name == "B"), Is.False);
            gate.TrySetResult();
            Failure(Done(first), NavigationStatus.Superseded, NavigationStage.Entry, NavigationRecovery.Contained);
            Failure(Done(obsolete), NavigationStatus.Superseded, NavigationStage.Queue, NavigationRecovery.Preserved);
            Require(last, winner == "clear" ? NavigationStatus.NoOp : NavigationStatus.Succeeded);
            Assert.That(_models.Any(model => model.Name == "B"), Is.False);
            Assert.That(stale.TryPublish(() => a.Publications++), Is.False);
            Assert.That(a.Publications, Is.Zero);
            if (winner == "clear")
            {
                AssertBlank(_main);
            }
            else
            {
                AssertSingle(_main, winner == "reentry" ? "A" : "C");
                Assert.That(Current(_main, winner == "reentry" ? "A" : "C").Current.IsValid, Is.True);
            }
        }

        [Test]
        public void Opted_in_latest_requests_do_not_cancel_a_FIFO_predecessor()
        {
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = (_, _) => model.Name == "A" ? gate.Task : UniTask.CompletedTask;
            UniTask<NavigationResult> first = Request("A");
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            UniTask<NavigationResult> obsolete = Request("B", policy: NavigationPolicy.LatestRequestWins);
            UniTask<NavigationResult> winner = Request("C", policy: NavigationPolicy.LatestRequestWins);
            Assert.That(lifetime.IsValid, Is.True);
            Assert.That(winner.Status, Is.EqualTo(UniTaskStatus.Pending));
            gate.TrySetResult();
            Require(first, NavigationStatus.Succeeded);
            Require(obsolete, NavigationStatus.Superseded);
            Require(winner, NavigationStatus.Succeeded);
            AssertSingle(_main, "C");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Executing_exit_must_drain_before_latest_winner_and_does_not_enter_obsolete_target(bool clearWinner)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            UniTaskCompletionSource gate = Gate();
            bool returned = false;
            a.OnAway = async () =>
            {
                await gate.Task;
                returned = true;
                _trace.Add("A:late-exit-write");
            };
            UniTask<NavigationResult> obsolete = Request("B", policy: NavigationPolicy.LatestRequestWins);
            UniTask<NavigationResult> winner = clearWinner ? Clear(NavigationPolicy.LatestRequestWins) :
                Request("C", policy: NavigationPolicy.LatestRequestWins);
            Assert.That(returned, Is.False);
            Assert.That(obsolete.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(winner.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_models.Single(model => model.Name == "B").ToCount, Is.Zero);
            gate.TrySetResult();
            Assert.That(returned, Is.True);
            Failure(Done(obsolete), NavigationStatus.Superseded, NavigationStage.Exit, NavigationRecovery.Contained);
            Require(winner, clearWinner ? NavigationStatus.NoOp : NavigationStatus.Succeeded);
            Assert.That(_models.Single(model => model.Name == "B").ToCount, Is.Zero);
            if (!clearWinner)
            {
                AssertSingle(_main, "C");
            }
        }

        [Test]
        public void Cooperative_cancellation_returns_a_superseded_result_only_after_callback_finally_runs()
        {
            UniTaskCompletionSource gate = Gate();
            bool drained = false;
            _created = model =>
            {
                if (model.Name == "A")
                {
                    model.OnTo = async (_, lifetime) =>
                    {
                        try
                        {
                            await gate.Task.AttachExternalCancellation(lifetime.CancellationToken);
                        }
                        finally
                        {
                            drained = true;
                        }
                    };
                }
                else
                {
                    model.OnTo = (_, _) =>
                    {
                        Assert.That(drained, Is.True);
                        return UniTask.CompletedTask;
                    };
                }
            };
            UniTask<NavigationResult> obsolete = Request("A", policy: NavigationPolicy.LatestRequestWins);
            UniTask<NavigationResult> winner = Request("B", policy: NavigationPolicy.LatestRequestWins);
            Require(obsolete, NavigationStatus.Superseded);
            Require(winner, NavigationStatus.Succeeded);
            Assert.That(drained, Is.True);
            AssertSingle(_main, "B");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Scoped_result_reentrancy_after_await_is_rejected_without_deadlock_or_state_change(bool clear)
        {
            UniTaskCompletionSource gate = Gate();
            NavigationResult nested = null;
            _created = model => model.OnTo = async (_, _) =>
            {
                await gate.Task;
                nested = await model.Scoped(clear, "B", Main);
            };
            UniTask<NavigationResult> outer = Request("A");
            gate.TrySetResult();
            Require(outer, NavigationStatus.Succeeded);
            Failure(nested, NavigationStatus.InvalidConfiguration, NavigationStage.Queue, NavigationRecovery.Preserved);
            AssertSingle(_main, "A");
            Assert.That(_models, Has.Count.EqualTo(1));
        }

        [Test]
        public void Scoped_independent_navigation_succeeds_and_a_dependency_cycle_is_an_explicit_result()
        {
            NavigationResult child = null;
            NavigationResult cycle = null;
            _created = model => model.OnTo = async (_, _) =>
            {
                if (model.Name == "A")
                {
                    child = await model.Scoped(false, "B", Other);
                }
                else
                {
                    cycle = await model.Scoped(false, "C", Main);
                }
            };
            Require(Request("A"), NavigationStatus.Succeeded);
            Assert.That(child.Status, Is.EqualTo(NavigationStatus.Succeeded));
            Failure(cycle, NavigationStatus.InvalidConfiguration, NavigationStage.Queue, NavigationRecovery.Preserved);
            AssertSingle(_main, "A");
            AssertSingle(_other, "B");
        }

        [TestCase(NavigationPolicy.Fifo)]
        [TestCase(NavigationPolicy.LatestRequestWins)]
        public void Explicit_reentry_result_keeps_identity_and_replaces_only_activation_lifetime(NavigationPolicy policy)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            VisualElement view = View(_main, "A");
            NavigationActivation old = a.Current;
            Require(Request("A", "restart", policy, NavigationEntryMode.ReenterIfActive), NavigationStatus.Succeeded);
            Assert.That(Current(_main, "A"), Is.SameAs(a));
            Assert.That(View(_main, "A"), Is.SameAs(view));
            Assert.That(a.Current, Is.Not.SameAs(old));
            Assert.That(a.AwayCount, Is.EqualTo(1));
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(a.ToCount, Is.EqualTo(2));
            Assert.That(a.Disposals, Is.Zero);
            Assert.That(old.IsValid, Is.False);
            AssertSingle(_main, "A");
        }

        [Test]
        public void Shared_multi_Region_entry_failure_contains_the_whole_owned_group_and_cleans_each_logical_model_once()
        {
            Region second = AddRegion(Main);
            RegionManager group = Manager(_main, second);
            Require(Request("A", manager: group), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            VisualElement first = View(_main, "A");
            VisualElement other = View(second, "A");
            Assert.That(Current(second, "A"), Is.SameAs(a));
            _created = model => model.OnTo = (_, _) => throw _fault;
            NavigationResult result = Done(Request("B", manager: group));
            Failure(result, NavigationStatus.Failed, NavigationStage.Entry, NavigationRecovery.Contained);
            Model b = Current(_main, "B");
            Assert.That(Current(second, "B"), Is.SameAs(b));
            Assert.That(View(_main, "B"), Is.Not.SameAs(View(second, "B")));
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(a.AwayCount, Is.EqualTo(1));
            Assert.That(b.Cleanups, Is.EqualTo(1));
            Assert.That(b.ToCount, Is.EqualTo(1));
            AssertBlank(_main);
            AssertBlank(second);
            _created = null;
            Require(Request("A", "recover", manager: group), NavigationStatus.Succeeded);
            Assert.That(View(_main, "A"), Is.SameAs(first));
            Assert.That(View(second, "A"), Is.SameAs(other));
            Assert.That(Current(second, "A").Current, Is.SameAs(a.Current));
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
        }

        [Test]
        public void Partial_multi_Region_exit_failure_is_contained_without_touching_unowned_Region()
        {
            Region second = AddRegion(Main);
            Require(Request("A", manager: Manager(_main)), NavigationStatus.Succeeded);
            Require(Request("C", manager: Manager(second)), NavigationStatus.Succeeded);
            Require(Request("A", region: Other), NavigationStatus.Succeeded);
            Model independent = Current(_other, "A");
            NavigationActivation untouched = independent.Current;
            Model c = Current(second, "C");
            c.OnAway = () => throw _fault;
            NavigationResult result = Done(Request("B", manager: Manager(_main, second)));
            Failure(result, NavigationStatus.Failed, NavigationStage.Exit, NavigationRecovery.Contained);
            AssertBlank(_main);
            AssertBlank(second);
            Assert.That(Current(_main, "A").Cleanups, Is.EqualTo(1));
            Assert.That(c.Cleanups, Is.EqualTo(1));
            Assert.That(_models.Single(model => model.Name == "B").ToCount, Is.Zero);
            Assert.That(independent.Current, Is.SameAs(untouched));
            AssertSingle(_other, "A");
        }

        [Test]
        public void Foreign_active_source_in_later_Region_rejects_the_whole_group_before_any_exit()
        {
            Region second = AddRegion(Main);
            Require(Request("A", manager: Manager(_main)), NavigationStatus.Succeeded);
            Require(Request("C", manager: Manager(second)), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            Model c = Current(second, "C");
            NavigationActivation lifetime = a.Current;
            object foreign = new object();
            View(second, "C").dataSource = foreign;
            NavigationResult result = Done(Request("B", manager: Manager(_main, second)));
            Failure(result, NavigationStatus.InvalidConfiguration, NavigationStage.Validation, NavigationRecovery.Preserved);
            Assert.That(a.Current, Is.SameAs(lifetime));
            Assert.That(a.AwayCount, Is.Zero);
            Assert.That(c.AwayCount, Is.Zero);
            Assert.That(View(second, "C").dataSource, Is.SameAs(foreign));
            AssertSingle(_main, "A");
            View(second, "C").dataSource = c;
        }

        [Test]
        public void Partial_shared_cohort_reentry_reports_invalid_configuration_before_restart_mutation()
        {
            Region second = AddRegion(Main);
            Require(Request("A", manager: Manager(_main, second)), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            NavigationResult result = Done(Request("A", mode: NavigationEntryMode.ReenterIfActive, manager: Manager(_main)));
            Failure(result, NavigationStatus.InvalidConfiguration, NavigationStage.Validation, NavigationRecovery.Preserved);
            Assert.That(a.Current, Is.SameAs(lifetime));
            Assert.That(a.AwayCount, Is.Zero);
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Heterogeneous_target_caches_are_not_silently_rebound_or_reported_success(bool reentry)
        {
            Region second = AddRegion(Main);
            Require(Request("A", manager: Manager(_main)), NavigationStatus.Succeeded);
            Require(Request("A", manager: Manager(second)), NavigationStatus.Succeeded);
            Model first = Current(_main, "A");
            Model last = Current(second, "A");
            Assert.That(last, Is.Not.SameAs(first));
            NavigationResult result = Done(Request("A", mode: reentry ? NavigationEntryMode.ReenterIfActive : NavigationEntryMode.Default,
                manager: Manager(_main, second)));
            Failure(result, NavigationStatus.InvalidConfiguration, NavigationStage.Validation, NavigationRecovery.Preserved);
            Assert.That(Current(_main, "A"), Is.SameAs(first));
            Assert.That(Current(second, "A"), Is.SameAs(last));
            Assert.That(first.ToCount, Is.EqualTo(1));
            Assert.That(last.ToCount, Is.EqualTo(1));
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Source_corruption_during_an_earlier_exit_cannot_turn_a_partial_group_operation_into_success(bool clear)
        {
            Region second = AddRegion(Main);
            Require(Request("A", manager: Manager(_main)), NavigationStatus.Succeeded);
            Require(Request("C", manager: Manager(second)), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            Model c = Current(second, "C");
            object foreign = new object();
            a.OnAway = () =>
            {
                View(second, "C").dataSource = foreign;
                return UniTask.CompletedTask;
            };
            RegionManager group = Manager(_main, second);
            NavigationResult result = Done(clear ? Track(_navigation.ClearResult(group, Main)) : Request("B", manager: group));
            Failure(result, NavigationStatus.InvalidConfiguration, NavigationStage.Validation, NavigationRecovery.Contained);
            Assert.That(a.Cleanups, Is.EqualTo(1));
            Assert.That(c.Cleanups, Is.EqualTo(1));
            Assert.That(c.AwayCount, Is.Zero, "Containment does not invent lifecycle callbacks.");
            Assert.That(View(second, "C").dataSource, Is.SameAs(foreign));
            AssertBlank(_main);
            AssertBlank(second);
            View(second, "C").dataSource = c;
            Require(Request("C", "recover", manager: group), NavigationStatus.Succeeded);
            Assert.That(Current(_main, "C"), Is.SameAs(c));
            Assert.That(Current(second, "C"), Is.SameAs(c));
            AssertSingle(_main, "C");
            AssertSingle(second, "C");
        }

        [Test]
        public void Busy_shared_model_rejects_an_independent_result_request_without_ending_the_owning_callback()
        {
            Region second = AddRegion(Main);
            Require(Request("A", manager: Manager(_main, second)), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            UniTaskCompletionSource gate = Gate();
            a.OnTo = (_, _) => gate.Task;
            UniTask<NavigationResult> owner = Request("A", manager: Manager(_main));
            NavigationResult rejected = Done(Request("A", manager: Manager(second)));
            Failure(rejected, NavigationStatus.InvalidConfiguration, NavigationStage.Validation, NavigationRecovery.Preserved);
            Assert.That(owner.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(a.Current, Is.SameAs(lifetime));
            Assert.That(lifetime.IsValid, Is.True);
            Assert.That(a.Cleanups, Is.Zero);
            gate.TrySetResult();
            Require(owner, NavigationStatus.Succeeded);
            AssertSingle(_main, "A");
            AssertSingle(second, "A");
        }

        [Test]
        public void Overlap_with_one_member_supersedes_and_contains_the_whole_executing_result_group()
        {
            Region second = AddRegion(Main);
            UniTaskCompletionSource gate = Gate();
            _created = model =>
            {
                if (model.Name == "A")
                {
                    model.OnTo = (_, _) => gate.Task;
                }
            };
            UniTask<NavigationResult> obsolete = Request("A", policy: NavigationPolicy.LatestRequestWins,
                manager: Manager(_main, second));
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            UniTask<NavigationResult> winner = Request("B", policy: NavigationPolicy.LatestRequestWins, manager: Manager(second));
            Assert.That(winner.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(lifetime.IsValid, Is.False);
            gate.TrySetResult();
            Failure(Done(obsolete), NavigationStatus.Superseded, NavigationStage.Entry, NavigationRecovery.Contained);
            Require(winner, NavigationStatus.Succeeded);
            Assert.That(a.Cleanups, Is.EqualTo(1));
            AssertBlank(_main);
            AssertSingle(second, "B");
        }

        private static ViewId Id(string name) => ViewId.From("outcome/" + name);
        private static NavigationParameters Parameters(string input) => NavigationParameters.Empty.Add(Input, input);
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
        private UniTask<NavigationResult> Request(string name, string input = "input", NavigationPolicy policy = NavigationPolicy.Fifo,
            NavigationEntryMode mode = NavigationEntryMode.Default, RegionManager manager = null, RegionName? region = null)
        {
            return Track(_navigation.NavigateToResult(manager ?? _manager, Id(name), region ?? Main, Parameters(input), policy, mode));
        }
        private UniTask<NavigationResult> Clear(NavigationPolicy policy = NavigationPolicy.Fifo)
        {
            return Track(_navigation.ClearResult(_manager, Main, policy));
        }
        private UniTask<NavigationResult> Track(UniTask<NavigationResult> task)
        {
            UniTask<NavigationResult> tracked = Record().Preserve();
            _tasks.Add(tracked);
            return tracked;
            async UniTask<NavigationResult> Record()
            {
                NavigationResult result = await task;
                _trace.Add("result=" + result.Status + ":stage=" + result.Stage + ":recovery=" + result.Recovery +
                    ":error=" + (result.Error?.ToString() ?? "null"));
                return result;
            }
        }
        private static NavigationResult Done(UniTask<NavigationResult> task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending), "Do not block Unity's main thread.");
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "Opt-in operational failures are values.");
            return task.GetAwaiter().GetResult();
        }
        private static void Finish(UniTask task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
            task.GetAwaiter().GetResult();
        }
        private static void Require(UniTask<NavigationResult> task, NavigationStatus expected)
        {
            NavigationResult result = Done(task);
            Assert.That(result.Status, Is.EqualTo(expected), result.Error?.ToString());
            Assert.That(result.IsSuccess, Is.EqualTo(expected == NavigationStatus.Succeeded));
        }
        private static void Failure(NavigationResult result, NavigationStatus status, NavigationStage stage, NavigationRecovery recovery)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(status), result.Error?.ToString());
            Assert.That(result.Stage, Is.EqualTo(stage));
            Assert.That(result.Recovery, Is.EqualTo(recovery));
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.IsSuccess, Is.False);
        }
        private static IEnumerable<Exception> Errors(Exception error)
        {
            return error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : new[] { error };
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
            Assert.That(View(region, name).parent, Is.SameAs(region));
            Assert.That(Current(region, name).Current.IsValid, Is.True);
        }
        private static void AssertBlank(Region region)
        {
            Assert.That(region.ActiveViews, Is.Empty);
            Assert.That(region.Children().All(view => view.style.display.value == DisplayStyle.None), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            int primaryCount = _trace.Count;
            string primaryModels = Snapshot();
            try
            {
                _prepareFault = null;
                _created = null;
                _construct = null;
                foreach (Model model in _models)
                {
                    model.OnTo = null;
                    model.OnAway = null;
                }
                foreach (FaultRegion region in _regions.OfType<FaultRegion>())
                {
                    region.FailAdd = false;
                }
                foreach (UniTaskCompletionSource gate in _gates)
                {
                    gate.TrySetResult();
                }
                foreach (UniTask<NavigationResult> task in _tasks)
                {
                    Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
                }
                foreach (Region region in _regions)
                {
                    region.SupportMultipleViews = false;
                    NavigationStatus expected = region.ActiveViews.Count == 0 ? NavigationStatus.NoOp : NavigationStatus.Succeeded;
                    Require(Track(_navigation.ClearResult(Manager(region), ((IRegion)region).RegionName)), expected);
                }
                foreach (Model model in _models)
                {
                    Finish(model.EndActivationAsync());
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
                Directory.CreateDirectory("Logs/navigation-phase4");
                string file = Regex.Replace(TestContext.CurrentContext.Test.Name, "[^a-zA-Z0-9._-]", "_");
                File.WriteAllText("Logs/navigation-phase4/" + file + ".txt", "test=" + TestContext.CurrentContext.Test.Name +
                    "\noutcome=" + TestContext.CurrentContext.Result.Outcome.Status + "\nprimaryTraceCount=" + primaryCount +
                    "\nPRIMARY MODELS\n" + primaryModels + "\nTRACE (teardown begins at primaryTraceCount)\n" +
                    string.Join("\n", _trace) + "\nFINAL MODELS\n" + Snapshot() + "\n");
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
        private string Snapshot()
        {
            return string.Join("\n", _models.Select(model => model.Name + ":model=" + RuntimeHelpers.GetHashCode(model) +
                ":To=" + model.ToCount + ":Away=" + model.AwayCount + ":Cleanup=" + model.Cleanups +
                ":FinalDispose=" + model.Disposals + ":Activation=" + (model.Current == null ? "null" : model.Current.IsValid.ToString())));
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
            private readonly NavigationOutcomeTests _fixture;
            internal Bridge(NavigationOutcomeTests fixture)
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
                _fixture._construct?.Invoke(type);
                Model model = type == typeof(A) ? new A(_fixture) : type == typeof(B) ? new B(_fixture) : new C(_fixture);
                _fixture._models.Add(model);
                _fixture._created?.Invoke(model);
                return model;
            }
        }
        private sealed class FaultNavigation : NavigationService
        {
            private readonly NavigationOutcomeTests _fixture;
            internal FaultNavigation(NavigationOutcomeTests fixture, IViewModelConstructionService construction, Internal.IViewIdRegistry registry)
                : base(construction, registry, null)
            {
                _fixture = fixture;
            }
            internal override VisualElement PrepareView(ViewIdAsset asset, BaseViewModel model)
            {
                if (_fixture._prepareFault == "before-create")
                {
                    _fixture._trace.Add("injected:before-real-instantiation");
                    throw _fixture._fault;
                }
                VisualElement view = base.PrepareView(asset, model);
                if (_fixture._prepareFault == "after-detached-binding")
                {
                    Assert.That(view.dataSource, Is.SameAs(model));
                    Assert.That(view.parent, Is.Null);
                    _fixture._trace.Add("injected:after-real-detached-dataSource-assignment");
                    throw _fixture._fault;
                }
                return view;
            }
        }
        private sealed class FaultRegion : Region, IRegion
        {
            private readonly Exception _fault;
            internal bool FailAdd;
            internal bool AfterMount;
            internal FaultRegion(RegionName name, Exception fault) : base(name)
            {
                _fault = fault;
            }
            void IRegion.AddView(ViewId id, VisualElement view)
            {
                if (FailAdd && !AfterMount)
                {
                    throw _fault;
                }
                base.AddView(id, view);
                if (FailAdd)
                {
                    Assert.That(view.panel, Is.Not.Null);
                    Assert.That(view.dataSource, Is.TypeOf<B>());
                    Assert.That(view.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                    throw _fault;
                }
            }
        }
        private abstract class Model : BaseViewModel
        {
            private readonly NavigationOutcomeTests _fixture;
            internal readonly string Name;
            internal readonly List<NavigationActivation> Entries = new();
            internal Func<string, NavigationActivation, UniTask> OnTo;
            internal Func<UniTask> OnAway;
            internal int ToCount;
            internal int AwayCount;
            internal int Cleanups;
            internal int Disposals;
            internal int Publications;
            internal NavigationActivation Current => Activation;
            protected Model(string name, NavigationOutcomeTests fixture)
            {
                Name = name;
                _fixture = fixture;
                Disposables.Add(R3.Disposable.Create(() => Disposals++));
            }
            internal UniTask<NavigationResult> Scoped(bool clear, string name, RegionName region)
            {
                return clear ? Navigation.ClearResult(region) : Navigation.NavigateResult(Id(name), region, Parameters("scoped"));
            }
            protected override async UniTask NavigateTo(NavigationParameters parameters)
            {
                ToCount++;
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
                string input = parameters.Get(Input).HasValue ? parameters.Get(Input).Value : "empty";
                _fixture._trace.Add(Name + ":To:" + input + ":lifetime=" + RuntimeHelpers.GetHashCode(lifetime));
                if (OnTo != null)
                {
                    await OnTo(input, lifetime);
                }
                _fixture._trace.Add(Name + ":To.return");
            }
            protected override async UniTask NavigateAway()
            {
                AwayCount++;
                _fixture._trace.Add(Name + ":Away:activation=" + (Activation == null ? "null" : "non-null"));
                if (OnAway != null)
                {
                    await OnAway();
                }
                _fixture._trace.Add(Name + ":Away.return");
            }
        }
        private sealed class A : Model
        {
            internal A(NavigationOutcomeTests fixture) : base("A", fixture)
            {
            }
        }
        private sealed class B : Model
        {
            internal B(NavigationOutcomeTests fixture) : base("B", fixture)
            {
            }
        }
        private sealed class C : Model
        {
            internal C(NavigationOutcomeTests fixture) : base("C", fixture)
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
        public sealed class TestWindow : EditorWindow
        {
        }
    }
}
