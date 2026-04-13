using Cysharp.Threading.Tasks;
using Sim.Faciem;

namespace Plugins.Sim.Faciem.Editor.Navigation
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
            return _navigationService.NavigateTo(_editorWindow.RegionManager, viewId, region);
        }

        public UniTask Clear(RegionName region)
        {
            return _navigationService.Clear(_editorWindow.RegionManager, region);
        }
    }
}