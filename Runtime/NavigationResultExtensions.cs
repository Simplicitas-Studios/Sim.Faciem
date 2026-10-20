using Cysharp.Threading.Tasks;
using Sim.Faciem.Internal;

namespace Sim.Faciem
{
    /// <summary>Opt-in outcomes and contained recovery through Faciem's existing Region queue.</summary>
    public static class NavigationResultExtensions
    {
        /// <summary>For external submissions. Lifecycle callbacks must use their protected scoped Navigation handle.</summary>
        public static UniTask<NavigationResult> NavigateToResult(this INavigationService service,
            IRegionManager manager, ViewId viewId, RegionName region, NavigationParameters parameters,
            NavigationPolicy policy = NavigationPolicy.Fifo, NavigationEntryMode entryMode = NavigationEntryMode.Default)
        {
            return service is NavigationService native
                ? native.NavigateResultFrom(null, manager, viewId, region, parameters, policy, entryMode)
                : UniTask.FromResult(NavigationExecution.Unsupported("Custom navigation services do not advertise result/recovery support."));
        }

        /// <summary>For external submissions. An empty owned Region returns NoOp after its queue drains.</summary>
        public static UniTask<NavigationResult> ClearResult(this INavigationService service,
            IRegionManager manager, RegionName region, NavigationPolicy policy = NavigationPolicy.Fifo)
        {
            return service is NavigationService native
                ? native.ClearResultFrom(null, manager, region, policy)
                : UniTask.FromResult(NavigationExecution.Unsupported("Custom navigation services do not advertise result/recovery support."));
        }

        /// <summary>Retains lifecycle-origin reentrancy/cycle detection across awaits.</summary>
        public static UniTask<NavigationResult> NavigateResult(this IViewModelNavigationService service,
            ViewId viewId, RegionName region, NavigationParameters parameters,
            NavigationPolicy policy = NavigationPolicy.Fifo, NavigationEntryMode entryMode = NavigationEntryMode.Default)
        {
            return service is ViewModelNavigationService scoped
                ? scoped.NavigateResult(viewId, region, parameters, policy, entryMode)
                : UniTask.FromResult(NavigationExecution.Unsupported("Custom scoped navigation services do not advertise result/recovery support."));
        }

        /// <summary>Retains lifecycle-origin reentrancy/cycle detection across awaits.</summary>
        public static UniTask<NavigationResult> ClearResult(this IViewModelNavigationService service,
            RegionName region, NavigationPolicy policy = NavigationPolicy.Fifo)
        {
            return service is ViewModelNavigationService scoped
                ? scoped.ClearResult(region, policy)
                : UniTask.FromResult(NavigationExecution.Unsupported("Custom scoped navigation services do not advertise result/recovery support."));
        }
    }
}
