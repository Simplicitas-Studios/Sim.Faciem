using System;
using System.Collections.Generic;
using System.Linq;
using Bebop.Monads;
using Cysharp.Threading.Tasks;
using Sim.Faciem.Internal;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    internal class NavigationService : INavigationService
    {
        private readonly IViewModelConstructionService _viewModelConstructionService;
        private readonly IViewIdRegistry _viewIdRegistry;

        public NavigationService(IViewModelConstructionService viewModelConstructionService, IViewIdRegistry viewIdRegistry)
        {
            _viewModelConstructionService = viewModelConstructionService;
            _viewIdRegistry = viewIdRegistry;
        }


        public async UniTask NavigateTo(RegionManager regionManager, ViewId viewId, RegionName regionName)
        {
            var maybeRegionInfos = TryFindRegion(regionManager, regionName) as IMaybe<(RegionManager, IReadOnlyList<IRegion>)>;

            if (!maybeRegionInfos.HasValue
                || !_viewIdRegistry.TryGetViewId(viewId, out var viewAsset))
            {
                return;
            }


            BaseViewModel viewModel = null;

            foreach (var region in maybeRegionInfos.Value.Item2)
            {
                if(region.TryGetView(viewAsset.ViewId, out var view))
                {
                    viewModel = view.dataSource as BaseViewModel;
                    break;
                }
            }

#if UNITY_EDITOR
            viewModel ??= _viewModelConstructionService.CreateInstance(viewAsset.DataContext.Script.GetClass());
#endif

            foreach (var region in maybeRegionInfos.Value.Item2)
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
                    await baseViewModel.NavigateAwayInternal();
                }

                if(!region.TryGetView(viewAsset.ViewId, out var view))
                {
                    view = viewAsset.View.Instantiate();
                    view.style.flexGrow = 1;

                    var regions = view.Query<Region>().ToList();

                    foreach (var innerRegion in regions)
                    {
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
            var regionControllingManager = maybeRegionInfos.Value.Item1;

            if (viewModel == null)
            {
                throw new InvalidOperationException("View has wrong Data Source");
            }

            viewModel.RegionManager.Parent = regionControllingManager;

            await viewModel.NavigateToInternal();
        }

        public async UniTask Clear(RegionManager regionManager, RegionName regionName)
        {
            var maybeRegionInfos = TryFindRegion(regionManager, regionName) as IMaybe<(RegionManager, IReadOnlyList<IRegion>)>;

            if (!maybeRegionInfos.HasValue)
            {
                return;
            }

            var regions = maybeRegionInfos.Value.Item2;

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
                    await baseViewModel.NavigateAwayInternal();
                }
            }
        }

        private Maybe<(RegionManager, IReadOnlyList<IRegion>)> TryFindRegion(RegionManager regionManager, RegionName regionName)
        {
            if (!regionManager.TryFindRegion(regionName, out var region))
            {
                return regionManager.Parent
                    .Map(parent => TryFindRegion(parent, regionName));
            }

            return Maybe.From((regionManager, region));
        }
    }
}