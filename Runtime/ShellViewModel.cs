using Cysharp.Threading.Tasks;

namespace Sim.Faciem
{
    public class ShellViewModel : ViewModel<ShellViewModel>
    {
        public ShellViewModel()
        {
            
        }

        protected override async UniTask NavigateTo()
        {
            await Navigation.Navigate(FaciemBridge.RootViewId, WellKnownRegionNames.MainRegion);
        }
    }
}