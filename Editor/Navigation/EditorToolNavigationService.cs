using Cysharp.Threading.Tasks;

namespace Sim.Faciem.Editor.Navigation
{
    public class EditorToolNavigationService : IEditorToolNavigationService
    {
        private readonly IRegionManagerOwner _editorWindow;
        private readonly INavigationService _navigationService;

        public EditorToolNavigationService(IRegionManagerOwner editorWindow, INavigationService navigationService)
        {
            _editorWindow = editorWindow;
            _navigationService = navigationService;
        }

        public UniTask Navigate(ViewId viewId, RegionName region)
        {
            return Navigate(viewId, region, NavigationParameters.Empty);
        }
        
        public UniTask Navigate(ViewId viewId, RegionName region, NavigationParameters navigationParameters)
        {
            return _navigationService.NavigateTo(_editorWindow.RegionManager, viewId, region, navigationParameters);
        }

        public UniTask Clear(RegionName region)
        {
            return _navigationService.Clear(_editorWindow.RegionManager, region);
        }
    }
}
