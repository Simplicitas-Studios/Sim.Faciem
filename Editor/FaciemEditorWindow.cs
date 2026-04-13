using System;
using Cysharp.Threading.Tasks;
using Plugins.Sim.Faciem.Editor.DI;
using Plugins.Sim.Faciem.Editor.Navigation;
using Sim.Faciem;
using UnityEditor;
using UnityEngine;

namespace Plugins.Sim.Faciem.Editor
{
    public abstract class FaciemEditorWindow : EditorWindow, IRegionManagerOwner
    {
        [SerializeField]
        private RegionNameDefinition _windowRegionName;

        [SerializeField]
        private EditorViewIdAsset _initialViewId;

        private RegionManager _regionManager = new();

        RegionManager IRegionManagerOwner.RegionManager => _regionManager;

        protected RegionName WindowRegionName => _windowRegionName.Name;
        
        protected IEditorToolNavigationService Navigation { get; private set; }
        
        private async UniTaskVoid CreateGUI()
        {
            rootVisualElement.dataSource = this;
            var navigationService = EditorInjector.Instance.ResolveInstance<INavigationService>();
            Navigation = new EditorToolNavigationService(this, navigationService);
            
            var region = new Region
            {
                RegionName = _windowRegionName,
                style =
                {
                    flexGrow = 1
                }
            };

            rootVisualElement.Add(region);
            _regionManager.AddRegion(region);

            await Navigation.Navigate(_initialViewId.ViewId, WindowRegionName);

            await NavigateTo();
        }

        protected virtual UniTask NavigateTo()
        {
            return UniTask.CompletedTask;
        }

        private async UniTaskVoid OnDisable()
        {
            await NavigateAway();
        }
        
        protected virtual UniTask NavigateAway()
        {
            return UniTask.CompletedTask;
        }
    }
}