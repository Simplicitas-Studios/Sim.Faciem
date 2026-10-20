using Cysharp.Threading.Tasks;

namespace Sim.Faciem
{
    public class ViewModelNavigationService : IViewModelNavigationService
    {
        private readonly BaseViewModel _baseViewModel;
        private readonly INavigationService _navigationService;

        public ViewModelNavigationService(BaseViewModel baseViewModel, INavigationService navigationService)
        {
            _baseViewModel = baseViewModel;
            _navigationService = navigationService;
        }

        public UniTask Navigate(ViewId viewId, RegionName region)
        {
            return Navigate(viewId, region, NavigationParameters.Empty);
        }

        public UniTask Navigate(ViewId viewId, RegionName region, NavigationParameters parameters)
        {
            return NavigateWithPolicy(viewId, region, parameters, NavigationPolicy.Fifo);
        }

        internal UniTask NavigateWithPolicy(ViewId viewId, RegionName region, NavigationParameters parameters,
            NavigationPolicy policy, NavigationEntryMode entryMode = NavigationEntryMode.Default)
        {
            Internal.RegionNavigationOperations.Validate(policy);
            NavigationPolicyExtensions.ValidateEntryMode(entryMode);
            if (_navigationService is NavigationService navigationService)
            {
                return navigationService.NavigateToFrom(_baseViewModel, _baseViewModel.RegionManager,
                    viewId, region, parameters, policy, entryMode);
            }
            if (policy != NavigationPolicy.Fifo || entryMode != NavigationEntryMode.Default)
            {
                throw new System.NotSupportedException("This navigation service does not support the requested policy or entry mode.");
            }
            return _navigationService.NavigateTo(_baseViewModel.RegionManager, viewId, region, parameters);
        }

        internal UniTask<NavigationResult> NavigateResult(ViewId viewId, RegionName region,
            NavigationParameters parameters, NavigationPolicy policy, NavigationEntryMode entryMode)
        {
            return _navigationService is NavigationService native
                ? native.NavigateResultFrom(_baseViewModel, _baseViewModel.RegionManager, viewId, region, parameters, policy, entryMode)
                : UniTask.FromResult(Internal.NavigationExecution.Unsupported("Custom navigation services do not advertise result/recovery support."));
        }

        internal UniTask<NavigationResult> ClearResult(RegionName region, NavigationPolicy policy)
        {
            return _navigationService is NavigationService native
                ? native.ClearResultFrom(_baseViewModel, _baseViewModel.RegionManager, region, policy)
                : UniTask.FromResult(Internal.NavigationExecution.Unsupported("Custom navigation services do not advertise result/recovery support."));
        }

        internal UniTask<NavigationResult> TeardownResult(System.Collections.Generic.IReadOnlyList<Region> regions,
            System.Func<BaseViewModel, bool> ownsModel)
        {
            return _navigationService is NavigationService native
                ? native.TeardownFrom(_baseViewModel, regions, ownsModel)
                : UniTask.FromResult(Internal.NavigationExecution.Unsupported("Custom navigation services do not support final teardown."));
        }

        public UniTask Clear(RegionName regionName)
        {
            return ClearWithPolicy(regionName, NavigationPolicy.Fifo);
        }

        internal UniTask ClearWithPolicy(RegionName regionName, NavigationPolicy policy)
        {
            Internal.RegionNavigationOperations.Validate(policy);
            if (_navigationService is NavigationService navigationService)
            {
                return navigationService.ClearFrom(_baseViewModel, _baseViewModel.RegionManager, regionName, policy);
            }
            if (policy != NavigationPolicy.Fifo)
            {
                throw new System.NotSupportedException("This navigation service does not support LatestRequestWins.");
            }
            return _navigationService.Clear(_baseViewModel.RegionManager, regionName);
        }
    }
}
