using Cysharp.Threading.Tasks;
using Sim.Faciem.Internal;

namespace Sim.Faciem
{
    public static class FaciemBootstrapper
    {
        public static void RegisterServices(IDIRegistrationBridge registrationBridge, bool registerViewIds = true)
        {
            RegisterServicesInternal(registrationBridge);
            if (registerViewIds)
            {
                ViewIdDiscoveryService.RegisterViewIds(registrationBridge);   
            }
        }

        public static async UniTask RegisterServicesAsync(IDIRegistrationBridge registrationBridge)
        {
            RegisterServicesInternal(registrationBridge);
            await ViewIdDiscoveryService.RegisterViewIdsAsync(registrationBridge);
        }
        
        private static void RegisterServicesInternal(IDIRegistrationBridge registrationBridge)
        {
            registrationBridge.RegisterSingleton<IViewModelConstructionService, ViewModelConstructionService>();
            registrationBridge.RegisterSingleton<IViewIdRegistry, ViewIdRegistry>();
            registrationBridge.RegisterSingleton<INavigationService, NavigationService>();

            registrationBridge.RegisterSingleton<ShellViewModel, ShellViewModel>();
        }
    }
}