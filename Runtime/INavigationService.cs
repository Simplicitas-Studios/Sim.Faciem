using Cysharp.Threading.Tasks;

namespace Sim.Faciem
{
    public interface INavigationService
    {
        UniTask NavigateTo(IRegionManager regionManager, ViewId viewId, RegionName regionName,
            NavigationParameters parameters);

        UniTask Clear(IRegionManager regionManager, RegionName regionName);
    }
}
