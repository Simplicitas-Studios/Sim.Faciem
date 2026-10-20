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
    public sealed class NavigationTeardownTests
    {
        private static readonly RegionName Main = RegionName.From("teardown/main");
        private static readonly RegionName Other = RegionName.From("teardown/other");
        private static readonly IPropertyKey<string> Input = PropertyKey.From<string>("Teardown/Input");
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
        private INavigationService _navigation;
        private Action<Model> _created;
        private Model _reuse;
        private bool _failPreparation;
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
            _reuse = null;
            _failPreparation = false;
            _fault = new InvalidOperationException("injected teardown fault");
            _previous = EditorWindow.focusedWindow;
            _window = ScriptableObject.CreateInstance<TestWindow>();
            _window.titleContent = new GUIContent("Faciem final ownership tests");
            _window.Show();
            _main = Add(Main);
            _other = Add(Other);
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
            _navigation = new Service(this, new ViewModelConstructionService(new Bridge(this)), new Registry(_assets));
            for (int frame = 0; frame < 60 && _main.panel == null; frame++)
            {
                yield return null;
            }
            Assert.That(_main.panel, Is.Not.Null);
        }

        [Test]
        public void A_B_A_Clear_and_reentry_preserve_exact_cache_references_until_explicit_final_release()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            VisualElement first = View(_main, "A");
            NavigationActivation activation = a.Current;
            Require(Request("B"), NavigationStatus.Succeeded);
            Model b = Current(_main, "B");
            VisualElement second = View(_main, "B");
            Require(Request("A"), NavigationStatus.Succeeded);
            Assert.That(Current(_main, "A"), Is.SameAs(a));
            Assert.That(View(_main, "A"), Is.SameAs(first));
            Assert.That(a.Current, Is.Not.SameAs(activation));
            Require(Clear(), NavigationStatus.Succeeded);
            Assert.That(View(_main, "A"), Is.SameAs(first));
            Assert.That(View(_main, "B"), Is.SameAs(second));
            Assert.That(a.Disposals + b.Disposals, Is.Zero);
            Require(Request("A"), NavigationStatus.Succeeded);
            Require(Request("A", mode: NavigationEntryMode.ReenterIfActive), NavigationStatus.Succeeded);
            NavigationActivation last = a.Current;
            Require(Release(_main), NavigationStatus.Succeeded);
            Assert.That(a.Disposals, Is.EqualTo(1));
            Assert.That(b.Disposals, Is.EqualTo(1));
            Assert.That(a.Current, Is.Null);
            Assert.That(last.IsValid, Is.False);
            Assert.That(last.TryPublish(() => a.Publications++), Is.False);
            AssertReleased(_main);
            Assert.That(first.parent, Is.Null);
            Assert.That(second.parent, Is.Null);
            Assert.That(first.dataSource, Is.Null);
            Assert.That(second.dataSource, Is.Null);
            Require(Release(_main), NavigationStatus.NoOp);
            a.Dispose();
            b.Dispose();
            Assert.That(a.Disposals + b.Disposals, Is.EqualTo(2));
        }

        [Test]
        public void A_new_Region_scope_creates_fresh_instances_and_the_old_scope_remains_terminal()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model old = Current(_main, "A");
            VisualElement view = View(_main, "A");
            Require(Release(_main), NavigationStatus.Succeeded);
            Require(Request("A"), NavigationStatus.ScopeClosed);
            Require(Clear(), NavigationStatus.ScopeClosed);
            Region fresh = Add(Main);
            Require(Request("A", regions: new[] { fresh }), NavigationStatus.Succeeded);
            Assert.That(Current(fresh, "A"), Is.Not.SameAs(old));
            Assert.That(View(fresh, "A"), Is.Not.SameAs(view));
            Assert.That(old.Disposals, Is.EqualTo(1));
            AssertSingle(fresh, "A");
            Assert.Throws<ObjectDisposedException>(() => _main.ActivateView(Id("A")));
            Assert.Throws<ObjectDisposedException>(() => _main.AddView(Id("B"), new VisualElement()));
        }

        [TestCase(NavigationPolicy.Fifo)]
        [TestCase(NavigationPolicy.LatestRequestWins)]
        public void Awaiting_entry_retains_ownership_and_final_disposal_waits_for_actual_return(NavigationPolicy policy)
        {
            UniTaskCompletionSource gate = Gate();
            bool drained = false;
            _created = model => model.OnTo = async _ =>
            {
                await gate.Task;
                drained = true;
                _trace.Add("entry:drained");
            };
            UniTask<NavigationResult> entry = Request("A", policy);
            Model a = Current(_main, "A");
            NavigationActivation lifetime = a.Current;
            a.FinalProbe = () => Assert.That(drained, Is.True);
            UniTask<NavigationResult> release = Release(_main);
            UniTask<NavigationResult> repeated = Release(_main);
            Assert.That(release.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(repeated.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(lifetime.IsValid, Is.True, "Teardown drains, rather than supersedes, prior work.");
            Assert.That(a.Disposals, Is.Zero);
            Require(Request("B", NavigationPolicy.LatestRequestWins), NavigationStatus.ScopeClosed);
            gate.TrySetResult();
            Require(entry, NavigationStatus.Succeeded);
            Require(release, NavigationStatus.Succeeded);
            Require(repeated, NavigationStatus.NoOp);
            Assert.That(a.Disposals, Is.EqualTo(1));
            Assert.That(lifetime.TryPublish(() => a.Publications++), Is.False);
            AssertReleased(_main);
        }

        [TestCase("navigate")]
        [TestCase("clear")]
        [TestCase("reentry")]
        public void Earlier_exit_and_its_queued_transition_drain_before_teardown(string operation)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            UniTaskCompletionSource gate = Gate();
            a.OnAway = () => gate.Task;
            UniTask<NavigationResult> pending = operation == "clear" ? Clear() : Request(operation == "navigate" ? "B" : "A",
                mode: operation == "reentry" ? NavigationEntryMode.ReenterIfActive : NavigationEntryMode.Default);
            UniTask<NavigationResult> release = Release(_main);
            Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(release.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_models.All(model => model.Disposals == 0), Is.True);
            gate.TrySetResult();
            Require(pending, NavigationStatus.Succeeded);
            Require(release, NavigationStatus.Succeeded);
            Assert.That(_models.All(model => model.Disposals == 1), Is.True);
            AssertReleased(_main);
        }

        [TestCase(NavigationPolicy.Fifo)]
        [TestCase(NavigationPolicy.LatestRequestWins)]
        public void FIFO_terminal_barrier_drains_admitted_latest_winners_and_rejects_later_supersession(NavigationPolicy firstPolicy)
        {
            UniTaskCompletionSource gate = Gate();
            _created = model =>
            {
                if (model.Name == "A")
                {
                    model.OnTo = _ => gate.Task;
                }
            };
            UniTask<NavigationResult> first = Request("A", firstPolicy);
            UniTask<NavigationResult> obsolete = Request("B", NavigationPolicy.LatestRequestWins);
            UniTask<NavigationResult> winner = Request("C", NavigationPolicy.LatestRequestWins);
            UniTask<NavigationResult> release = Release(_main);
            Require(Request("B", NavigationPolicy.LatestRequestWins), NavigationStatus.ScopeClosed);
            Require(Clear(NavigationPolicy.LatestRequestWins), NavigationStatus.ScopeClosed);
            Assert.That(release.Status, Is.EqualTo(UniTaskStatus.Pending));
            gate.TrySetResult();
            Require(first, firstPolicy == NavigationPolicy.Fifo ? NavigationStatus.Succeeded : NavigationStatus.Superseded);
            Require(obsolete, NavigationStatus.Superseded);
            Require(winner, NavigationStatus.Succeeded);
            Require(release, NavigationStatus.Succeeded);
            Assert.That(_models.Any(model => model.Name == "B"), Is.False);
            Assert.That(_models.All(model => model.Disposals == 1), Is.True);
            AssertReleased(_main);
        }

        [Test]
        public void Teardown_own_exit_await_holds_both_Region_and_model_leases_until_cleanup_finishes()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            UniTaskCompletionSource gate = Gate();
            a.OnAway = () => gate.Task;
            UniTask<NavigationResult> release = Release(_main);
            Assert.That(a.Current, Is.Null);
            Assert.That(a.Disposals, Is.Zero);
            Assert.That(release.Status, Is.EqualTo(UniTaskStatus.Pending));
            _reuse = a;
            NavigationResult rejected = Done(Request("A", regions: new[] { _other }));
            Assert.That(rejected.IsSuccess, Is.False);
            Assert.That(_other.TryGetView(Id("A"), out _), Is.False);
            gate.TrySetResult();
            Require(release, NavigationStatus.Succeeded);
            Assert.That(a.Disposals, Is.EqualTo(1));
        }

        [TestCase("entry")]
        [TestCase("exit")]
        [TestCase("dispose")]
        public void Scoped_reentrant_teardown_is_rejected_before_mutation_or_idempotent_shortcut(string phase)
        {
            NavigationResult nested = null;
            _created = model =>
            {
                if (phase == "entry")
                {
                    model.OnTo = async _ => nested = await model.ScopedRelease(_main);
                }
                if (phase == "exit")
                {
                    model.OnAway = async () => nested = await model.ScopedRelease(_main);
                }
                if (phase == "dispose")
                {
                    model.FinalProbe = () => nested = Done(model.ScopedRelease(_main));
                }
            };
            Require(Request("A"), NavigationStatus.Succeeded);
            if (phase != "entry")
            {
                Require(Release(_main), NavigationStatus.Succeeded);
            }
            Assert.That(nested.Status, Is.EqualTo(NavigationStatus.InvalidConfiguration));
            Assert.That(nested.Stage, Is.EqualTo(NavigationStage.Queue));
            Assert.That(nested.Recovery, Is.EqualTo(NavigationRecovery.Preserved));
            if (phase == "entry")
            {
                AssertSingle(_main, "A");
                Require(Request("B"), NavigationStatus.Succeeded);
            }
        }

        [Test]
        public void Independent_Region_and_persistent_shell_remain_alive_and_usable()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Require(Request("B"), NavigationStatus.Succeeded);
            Require(Request("C", regions: new[] { _other }), NavigationStatus.Succeeded);
            Model shell = Current(_other, "C");
            VisualElement shellTree = View(_other, "C");
            NavigationActivation lifetime = shell.Current;
            Require(Release(_main), NavigationStatus.Succeeded);
            Assert.That(shell.Disposals, Is.Zero);
            Assert.That(shell.Current, Is.SameAs(lifetime));
            Assert.That(View(_other, "C"), Is.SameAs(shellTree));
            Assert.That(lifetime.TryPublish(() => shell.Publications++), Is.True);
            AssertSingle(_other, "C");
            Require(Request("A", regions: new[] { _other }), NavigationStatus.Succeeded);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Shared_model_requires_all_cached_owners_even_when_the_other_owner_is_inactive(bool clearFirst)
        {
            Region second = Add(Main);
            Require(Request("A", regions: new[] { _main, second }), NavigationStatus.Succeeded);
            Model shared = Current(_main, "A");
            Assert.That(Current(second, "A"), Is.SameAs(shared));
            if (clearFirst)
            {
                Require(Clear(regions: new[] { second }), NavigationStatus.Succeeded);
            }
            NavigationResult rejected = Done(Release(_main));
            Assert.That(rejected.Status, Is.EqualTo(NavigationStatus.InvalidConfiguration));
            Assert.That(rejected.Recovery, Is.EqualTo(NavigationRecovery.Preserved));
            Assert.That(shared.Disposals, Is.Zero);
            Require(Release(_main, second), NavigationStatus.Succeeded);
            Assert.That(shared.Disposals, Is.EqualTo(1));
            AssertReleased(_main);
            AssertReleased(second);
        }

        [Test]
        public void Shared_model_final_lease_blocks_a_new_owner_while_a_different_model_exit_is_awaiting()
        {
            Region second = Add(Main);
            Require(Request("A"), NavigationStatus.Succeeded);
            Require(Request("B", regions: new[] { second }), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            Model b = Current(second, "B");
            UniTaskCompletionSource gate = Gate();
            b.OnAway = () => gate.Task;
            UniTask<NavigationResult> release = Release(_main, second);
            Assert.That(a.AwayCount, Is.EqualTo(1));
            Assert.That(a.Disposals, Is.Zero);
            _reuse = a;
            NavigationResult rejected = Done(Request("A", regions: new[] { _other }));
            Assert.That(rejected.IsSuccess, Is.False);
            Assert.That(_other.TryGetView(Id("A"), out _), Is.False);
            gate.TrySetResult();
            Require(release, NavigationStatus.Succeeded);
            Assert.That(a.Disposals, Is.EqualTo(1));
            Assert.That(b.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void Nested_parent_is_explicitly_rejected_but_selected_leaf_can_release_without_disposing_parent()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model parent = Current(_main, "A");
            Region child = AddChild(parent, View(_main, "A"));
            Require(Request("B", regions: new[] { child }), NavigationStatus.Succeeded);
            Model leaf = Current(child, "B");
            NavigationResult rejected = Done(Release(_main));
            Assert.That(rejected.Status, Is.EqualTo(NavigationStatus.UnsupportedOperation));
            Assert.That(rejected.Recovery, Is.EqualTo(NavigationRecovery.Preserved));
            Assert.That(parent.Disposals + leaf.Disposals, Is.Zero);
            AssertSingle(_main, "A");
            AssertSingle(child, "B");
            Require(Release(child), NavigationStatus.Succeeded);
            Assert.That(leaf.Disposals, Is.EqualTo(1));
            Assert.That(parent.Disposals, Is.Zero);
            Assert.That(parent.Current.IsValid, Is.True);
            Assert.That(child.parent, Is.Not.Null);
            AssertSingle(_main, "A");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Explicit_ownership_denial_or_predicate_fault_reopens_admission_without_partial_disposal(bool throws)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Require(Request("B"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            Model b = Current(_main, "B");
            NavigationActivation current = b.Current;
            NavigationResult result = Done(Track(_navigation.TeardownResult(new[] { _main }, model =>
            {
                if (ReferenceEquals(model, b))
                {
                    if (throws)
                    {
                        throw _fault;
                    }
                    return false;
                }
                return true;
            })));
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.InvalidConfiguration));
            Assert.That(result.Recovery, Is.EqualTo(NavigationRecovery.Preserved));
            Assert.That(a.Disposals + b.Disposals, Is.Zero);
            Assert.That(b.Current, Is.SameAs(current));
            Require(Request("A"), NavigationStatus.Succeeded);
            Require(Release(_main), NavigationStatus.Succeeded);
        }

        [TestCase("foreign")]
        [TestCase("reparented")]
        [TestCase("untracked")]
        [TestCase("multi")]
        [TestCase("global")]
        public void Unsafe_cache_configuration_rejects_before_final_ownership_or_UI_changes(string kind)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            VisualElement view = View(_main, "A");
            if (kind == "foreign")
            {
                view.dataSource = new object();
            }
            else if (kind == "reparented")
            {
                _other.Add(view);
            }
            else if (kind == "untracked")
            {
                _main.AddView(Id("untracked"), new VisualElement());
            }
            else if (kind == "multi")
            {
                _main.SupportMultipleViews = true;
            }
            else
            {
                _main.IsGlobal = true;
            }
            NavigationResult result = Done(Release(_main));
            Assert.That(result.Status, Is.EqualTo(kind == "multi" || kind == "global" ? NavigationStatus.UnsupportedOperation : NavigationStatus.InvalidConfiguration));
            Assert.That(result.Recovery, Is.EqualTo(NavigationRecovery.Preserved));
            Assert.That(a.Disposals, Is.Zero);
            Assert.That(a.Current.IsValid, Is.True);
            view.dataSource = a;
            if (kind == "reparented")
            {
                _main.Add(view);
            }
            _main.SupportMultipleViews = false;
            _main.IsGlobal = false;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Tracked_partial_commit_before_or_after_mount_is_finally_released(bool afterMount)
        {
            var region = new FaultRegion(Main, _fault) { AfterMount = afterMount };
            _regions.Add(region);
            _window.rootVisualElement.Add(region);
            Require(Request("A", regions: new[] { region }), NavigationStatus.Succeeded);
            region.Fail = true;
            Require(Request("B", regions: new[] { region }), NavigationStatus.Failed);
            Model b = _models.Single(model => model.Name == "B");
            VisualElement partial = region.OwnedEntries.Single(entry => ReferenceEquals(entry.Model, b)).View;
            Assert.That(partial.parent != null, Is.EqualTo(afterMount));
            Require(Release(region), NavigationStatus.Succeeded);
            Assert.That(_models.All(model => model.Disposals == 1), Is.True);
            Assert.That(partial.parent, Is.Null);
            Assert.That(partial.dataSource, Is.Null);
            AssertReleased(region);
        }

        [Test]
        public void Preparation_candidate_not_adopted_by_cache_remains_with_its_existing_DI_owner()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            _failPreparation = true;
            Require(Request("B"), NavigationStatus.Failed);
            Model b = _models.Single(model => model.Name == "B");
            Require(Release(_main), NavigationStatus.Succeeded);
            Assert.That(_models.Single(model => model.Name == "A").Disposals, Is.EqualTo(1));
            Assert.That(b.Disposals, Is.Zero, "A detached preparation failure did not transfer candidate ownership to a cache.");
            b.Dispose();
            Assert.That(b.Disposals, Is.EqualTo(1));
        }

        [TestCase("cleanup")]
        [TestCase("exit")]
        [TestCase("dispose")]
        public void Failed_teardown_remains_terminal_and_repeated_call_retains_failure_without_retrying_resources(string stage)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            if (stage == "cleanup")
            {
                a.Current.Own(new Resource(() => throw _fault));
            }
            else if (stage == "exit")
            {
                a.OnAway = () => throw _fault;
            }
            else
            {
                a.OwnFinal(new Resource(() => throw _fault));
            }
            NavigationResult result = Done(Release(_main));
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(result.Stage, Is.EqualTo(stage == "cleanup" ? NavigationStage.Cleanup :
                stage == "exit" ? NavigationStage.Exit : NavigationStage.FinalDisposal));
            Assert.That(result.Recovery, Is.EqualTo(NavigationRecovery.Incomplete));
            Assert.That(Flatten(result.Error), Does.Contain(_fault));
            Assert.That(a.Disposals, Is.EqualTo(1));
            Assert.That(a.AwayCount, Is.EqualTo(1));
            AssertReleased(_main);
            Assert.That(Done(Release(_main)), Is.SameAs(result));
            Require(Request("A"), NavigationStatus.ScopeClosed);
            a.Dispose();
            Assert.That(a.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void All_owned_models_and_stages_are_attempted_and_failures_aggregate_in_order()
        {
            Region second = Add(Main);
            Require(Request("A"), NavigationStatus.Succeeded);
            Require(Request("B", regions: new[] { second }), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            Model b = Current(second, "B");
            var exitFault = new ArgumentException("exit fault");
            var disposeA = new ArgumentException("final A fault");
            var disposeB = new ArgumentException("final B fault");
            a.Current.Own(new Resource(() => throw _fault));
            a.OnAway = () => throw exitFault;
            a.OwnFinal(new Resource(() => throw disposeA));
            b.OwnFinal(new Resource(() => throw disposeB));
            VisualElement av = View(_main, "A");
            VisualElement bv = View(second, "B");
            a.FinalProbe = b.FinalProbe = () =>
            {
                Assert.That(av.parent, Is.Null);
                Assert.That(bv.parent, Is.Null);
                Assert.That(av.dataSource, Is.Null);
                Assert.That(bv.dataSource, Is.Null);
            };
            NavigationResult result = Done(Release(_main, second));
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(result.Stage, Is.EqualTo(NavigationStage.Cleanup));
            Assert.That(Flatten(result.Error), Does.Contain(_fault));
            Assert.That(Flatten(result.Error), Does.Contain(exitFault));
            Assert.That(Flatten(result.Error), Does.Contain(disposeA));
            Assert.That(Flatten(result.Error), Does.Contain(disposeB));
            Assert.That(a.Disposals, Is.EqualTo(1));
            Assert.That(b.Disposals, Is.EqualTo(1));
            Assert.That(a.AwayCount, Is.EqualTo(1));
            Assert.That(b.AwayCount, Is.EqualTo(1));
            Assert.That(a.Current, Is.Null);
            Assert.That(b.Current, Is.Null);
            AssertReleased(_main);
            AssertReleased(second);
        }

        [Test]
        public void Binding_references_are_cleared_before_final_model_disposal()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            VisualElement view = View(_main, "A");
            Label label = view.Q<Label>("caption");
            label.SetBinding("text", new DataBinding());
            label.dataSource = a;
            _trace.Add("binding:before-release:infos=" + label.GetBindingInfos().Count());
            a.FinalProbe = () =>
            {
                _trace.Add("binding:at-final-dispose:infos=" + label.GetBindingInfos().Count());
                Assert.That(label.GetBinding("text"), Is.Null);
                Assert.That(label.dataSource, Is.Null);
                Assert.That(view.dataSource, Is.Null);
                Assert.That(view.parent, Is.Null);
            };
            Require(Release(_main), NavigationStatus.Succeeded);
        }

        [Test]
        public void A_disposed_DI_singleton_cannot_be_reactivated_in_a_new_scope()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            Require(Release(_main), NavigationStatus.Succeeded);
            _reuse = a;
            Region fresh = Add(Main);
            NavigationResult result = Done(Request("A", regions: new[] { fresh }));
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(result.Stage, Is.EqualTo(NavigationStage.Construction));
            Assert.That(fresh.CachedViews, Is.Empty);
            Assert.That(a.Current, Is.Null);
            Assert.That(a.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void Overlapping_inflight_teardown_sets_are_rejected_without_closing_unrelated_Region()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            UniTaskCompletionSource gate = Gate();
            a.OnAway = () => gate.Task;
            UniTask<NavigationResult> first = Release(_main);
            Require(Release(_main, _other), NavigationStatus.InvalidConfiguration);
            Require(Request("C", regions: new[] { _other }), NavigationStatus.Succeeded);
            gate.TrySetResult();
            Require(first, NavigationStatus.Succeeded);
            AssertSingle(_other, "C");
        }

        [Test]
        public void Custom_service_teardown_is_unsupported_and_never_dispatches()
        {
            INavigationService custom = new CustomNavigation();
            Require(Track(custom.TeardownResult(new[] { _main }, _ => true)), NavigationStatus.UnsupportedOperation);
            var model = new A(this);
            model.Setup(new RegionManager(), custom);
            Require(model.ScopedRelease(_main), NavigationStatus.UnsupportedOperation);
            model.Dispose();
        }

        [Test]
        public void Foreign_source_introduced_during_exit_is_not_overwritten_or_disposed_as_a_cached_model()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            VisualElement view = View(_main, "A");
            object foreign = new object();
            a.OnAway = () =>
            {
                view.dataSource = foreign;
                return UniTask.CompletedTask;
            };
            NavigationResult result = Done(Release(_main));
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(result.Stage, Is.EqualTo(NavigationStage.ViewRelease));
            Assert.That(view.dataSource, Is.SameAs(foreign));
            Assert.That(a.Disposals, Is.EqualTo(1));
            Assert.That(_main.CachedViews, Is.Empty);
        }

        [Test]
        public void A_nested_scope_added_during_an_awaited_exit_is_not_forcibly_detached_with_its_callback_running()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            VisualElement parent = View(_main, "A");
            UniTaskCompletionSource away = Gate();
            a.OnAway = () => away.Task;
            UniTask<NavigationResult> release = Release(_main);
            var child = new Region(Main);
            _regions.Add(child);
            parent.Add(child);
            UniTaskCompletionSource entry = Gate();
            _created = model => model.OnTo = _ => entry.Task;
            UniTask<NavigationResult> nested = Request("B", regions: new[] { child });
            Model b = Current(child, "B");
            away.TrySetResult();
            NavigationResult result = Done(release);
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(result.Stage, Is.EqualTo(NavigationStage.ViewRelease));
            Assert.That(result.Recovery, Is.EqualTo(NavigationRecovery.Incomplete));
            Assert.That(nested.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(child.panel, Is.Not.Null);
            Assert.That(b.Disposals, Is.Zero);
            Assert.That(b.Current.IsValid, Is.True);
            entry.TrySetResult();
            Require(nested, NavigationStatus.Succeeded);
            AssertSingle(child, "B");
        }

        [TestCase("empty")]
        [TestCase("null-list")]
        [TestCase("null-region")]
        [TestCase("null-owner")]
        public void Invalid_teardown_arguments_do_not_fence_or_dispose_a_live_scope(string kind)
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            IReadOnlyList<Region> regions = kind == "empty" ? Array.Empty<Region>() :
                kind == "null-list" ? null : kind == "null-region" ? new Region[] { null } : new[] { _main };
            Func<BaseViewModel, bool> owns = kind == "null-owner" ? null : _ => true;
            NavigationResult result = Done(Track(_navigation.TeardownResult(regions, owns)));
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.InvalidConfiguration));
            Assert.That(result.Recovery, Is.EqualTo(NavigationRecovery.Preserved));
            Assert.That(Current(_main, "A").Disposals, Is.Zero);
            Require(Request("B"), NavigationStatus.Succeeded);
        }

        [Test]
        public void A_captured_disposed_scoped_handle_cannot_tear_down_a_new_application_scope()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            IViewModelNavigationService captured = Current(_main, "A").Handle;
            Require(Release(_main), NavigationStatus.Succeeded);
            Require(Request("B", regions: new[] { _other }), NavigationStatus.Succeeded);
            Require(Track(captured.TeardownResult(new[] { _other }, _ => true)), NavigationStatus.ScopeClosed);
            Assert.That(Current(_other, "B").Disposals, Is.Zero);
            AssertSingle(_other, "B");
        }

        [Test]
        public void An_ownership_rejected_queued_teardown_does_not_prevent_an_already_queued_authorized_teardown()
        {
            UniTaskCompletionSource gate = Gate();
            _created = model => model.OnTo = _ => gate.Task;
            UniTask<NavigationResult> entry = Request("A");
            UniTask<NavigationResult> denied = Track(_navigation.TeardownResult(new[] { _main }, _ => false));
            UniTask<NavigationResult> allowed = Release(_main);
            gate.TrySetResult();
            Require(entry, NavigationStatus.Succeeded);
            Require(denied, NavigationStatus.InvalidConfiguration);
            Require(allowed, NavigationStatus.Succeeded);
            Require(Request("A"), NavigationStatus.ScopeClosed);
            Assert.That(_models.Single().Disposals, Is.EqualTo(1));
        }

        [Test]
        public void Concurrent_repetition_retains_a_failed_teardown_result_without_duplicate_exit_or_disposal()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            UniTaskCompletionSource gate = Gate();
            a.OnAway = async () =>
            {
                await gate.Task;
                throw _fault;
            };
            UniTask<NavigationResult> first = Release(_main);
            UniTask<NavigationResult> repeat = Release(_main);
            Assert.That(repeat.Status, Is.EqualTo(UniTaskStatus.Pending));
            gate.TrySetResult();
            NavigationResult result = Done(first);
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(Done(repeat), Is.SameAs(result));
            Assert.That(a.Disposals, Is.EqualTo(1));
            Assert.That(a.AwayCount, Is.EqualTo(1));
        }

        [Test]
        public void Opaque_legacy_R3_bag_failure_is_reported_incomplete_not_misrepresented_as_all_resources_released()
        {
            Require(Request("A"), NavigationStatus.Succeeded);
            Model a = Current(_main, "A");
            int afterThrow = 0;
            a.OwnFinal(new Resource(() => throw _fault));
            a.OwnFinal(new Resource(() => afterThrow++));
            NavigationResult result = Done(Release(_main));
            Assert.That(result.Status, Is.EqualTo(NavigationStatus.Failed));
            Assert.That(result.Recovery, Is.EqualTo(NavigationRecovery.Incomplete));
            Assert.That(afterThrow, Is.Zero, "Characterization: the existing opaque R3 bag stops on a user resource failure.");
            a.Dispose();
            Assert.That(afterThrow, Is.Zero, "Never retry an already attempted opaque bag and duplicate earlier effects.");
            Assert.That(a.Disposals, Is.EqualTo(1));
            AssertReleased(_main);
        }

        private static ViewId Id(string name) => ViewId.From("teardown/" + name);
        private Region Add(RegionName name)
        {
            var region = new Region(name);
            _regions.Add(region);
            _window.rootVisualElement.Add(region);
            return region;
        }
        private Region AddChild(Model owner, VisualElement parent)
        {
            var child = new Region(Main);
            _regions.Add(child);
            parent.Add(child);
            owner.AddRegion(child);
            return child;
        }
        private static RegionManager Manager(Region[] regions)
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
        private UniTask<NavigationResult> Request(string name, NavigationPolicy policy = NavigationPolicy.Fifo,
            NavigationEntryMode mode = NavigationEntryMode.Default, Region[] regions = null)
        {
            Region[] targets = regions ?? new[] { _main };
            return Track(_navigation.NavigateToResult(Manager(targets), Id(name), ((IRegion)targets[0]).RegionName,
                NavigationParameters.Empty.Add(Input, name), policy, mode));
        }
        private UniTask<NavigationResult> Clear(NavigationPolicy policy = NavigationPolicy.Fifo, Region[] regions = null)
        {
            Region[] targets = regions ?? new[] { _main };
            return Track(_navigation.ClearResult(Manager(targets), ((IRegion)targets[0]).RegionName, policy));
        }
        private UniTask<NavigationResult> Release(params Region[] regions) => Track(_navigation.TeardownResult(regions, _ => true));
        private UniTask<NavigationResult> Track(UniTask<NavigationResult> task)
        {
            UniTask<NavigationResult> preserved = Record().Preserve();
            _tasks.Add(preserved);
            return preserved;
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
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded), "Never block the Unity main thread.");
            return task.GetAwaiter().GetResult();
        }
        private static void Require(UniTask<NavigationResult> task, NavigationStatus status)
        {
            NavigationResult result = Done(task);
            Assert.That(result.Status, Is.EqualTo(status), result.Error?.ToString());
        }
        private static IEnumerable<Exception> Flatten(Exception error) => error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : new[] { error };
        private static VisualElement View(Region region, string name)
        {
            Assert.That(region.TryGetView(Id(name), out VisualElement view), Is.True);
            return view;
        }
        private static Model Current(Region region, string name) => (Model)View(region, name).dataSource;
        private static void AssertSingle(Region region, string name)
        {
            Assert.That(region.ActiveViews, Is.EqualTo(new[] { Id(name) }));
            Assert.That(View(region, name).panel, Is.Not.Null);
            Assert.That(Current(region, name).Current.IsValid, Is.True);
        }
        private static void AssertReleased(Region region)
        {
            Assert.That(region.ActiveViews, Is.Empty);
            Assert.That(region.CachedViews, Is.Empty);
            Assert.That(region.OwnedEntries, Is.Empty);
            Assert.That(region.OwnedModels, Is.Empty);
            Assert.That(region.childCount, Is.Zero);
        }

        [TearDown]
        public void TearDown()
        {
            int primaryCount = _trace.Count;
            string primary = Snapshot();
            try
            {
                _reuse = null;
                _created = null;
                foreach (Model model in _models)
                {
                    model.OnTo = null;
                    model.OnAway = null;
                    model.FinalProbe = null;
                }
                foreach (UniTaskCompletionSource gate in _gates)
                {
                    gate.TrySetResult();
                }
                foreach (UniTask<NavigationResult> task in _tasks)
                {
                    Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
                }
                // Harness cleanup is explicit and separate from primary API evidence, including rejected configurations.
                foreach (Model model in _models)
                {
                    UniTask end = model.EndActivationAsync();
                    Assert.That(end.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
                    end.GetAwaiter().GetResult();
                    model.Dispose();
                }
            }
            finally
            {
                _window.Close();
                Directory.CreateDirectory("Logs/navigation-phase5");
                string name = Regex.Replace(TestContext.CurrentContext.Test.Name, "[^a-zA-Z0-9._-]", "_");
                File.WriteAllText("Logs/navigation-phase5/" + name + ".txt", "test=" + TestContext.CurrentContext.Test.Name +
                    "\noutcome=" + TestContext.CurrentContext.Result.Outcome.Status + "\nprimaryTraceCount=" + primaryCount +
                    "\nPRIMARY MODELS\n" + primary + "\nTRACE\n" + string.Join("\n", _trace) + "\nFINAL MODELS\n" + Snapshot() + "\n");
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
        private string Snapshot() => string.Join("\n", _models.Select(model => model.Name + ":model=" + RuntimeHelpers.GetHashCode(model) +
            ":To=" + model.ToCount + ":Away=" + model.AwayCount + ":Cleanup=" + model.Cleanups +
            ":FinalDispose=" + model.Disposals + ":IsDisposed=" + model.IsDisposed));
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
            private readonly NavigationTeardownTests _fixture;
            internal Bridge(NavigationTeardownTests fixture)
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
                if (_fixture._reuse != null)
                {
                    return _fixture._reuse;
                }
                Model model = type == typeof(A) ? new A(_fixture) : type == typeof(B) ? new B(_fixture) : new C(_fixture);
                _fixture._models.Add(model);
                _fixture._created?.Invoke(model);
                return model;
            }
        }
        private sealed class Service : NavigationService
        {
            private readonly NavigationTeardownTests _fixture;
            internal Service(NavigationTeardownTests fixture, IViewModelConstructionService construction, Internal.IViewIdRegistry registry)
                : base(construction, registry, null)
            {
                _fixture = fixture;
            }
            internal override VisualElement PrepareView(ViewIdAsset asset, BaseViewModel model)
            {
                VisualElement view = base.PrepareView(asset, model);
                if (_fixture._failPreparation)
                {
                    throw _fixture._fault;
                }
                return view;
            }
        }
        private sealed class FaultRegion : Region, IRegion
        {
            private readonly Exception _fault;
            internal bool Fail;
            internal bool AfterMount;
            internal FaultRegion(RegionName name, Exception fault) : base(name)
            {
                _fault = fault;
            }
            void IRegion.AddView(ViewId id, VisualElement view)
            {
                if (Fail && !AfterMount)
                {
                    throw _fault;
                }
                base.AddView(id, view);
                if (Fail)
                {
                    throw _fault;
                }
            }
        }
        private abstract class Model : BaseViewModel
        {
            private readonly NavigationTeardownTests _fixture;
            internal readonly string Name;
            internal Func<NavigationActivation, UniTask> OnTo;
            internal Func<UniTask> OnAway;
            internal Action FinalProbe;
            internal int ToCount;
            internal int AwayCount;
            internal int Cleanups;
            internal int Disposals;
            internal int Publications;
            private NavigationActivation _registered;
            internal NavigationActivation Current => Activation;
            internal IViewModelNavigationService Handle => Navigation;
            protected Model(string name, NavigationTeardownTests fixture)
            {
                Name = name;
                _fixture = fixture;
                Disposables.Add(new Resource(() =>
                {
                    Disposals++;
                    _fixture._trace.Add(Name + ":final-dispose");
                    FinalProbe?.Invoke();
                }));
            }
            internal void OwnFinal(IDisposable resource) => Disposables.Add(resource);
            internal UniTask<NavigationResult> ScopedRelease(params Region[] regions) => Navigation.TeardownResult(regions, _ => true);
            protected override async UniTask NavigateTo(NavigationParameters parameters)
            {
                ToCount++;
                if (!ReferenceEquals(_registered, Activation))
                {
                    _registered = Activation;
                    Activation.Own(new Resource(() =>
                    {
                        Cleanups++;
                        _fixture._trace.Add(Name + ":activation-cleanup");
                    }));
                }
                _fixture._trace.Add(Name + ":To:lifetime=" + RuntimeHelpers.GetHashCode(Activation));
                if (OnTo != null)
                {
                    await OnTo(Activation);
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
            internal A(NavigationTeardownTests fixture) : base("A", fixture)
            {
            }
        }
        private sealed class B : Model
        {
            internal B(NavigationTeardownTests fixture) : base("B", fixture)
            {
            }
        }
        private sealed class C : Model
        {
            internal C(NavigationTeardownTests fixture) : base("C", fixture)
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
            public UniTask NavigateTo(IRegionManager manager, ViewId id, RegionName region, NavigationParameters parameters) => throw new Exception("Must not dispatch.");
            public UniTask Clear(IRegionManager manager, RegionName region) => throw new Exception("Must not dispatch.");
        }
        public sealed class TestWindow : EditorWindow
        {
        }
    }
}
