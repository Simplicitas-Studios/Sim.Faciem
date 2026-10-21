using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Sim.Faciem.Tests
{
    // Validation-only: copied into the native friend test assembly after importing the unchanged sample into Assets.
    public sealed class F05ImportedTypedSampleTests
    {
        private Window _window;
        private Region _region;
        private BaseViewModel _model;
        private INavigationService _navigation;
        private ViewIdAsset _asset;
        private EditorWindow _previous;

        [UnityTest]
        public IEnumerator Imported_sample_executes_actual_constructor_binding_reentry_USS_and_final_release()
        {
            Type modelType = Type.GetType("Sim.Faciem.Samples.SelectedItemViewModel, Assembly-CSharp", true);
            Type viewType = Type.GetType("Sim.Faciem.Samples.SelectedItemView, Assembly-CSharp", true);
            _model = (BaseViewModel)Activator.CreateInstance(modelType);
            var bridge = new Bridge { Model = _model };
            _asset = ScriptableObject.CreateInstance<ViewIdAsset>();
            _asset.ViewId = ViewId.From("f05/imported-sample");
            _asset.View = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/F05ImportedSample/SelectedItem.uxml");
            _asset.DataContext = new MonoScriptReference { TypeName = modelType.AssemblyQualifiedName };
            _asset.ViewModel = new MonoScriptReference { TypeName = modelType.AssemblyQualifiedName };
            _asset.TypedView = new MonoScriptReference { TypeName = viewType.AssemblyQualifiedName };
            _navigation = bridge.Navigation = new NavigationService(new ViewModelConstructionService(bridge), new Registry { Asset = _asset }, null);
            _previous = EditorWindow.focusedWindow;
            _window = ScriptableObject.CreateInstance<Window>();
            _window.Show();
            RegionName name = RegionName.From("f05/sample");
            _region = new Region(name);
            _window.rootVisualElement.Add(_region);
            var manager = new RegionManager();
            manager.AddRegion(_region);
            yield return null;
            Assert.That(_region.panel, Is.Not.Null);
            Assert.That(_asset.View, Is.Not.Null);
            Assert.That(_navigation.NavigateToResult(manager, _asset.ViewId, name, NavigationParameters.Empty).GetAwaiter().GetResult().Status,
                Is.EqualTo(NavigationStatus.Succeeded));
            VisualElement root = _region.OwnedEntries.Single().View;
            View behavior = _region.OwnedEntries.Single().TypedView;
            Assert.That(behavior.GetType(), Is.EqualTo(viewType));
            Assert.That(behavior.Model, Is.SameAs(_model));
            var selected = (ReactiveProperty<int>)modelType.GetProperty("SelectedIndex").GetValue(_model);
            Assert.That(root.Q<Label>("item-0").ClassListContains("item--selected"), Is.True);
            selected.Value = 3;
            Assert.That(root.Q<Label>("item-0").ClassListContains("item--selected"), Is.False);
            Assert.That(root.Q<Label>("item-3").ClassListContains("item--selected"), Is.True);
            Assert.That(_navigation.ClearResult(manager, name).GetAwaiter().GetResult().Status, Is.EqualTo(NavigationStatus.Succeeded));
            selected.Value = 1;
            Assert.That(root.Q<Label>("item-3").ClassListContains("item--selected"), Is.True, "Inactive controller is unsubscribed.");
            Assert.That(_navigation.NavigateToResult(manager, _asset.ViewId, name, NavigationParameters.Empty).GetAwaiter().GetResult().Status,
                Is.EqualTo(NavigationStatus.Succeeded));
            Assert.That(_region.OwnedEntries.Single().TypedView, Is.SameAs(behavior));
            Assert.That(root.Q<Label>("item-1").ClassListContains("item--selected"), Is.True);
            Assert.That(root.Q<Label>("item-3").ClassListContains("item--selected"), Is.False);
            Assert.That(bridge.ModelResolutions, Is.EqualTo(1));
            Assert.That(_navigation.TeardownResult(new[] { _region }, model => ReferenceEquals(model, _model)).GetAwaiter().GetResult().Status,
                Is.EqualTo(NavigationStatus.Succeeded));
            Assert.That(_model.IsDisposed, Is.True);
            Assert.That(behavior.IsDisposed, Is.True);
            Assert.That(behavior.Model, Is.Null);
            Assert.That(behavior.VisualRoot, Is.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_navigation != null && _region != null)
            {
                NavigationResult result = _navigation.TeardownResult(new[] { _region }, model => ReferenceEquals(model, _model)).GetAwaiter().GetResult();
                Assert.That(result.Status == NavigationStatus.Succeeded || result.Status == NavigationStatus.NoOp, Is.True);
            }
            if (_model != null && !_model.IsDisposed)
            {
                // Fixture/DI candidate only, if preparation failed before native cache transfer.
                _model.Dispose();
            }
            if (_window != null)
            {
                _window.Close();
            }
            if (_asset != null)
            {
                UnityEngine.Object.DestroyImmediate(_asset);
            }
            if (_previous != null)
            {
                _previous.Focus();
            }
            yield return null;
        }

        private sealed class Window : EditorWindow { }
        private sealed class Registry : Internal.IViewIdRegistry
        {
            public ViewIdAsset Asset;
            public bool TryGetViewId(ViewId id, out ViewIdAsset asset)
            {
                asset = Asset;
                return id.Equals(asset.ViewId);
            }
        }
        private sealed class Bridge : IDIContainerBridge
        {
            public BaseViewModel Model;
            public INavigationService Navigation;
            public int ModelResolutions;
            public T ResolveInstance<T>() where T : class => (T)ResolveInstance(typeof(T));
            public object ResolveInstance(Type type)
            {
                if (type == typeof(INavigationService))
                {
                    return Navigation;
                }
                ModelResolutions++;
                return Model;
            }
        }
    }
}
