using Cysharp.Threading.Tasks;
using Plugins.Sim.Faciem.Editor.DI;
using Plugins.Sim.Faciem.Editor.Navigation;
using Plugins.Sim.Faciem.Shared;
using R3;
using Sim.Faciem;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace Plugins.Sim.Faciem.Editor
{
    public abstract class FaciemToolbarOverlay : ToolbarOverlay, IRegionManagerOwner
    {
        private readonly RegionManager _regionManager;

        RegionManager IRegionManagerOwner.RegionManager => _regionManager;

        protected IEditorToolNavigationService Navigation { get; }

        protected abstract RegionName PanelRegionName { get; }
        
        protected abstract RegionName ToolbarRegionName { get; }
        
        protected FaciemToolbarOverlay()
        {
            _regionManager = new RegionManager();
            var navigationService = EditorInjector.Instance.ResolveInstance<INavigationService>();
            Navigation = new EditorToolNavigationService(this, navigationService);
        }

        protected virtual VisualElement CreateRootElement()
        {
            return new VisualElement();
        }
        
        public sealed override VisualElement CreatePanelContent()
        {
            var root = CreateRootElement();
            var region = new Region(PanelRegionName);

            var disposables = region.RegisterDisposableBag();
            _regionManager.AddRegion(region);
            disposables.Add(Disposable.Create(() =>
            {
                _regionManager.RemoveRegion(region);
                UniTask.Defer(NavigateAwayPanel).Forget();
            }));            
            
            UniTask.Defer(NavigateToPanel).Forget();

            root.Add(region);
            return root;
        }

        public override void OnCreated()
        {
            collapsed = false;
            var region = new Region(ToolbarRegionName);
            _regionManager.AddRegion(region);
            rootVisualElement[0].Add(region);
            
            UniTask.Defer(NavigateToToolbar).Forget();
        }
        
        protected virtual UniTask NavigateToToolbar()
        {
            return UniTask.CompletedTask;
        }
        
        protected virtual UniTask NavigateToPanel()
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask NavigateAwayPanel()
        {
            return UniTask.CompletedTask;
        }
    }
}