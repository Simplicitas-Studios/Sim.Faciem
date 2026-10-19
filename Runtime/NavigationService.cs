using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sim.Faciem.Internal;
using Sim.Utility;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    internal class NavigationService : INavigationService
    {
        private readonly IViewModelConstructionService _viewModelConstructionService;
        private readonly IViewIdRegistry _viewIdRegistry;
        private readonly IGlobalRegionManagerInternal _globalRegionManagerInternal;

        public NavigationService(IViewModelConstructionService viewModelConstructionService,
            IViewIdRegistry viewIdRegistry,
            IGlobalRegionManagerInternal globalRegionManagerInternal)
        {
            _viewModelConstructionService = viewModelConstructionService;
            _viewIdRegistry = viewIdRegistry;
            _globalRegionManagerInternal = globalRegionManagerInternal;
        }

        public UniTask NavigateTo(
            IRegionManager regionManager,
            ViewId viewId,
            RegionName regionName,
            NavigationParameters parameters)
        {
            return NavigateToFrom(null, regionManager, viewId, regionName, parameters);
        }

        internal async UniTask NavigateToFrom(BaseViewModel caller, IRegionManager regionManager,
            ViewId viewId, RegionName regionName, NavigationParameters parameters)
        {
            var maybeRegionInfos = TryFindRegion(regionManager, regionName);

            if (maybeRegionInfos.IsNone
                || !_viewIdRegistry.TryGetViewId(viewId, out var viewAsset))
            {
                return;
            }

            IRegion[] regions = RegionNavigationOperations.Snapshot(maybeRegionInfos.Value.Item2);
            await RegionNavigationOperations.Run(regions, caller, operation =>
                NavigateToOwned(maybeRegionInfos.Value.Item1, regions, viewAsset, viewId, parameters, operation));
        }

        private async UniTask NavigateToOwned(IRegionManager regionManager, IRegion[] regions,
            ViewIdAsset viewAsset, ViewId viewId, NavigationParameters parameters,
            RegionNavigationOperations.Operation operation)
        {
            BaseViewModel viewModel = null;

            foreach (IRegion region in regions)
            {
                if (region.TryGetView(viewAsset.ViewId, out var view))
                {
                    viewModel = view.dataSource as BaseViewModel;
                    break;
                }
            }

            viewModel ??= _viewModelConstructionService.CreateInstance(viewAsset.DataContext.GetReferencedType());

            foreach (IRegion region in regions)
            {
                if (region.ActiveViews.Contains(viewId) || region.SupportMultipleViews)
                {
                    //TODO: Add support for multi view regions
                    continue;
                }

                // Navigate away current active views
                var activeViewsCopy = region.ActiveViews.ToList();
                foreach (var currentActiveViewId in activeViewsCopy)
                {
                    if (!region.TryGetView(currentActiveViewId, out var oldView)
                        || oldView.dataSource is not BaseViewModel baseViewModel)
                    {
                        return;
                    }

                    region.DeactivateView(currentActiveViewId);
                    await RegionNavigationOperations.InvokeLifecycle(operation, baseViewModel,
                        baseViewModel.NavigateAwayInternal);
                }

                if (!region.TryGetView(viewAsset.ViewId, out var view))
                {
                    view = viewAsset.View.Instantiate();
                    view.style.flexGrow = 1;
                    view.pickingMode = PickingMode.Ignore;

                    var innerRegions = view.Query<Region>().ToList();

                    foreach (var innerRegion in innerRegions)
                    {
                        if (innerRegion.IsGlobal)
                        {
                            innerRegion.RegisterDirect(_globalRegionManagerInternal);
                        }
                        innerRegion.RegisterDirect(viewModel);
                    }

                    region.AddView(viewId, view);
                    view.dataSource = viewModel;
                }
                else
                {
                    view.dataSource = null;
                    view.dataSource = viewModel;
                }

                // Active new View
                region.ActivateView(viewId);
            }

            if (viewModel == null)
            {
                throw new InvalidOperationException("View has wrong Data Source");
            }

            viewModel.RegionManager.Parent = Maybe.Some(regionManager);

            await RegionNavigationOperations.InvokeLifecycle(operation, viewModel,
                () => viewModel.NavigateToInternal(parameters));
        }

        public UniTask Clear(IRegionManager regionManager, RegionName regionName)
        {
            return ClearFrom(null, regionManager, regionName);
        }

        internal async UniTask ClearFrom(BaseViewModel caller, IRegionManager regionManager, RegionName regionName)
        {
            var maybeRegionInfos = TryFindRegion(regionManager, regionName);

            if (maybeRegionInfos.IsNone)
            {
                return;
            }

            IRegion[] regions = RegionNavigationOperations.Snapshot(maybeRegionInfos.Value.Item2);
            await RegionNavigationOperations.Run(regions, caller, operation => ClearOwned(regions, operation));
        }

        private static async UniTask ClearOwned(IRegion[] regions, RegionNavigationOperations.Operation operation)
        {
            foreach (IRegion region in regions)
            {
                var activeViewsCopy = region.ActiveViews.ToList();
                foreach (var currentActiveViewId in activeViewsCopy)
                {
                    if (!region.TryGetView(currentActiveViewId, out var oldView)
                        || oldView.dataSource is not BaseViewModel baseViewModel)
                    {
                        return;
                    }

                    region.DeactivateView(currentActiveViewId);
                    await RegionNavigationOperations.InvokeLifecycle(operation, baseViewModel,
                        baseViewModel.NavigateAwayInternal);
                }
            }
        }

        private Maybe<(IRegionManager, IReadOnlyList<IRegion>)> TryFindRegion(IRegionManager regionManager,
            RegionName regionName)
        {
            return !regionManager.TryFindRegion(regionName, out var region)
                ? Maybe.None<(IRegionManager, IReadOnlyList<IRegion>)>()
                : Maybe.Some((regionManager, region));
        }
    }
}
