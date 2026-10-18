using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using InternalEditor;
using NUnit.Framework;
using R3;
using Sim.Faciem.Internal;
using Sim.Faciem.InternalEditor;
using Sim.Utility;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sim.Faciem.Tests
{
    public sealed class FaciemCompatibilityTests
    {
        [Test]
        public void SerializedTypeNameResolvesWithoutAnEditorScript()
        {
            var reference = new MonoScriptReference { TypeName = typeof(string).AssemblyQualifiedName };

            Assert.That(reference.Script, Is.Null);
            Assert.That(reference.GetReferencedType(), Is.EqualTo(typeof(string)));
        }

        [TestCase("")]
        [TestCase("Missing.Type")]
        public void UnknownSerializedTypeNameReturnsNull(string typeName)
        {
            var reference = new MonoScriptReference { TypeName = typeName };

            Assert.That(reference.GetReferencedType(), Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NavigationUsesSerializedTypeResolutionAndPreservesCachedOwnership(bool cached)
        {
            ViewIdAsset asset = ScriptableObject.CreateInstance<ViewIdAsset>();
            var model = new RecordingViewModel();
            try
            {
                asset.ViewId = ViewId.From("u1-compatibility");
                asset.DataContext = new MonoScriptReference { TypeName = typeof(string).AssemblyQualifiedName };
                var region = new CachedRegion(asset.ViewId, model);
                var manager = new RecordingRegionManager(cached ? new IRegion[] { region } : Array.Empty<IRegion>());
                var factory = new RecordingFactory(model);
                var service = new NavigationService(factory, new SingleViewRegistry(asset), null);
                model.Setup(manager, service);
                NavigationParameters parameters = NavigationParameters.Empty;

                service.NavigateTo(manager, asset.ViewId, RegionName.From("u1"), parameters).GetAwaiter().GetResult();

                Assert.That(factory.Calls, Is.EqualTo(cached ? 0 : 1));
                Assert.That(factory.RequestedType, Is.EqualTo(cached ? null : typeof(string)));
                Assert.That(model.NavigateCount, Is.EqualTo(1));
                Assert.That(model.Parameters, Is.SameAs(parameters));
                Assert.That(model.RegionManager.Parent.IsSome, Is.True);
                Assert.That(model.RegionManager.Parent.Value, Is.SameAs(manager));
                Assert.That(asset.DataContext.Script, Is.Null);
            }
            finally
            {
                model.Dispose();
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MissingSettingsHelpersDoNotCreateConfiguration()
        {
            Assert.That(AddressableAssetSettingsDefaultObject.Settings, Is.Null,
                "This prerequisite fixture requires the isolated consumer without Addressables settings.");

            Assert.That(AddressableHelper.GetGroup("u1"), Is.Null);
            Assert.That(AddressableHelper.CreateGroup("u1"), Is.Null);
            Assert.That(AddressableHelper.GroupExists("u1"), Is.False);
            Assert.That(AddressableAssetSettingsDefaultObject.Settings, Is.Null);
        }

        [Test]
        public void MissingSettingsPersistentAssetPathsAreSafe()
        {
            Assert.That(AddressableAssetSettingsDefaultObject.Settings, Is.Null);
            string path = "Assets/U1Compatibility-" + Guid.NewGuid().ToString("N") + ".asset";
            RegionNameDefinition asset = ScriptableObject.CreateInstance<RegionNameDefinition>();
            try
            {
                AssetDatabase.CreateAsset(asset, path);

                Assert.That(AddressableHelper.CreateAssetEntry(asset), Is.Null);
                Assert.That(AddressableHelper.CreateAssetEntry(asset, "u1-label"), Is.Null);
                Assert.That(AddressableHelper.CreateAssetEntry(asset, "u1-group", "u1-label"), Is.Null);
                Assert.That(asset.GetAddressableAssetEntry(), Is.Null);
                Assert.DoesNotThrow(() => asset.AddAddressableAssetLabel("u1-label"));
                Assert.DoesNotThrow(() => asset.RemoveAddressableAssetLabel("u1-label"));
                Assert.DoesNotThrow(() => asset.SetAddressableAssetAddress("u1-address"));
                Assert.DoesNotThrow(() => asset.SetAddressableAssetGroup("u1-group"));
                Assert.DoesNotThrow(() => asset.SetAddressableAssetGroup((AddressableAssetGroup)null));
                Assert.That(AddressableAssetSettingsDefaultObject.Settings, Is.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }
        }

        private sealed class RecordingViewModel : BaseViewModel
        {
            public int NavigateCount { get; private set; }

            public NavigationParameters Parameters { get; private set; }

            protected override UniTask NavigateTo(NavigationParameters parameters)
            {
                NavigateCount++;
                Parameters = parameters;
                return UniTask.CompletedTask;
            }
        }

        private sealed class RecordingFactory : IViewModelConstructionService
        {
            private readonly BaseViewModel _model;

            public RecordingFactory(BaseViewModel model)
            {
                _model = model;
            }

            public int Calls { get; private set; }

            public Type RequestedType { get; private set; }

            public BaseViewModel CreateInstance(Type type)
            {
                Calls++;
                RequestedType = type;
                return _model;
            }

            public T CreateInstance<T>() where T : BaseViewModel
            {
                throw new NotSupportedException();
            }
        }

        private sealed class SingleViewRegistry : IViewIdRegistry
        {
            private readonly ViewIdAsset _asset;

            public SingleViewRegistry(ViewIdAsset asset)
            {
                _asset = asset;
            }

            public bool TryGetViewId(ViewId viewId, out ViewIdAsset viewIdAsset)
            {
                viewIdAsset = _asset;
                return _asset.ViewId.Equals(viewId);
            }
        }

        private sealed class RecordingRegionManager : IViewModelRegionManager
        {
            private readonly IReadOnlyList<IRegion> _regions;

            public RecordingRegionManager(IReadOnlyList<IRegion> regions)
            {
                _regions = regions;
            }

            public Maybe<IRegionManager> Parent { get; set; }

            public bool TryFindRegion(RegionName regionName, out IReadOnlyList<IRegion> regionList)
            {
                regionList = _regions;
                return true;
            }

            public void AddRegion(IRegion region)
            {
                throw new NotSupportedException();
            }

            public void RemoveRegion(IRegion region)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class CachedRegion : IRegion
        {
            private readonly VisualElement _view;

            public CachedRegion(ViewId viewId, BaseViewModel model)
            {
                ActiveViews = new[] { viewId };
                _view = new VisualElement { dataSource = model };
            }

            public bool SupportMultipleViews => false;

            public RegionName RegionName => RegionName.From("u1");

            public IReadOnlyList<ViewId> ActiveViews { get; }

            public Observable<Unit> Destroyed => Observable.Never<Unit>();

            public bool TryGetView(ViewId viewId, out VisualElement view)
            {
                view = _view;
                return true;
            }

            public void AddView(ViewId viewId, VisualElement view)
            {
                throw new NotSupportedException();
            }

            public void ActivateView(ViewId viewId)
            {
                throw new NotSupportedException();
            }

            public void DeactivateView(ViewId viewId)
            {
                throw new NotSupportedException();
            }

            public void DeactivateAllViews()
            {
                throw new NotSupportedException();
            }
        }
    }
}
