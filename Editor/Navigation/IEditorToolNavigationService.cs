using Cysharp.Threading.Tasks;
using Sim.Faciem;

namespace Plugins.Sim.Faciem.Editor.Navigation
{
    public interface IEditorToolNavigationService
    {
        UniTask Navigate(ViewId viewId, RegionName region);
        
        UniTask Clear(RegionName region);
    }
}