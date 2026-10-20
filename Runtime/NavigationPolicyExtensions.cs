using System;
using Cysharp.Threading.Tasks;
using Sim.Faciem.Internal;

namespace Sim.Faciem
{
    /// <summary>Opt-in policy/entry overloads; existing interface signatures and FIFO calls remain unchanged.</summary>
    public static class NavigationPolicyExtensions
    {
        internal static void ValidateEntryMode(NavigationEntryMode entryMode)
        {
            if (entryMode != NavigationEntryMode.Default && entryMode != NavigationEntryMode.ReenterIfActive)
            {
                throw new ArgumentOutOfRangeException(nameof(entryMode));
            }
        }

        public static UniTask NavigateTo(this INavigationService service, IRegionManager manager, ViewId viewId,
            RegionName region, NavigationParameters parameters, NavigationPolicy policy)
        {
            return NavigateTo(service, manager, viewId, region, parameters, policy, NavigationEntryMode.Default);
        }

        /// <summary>Explicitly combines ordering/supersession and entry behavior without application identity inference.</summary>
        public static UniTask NavigateTo(this INavigationService service, IRegionManager manager, ViewId viewId,
            RegionName region, NavigationParameters parameters, NavigationPolicy policy, NavigationEntryMode entryMode)
        {
            RegionNavigationOperations.Validate(policy);
            ValidateEntryMode(entryMode);
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (service is NavigationService native)
            {
                return native.NavigateToFrom(null, manager, viewId, region, parameters, policy, entryMode);
            }
            if (policy == NavigationPolicy.Fifo && entryMode == NavigationEntryMode.Default)
            {
                return service.NavigateTo(manager, viewId, region, parameters);
            }
            throw new NotSupportedException("This navigation service does not support the requested policy or entry mode.");
        }

        public static UniTask Clear(this INavigationService service, IRegionManager manager, RegionName region,
            NavigationPolicy policy)
        {
            RegionNavigationOperations.Validate(policy);
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (service is NavigationService native)
            {
                return native.ClearFrom(null, manager, region, policy);
            }
            if (policy == NavigationPolicy.Fifo)
            {
                return service.Clear(manager, region);
            }
            throw new NotSupportedException("This navigation service does not support LatestRequestWins.");
        }

        public static UniTask Navigate(this IViewModelNavigationService service, ViewId id, RegionName region,
            NavigationPolicy policy)
        {
            return Navigate(service, id, region, NavigationParameters.Empty, policy);
        }

        public static UniTask Navigate(this IViewModelNavigationService service, ViewId id, RegionName region,
            NavigationParameters parameters, NavigationPolicy policy)
        {
            return Navigate(service, id, region, parameters, policy, NavigationEntryMode.Default);
        }

        /// <summary>Explicit scoped entry mode; lifecycle-origin calls retain the existing reentrancy restrictions.</summary>
        public static UniTask Navigate(this IViewModelNavigationService service, ViewId id, RegionName region,
            NavigationParameters parameters, NavigationPolicy policy, NavigationEntryMode entryMode)
        {
            RegionNavigationOperations.Validate(policy);
            ValidateEntryMode(entryMode);
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (service is ViewModelNavigationService native)
            {
                return native.NavigateWithPolicy(id, region, parameters, policy, entryMode);
            }
            if (policy == NavigationPolicy.Fifo && entryMode == NavigationEntryMode.Default)
            {
                return service.Navigate(id, region, parameters);
            }
            throw new NotSupportedException("This scoped navigation service does not support the requested policy or entry mode.");
        }

        public static UniTask Clear(this IViewModelNavigationService service, RegionName region, NavigationPolicy policy)
        {
            RegionNavigationOperations.Validate(policy);
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (service is ViewModelNavigationService native)
            {
                return native.ClearWithPolicy(region, policy);
            }
            if (policy == NavigationPolicy.Fifo)
            {
                return service.Clear(region);
            }
            throw new NotSupportedException("This scoped navigation service does not support LatestRequestWins.");
        }
    }
}
