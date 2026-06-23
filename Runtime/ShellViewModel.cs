using Cysharp.Threading.Tasks;

namespace Sim.Faciem
{
    public class ShellViewModel : ViewModel<ShellViewModel>
    {
        protected override async UniTask NavigateTo(NavigationParameters navigationParameters)
        {
            var initialViewId = navigationParameters.Get(WellKnownShellNavigationParameters.InitialViewId);

            if (initialViewId.HasValue)
            {
                await Navigation.Navigate(initialViewId.Value, WellKnownRegionNames.MainRegion, navigationParameters);
            }
        }
    }
}
