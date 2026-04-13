using Cysharp.Threading.Tasks;

namespace Sim.Faciem
{
    public interface IViewModelNavigationService
    {
        UniTask Navigate(ViewId viewId, RegionName region);
        
        UniTask Clear(RegionName region);
    }
}