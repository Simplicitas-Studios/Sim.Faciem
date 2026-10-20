using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using R3;
using Sim.Faciem.Commands;

namespace Sim.Faciem
{
    public abstract class BaseViewModel : IDisposable, IDisposableContainer, IRegionSetup
    {
        internal bool IsSetup => RegionManager != null && Navigation != null;

        private DisposableBag _disposables = new();
        private readonly List<ActiveViewBinding> _activeViews = new();

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

        internal void TrackActiveView(IRegion region, ViewId viewId)
        {
            _activeViews.RemoveAll(binding => !IsActive(binding, out _) ||
                (binding.Region.TryGetTarget(out IRegion previous) && ReferenceEquals(previous, region) &&
                    binding.ViewId.Equals(viewId)));
            _activeViews.Add(new ActiveViewBinding(region, viewId));
        }

        internal void ValidateReentryOwnership(IReadOnlyList<IRegion> targets)
        {
            foreach (ActiveViewBinding binding in _activeViews)
            {
                if (IsActive(binding, out IRegion region) && !targets.Any(target => ReferenceEquals(target, region)))
                {
                    throw new InvalidOperationException(
                        "Re-entry requires all active Regions sharing the cached ViewModel to be targeted together.");
                }
            }
        }

        private bool IsActive(ActiveViewBinding binding, out IRegion region)
        {
            return binding.Region.TryGetTarget(out region) && region.ActiveViews.Contains(binding.ViewId) &&
                region.TryGetView(binding.ViewId, out var view) && ReferenceEquals(view.dataSource, this);
        }

        private sealed class ActiveViewBinding
        {
            internal readonly WeakReference<IRegion> Region;
            internal readonly ViewId ViewId;

            internal ActiveViewBinding(IRegion region, ViewId viewId)
            {
                Region = new WeakReference<IRegion>(region);
                ViewId = viewId;
            }
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
