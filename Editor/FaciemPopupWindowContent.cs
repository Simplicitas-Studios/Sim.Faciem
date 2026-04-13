using Cysharp.Threading.Tasks;
using Plugins.Sim.Faciem.Editor.DI;
using Plugins.Sim.Faciem.Editor.Navigation;
using Plugins.Sim.Faciem.Shared;
using R3;
using Sim.Faciem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Plugins.Sim.Faciem.Editor
{
    public abstract class FaciemPopupWindowContent : PopupWindowContent, IRegionManagerOwner
    {
        private readonly RegionManager _regionManager;

        RegionManager IRegionManagerOwner.RegionManager => _regionManager;

        protected IEditorToolNavigationService Navigation { get; }
        
        protected abstract RegionName RegionName { get; }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(300, 300);
        }

        public FaciemPopupWindowContent()
        {
            _regionManager = new RegionManager();
            var navigationService = EditorInjector.Instance.ResolveInstance<INavigationService>();
            Navigation = new EditorToolNavigationService(this, navigationService);
        }

        public override VisualElement CreateGUI()
        {
            var root = new VisualElement
            {
                style = { width = 200, height = 200 }
            };
            
            var region = new Region(RegionName);

            var disposables = region.RegisterDisposableBag();
            _regionManager.AddRegion(region);
            disposables.Add(Disposable.Create(() =>
            {
                _regionManager.RemoveRegion(region);
            }));            
           
            root.Add(region);
            
            return root;
        }

        public sealed override void OnOpen()
        {
            UniTask.Defer(NavigateToPopup).Forget();
        }

        public sealed override void OnClose()
        {
            UniTask.Defer(NavigateAwayPopup).Forget();
        }
        
        protected virtual UniTask NavigateToPopup()
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask NavigateAwayPopup()
        {
            return UniTask.CompletedTask;
        }
    }
}