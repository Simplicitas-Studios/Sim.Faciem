using Cysharp.Threading.Tasks;

namespace Sim.Faciem
{
    public interface IViewModelNavigationService
    {
        UniTask Navigate(ViewId viewId, RegionName region);
        
        UniTask Navigate(ViewId viewId, RegionName region, NavigationParameters parameters);

        UniTask Clear(RegionName region);
    }
}
