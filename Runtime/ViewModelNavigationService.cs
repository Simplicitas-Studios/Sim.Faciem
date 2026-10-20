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
            NavigationPolicy policy)
        {
            Internal.RegionNavigationOperations.Validate(policy);
            if (_navigationService is NavigationService navigationService)
            {
                return navigationService.NavigateToFrom(_baseViewModel, _baseViewModel.RegionManager,
                    viewId, region, parameters, policy);
            }
            if (policy != NavigationPolicy.Fifo)
            {
                throw new System.NotSupportedException("This navigation service does not support LatestRequestWins.");
            }
            return _navigationService.NavigateTo(_baseViewModel.RegionManager, viewId, region, parameters);
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
