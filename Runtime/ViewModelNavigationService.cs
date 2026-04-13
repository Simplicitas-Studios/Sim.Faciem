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
            return _navigationService.NavigateTo(_baseViewModel.RegionManager, viewId, region);
        }

        public UniTask Clear(RegionName regionName)
        {
            return _navigationService.Clear(_baseViewModel.RegionManager, regionName);
        }
    }
}