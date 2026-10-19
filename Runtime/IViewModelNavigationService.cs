using Cysharp.Threading.Tasks;

namespace Sim.Faciem
{
    /// <summary>
    /// ViewModel-scoped navigation. Use this handle inside lifecycle callbacks so Faciem can reject
    /// same-Region reentrancy and dependency cycles, including after asynchronous work.
    /// </summary>
    public interface IViewModelNavigationService
    {
        UniTask Navigate(ViewId viewId, RegionName region);

        UniTask Navigate(ViewId viewId, RegionName region, NavigationParameters parameters);

        UniTask Clear(RegionName region);
    }
}
