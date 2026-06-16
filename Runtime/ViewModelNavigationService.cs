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
            return _navigationService.NavigateTo(_baseViewModel.RegionManager, viewId, region, parameters);
        }

        public UniTask Clear(RegionName regionName)
        {
            return _navigationService.Clear(_baseViewModel.RegionManager, regionName);
        }
    }
}
