using System;
using Cysharp.Threading.Tasks;
using R3;
using Sim.Faciem.Commands;

namespace Sim.Faciem
{
    public abstract class BaseViewModel : IDisposable, IDisposableContainer, IRegionSetup
    {

        internal bool IsSetup => RegionManager != null && Navigation != null;

        protected DisposableBag Disposables { get; } = new();

        protected internal RegionManager RegionManager { get; set; }

        protected IViewModelNavigationService Navigation { get; private set; }

        protected ICommandBuilderFactory Command { get; private set; }


        protected BaseViewModel()
        {
            Command = new ViewModelCommandBuilderFactory(this);
        }

        internal void Setup(RegionManager regionManager, INavigationService navigationService)
        {
            RegionManager = regionManager;
            Navigation = new ViewModelNavigationService(this, navigationService);
        }

        public void AddRegion(IRegion region)
        {
            Disposables.Add(region
                .Destroyed
                .Take(1)
                .Subscribe(_ => RegionManager.RemoveRegion(region)));
            RegionManager.AddRegion(region);
        }

        internal UniTask NavigateToInternal()
        {
            return NavigateTo();
        }

        protected virtual UniTask NavigateTo()
        {
            return UniTask.CompletedTask;
        }

        internal UniTask NavigateAwayInternal()
        {
            return NavigateAway();
        }

        protected virtual UniTask NavigateAway()
        {
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            Disposables.Dispose();
        }

        void IDisposableContainer.Add(IDisposable disposable)
        {
            Disposables.Add(disposable);
        }


    }
}