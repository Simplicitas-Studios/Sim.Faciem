using System;
using Cysharp.Threading.Tasks;
using R3;
using Sim.Faciem.Commands;

namespace Sim.Faciem
{
    public abstract class BaseViewModel : IDisposable, IDisposableContainer, IRegionSetup
    {
        internal bool IsSetup => RegionManager != null && Navigation != null;

        private DisposableBag _disposables = new();

        protected ref DisposableBag Disposables => ref _disposables;

        protected internal IViewModelRegionManager RegionManager { get; set; }

        /// <summary>Use this scoped handle for navigation from lifecycle callbacks, including after awaits.</summary>
        protected IViewModelNavigationService Navigation { get; private set; }

        /// <summary>
        /// Current activation, or null after exit. Capture this handle before awaits and publish through it.
        /// Disposables remains the separate final-model lifetime bag.
        /// </summary>
        protected NavigationActivation Activation { get; private set; }

        protected ICommandBuilderFactory Command { get; private set; }

        protected BaseViewModel()
        {
            Command = new ViewModelCommandBuilderFactory(this);
        }

        internal void Setup(IViewModelRegionManager regionManager, INavigationService navigationService)
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

        internal async UniTask<NavigationActivation> EnsureActivationAsync()
        {
            if (Activation != null && !Activation.IsValid)
            {
                await EndActivationAsync(Activation);
            }
            if (Activation == null)
            {
                NavigationActivation created = null;
                created = new NavigationActivation(() => ReferenceEquals(Activation, created));
                Activation = created;
            }
            return Activation;
        }

        internal UniTask EndActivationAsync(NavigationActivation expected = null)
        {
            NavigationActivation activation = expected ?? Activation;
            if (activation == null)
            {
                return UniTask.CompletedTask;
            }
            if (ReferenceEquals(Activation, activation))
            {
                Activation = null;
            }
            return activation.EndAsync();
        }

        internal UniTask NavigateToInternal(NavigationParameters parameters)
        {
            return NavigateTo(parameters);
        }

        protected virtual UniTask NavigateTo(NavigationParameters navigationParameters)
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
