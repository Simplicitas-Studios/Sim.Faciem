using Cysharp.Threading.Tasks;

namespace Sim.Faciem.Editor.Navigation
{
    public interface IEditorToolNavigationService
    {
        UniTask Navigate(ViewId viewId, RegionName region, NavigationParameters navigationParameters);
        
        UniTask Navigate(ViewId viewId, RegionName region);

        UniTask Clear(RegionName region);
    }
}
