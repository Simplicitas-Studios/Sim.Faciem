using Cysharp.Threading.Tasks;

namespace Sim.Faciem
{
    /// <summary>
    /// External navigation entry point. Faciem orders operations FIFO by resolved Region identity.
    /// Lifecycle callbacks must use their ViewModel's scoped Navigation handle, not await this raw service
    /// on a Region owned by that callback.
    /// </summary>
    public interface INavigationService
    {
        UniTask NavigateTo(IRegionManager regionManager, ViewId viewId, RegionName regionName,
            NavigationParameters parameters);

        UniTask Clear(IRegionManager regionManager, RegionName regionName);
    }
}
