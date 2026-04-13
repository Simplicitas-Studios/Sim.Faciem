using System;

namespace Sim.Faciem
{
    internal class ViewModelConstructionService : IViewModelConstructionService
    {
        private readonly IDIContainerBridge _diContainerBridge;
        private INavigationService _navigationService;

        public ViewModelConstructionService(IDIContainerBridge diContainerBridge)
        {
            _diContainerBridge = diContainerBridge;
        }
        
        public T CreateInstance<T>() where T : BaseViewModel
        {
            EnsureNavigationServiceSet();
            
            var instance = _diContainerBridge.ResolveInstance<T>();
            var regionManager = new RegionManager();
            instance.Setup(regionManager, _navigationService);

            return instance;
        }

        public BaseViewModel CreateInstance(Type type)
        {
            EnsureNavigationServiceSet();
            
            var instance = _diContainerBridge.ResolveInstance(type);

            if (instance is not BaseViewModel baseViewModel)
            {
                throw new Exception("Cannot create instance of type " + type.FullName);
            }

            if (!baseViewModel.IsSetup)
            {
                var regionManager = new RegionManager();
                baseViewModel.Setup(regionManager, _navigationService);   
            }

            return baseViewModel;
        }

        private void EnsureNavigationServiceSet()
        {
            if (_navigationService == null)
            {
                _navigationService = _diContainerBridge.ResolveInstance<INavigationService>();
            }
        }
    }
}