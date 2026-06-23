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

        public async UniTask NavigateTo(
            IRegionManager regionManager,
            ViewId viewId,
            RegionName regionName,
            NavigationParameters parameters)
        {
            var maybeRegionInfos = TryFindRegion(regionManager, regionName);

            if (maybeRegionInfos.IsNone
                || !_viewIdRegistry.TryGetViewId(viewId, out var viewAsset))
            {
                return;
            }

            BaseViewModel viewModel = null;

            foreach (var region in maybeRegionInfos.Value.Item2)
            {
                if (region.TryGetView(viewAsset.ViewId, out var view))
                {
                    viewModel = view.dataSource as BaseViewModel;
                    break;
                }
            }

            viewModel ??= _viewModelConstructionService.CreateInstance(viewAsset.DataContext.Script.GetClass());

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

                if (!region.TryGetView(viewAsset.ViewId, out var view))
                {
                    view = viewAsset.View.Instantiate();
                    view.style.flexGrow = 1;
                    view.pickingMode = PickingMode.Ignore;

                    var regions = view.Query<Region>().ToList();

                    foreach (var innerRegion in regions)
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

            var regionControllingManager = maybeRegionInfos.Value.Item1;

            if (viewModel == null)
            {
                throw new InvalidOperationException("View has wrong Data Source");
            }

            viewModel.RegionManager.Parent = Maybe.Some(regionControllingManager);

            await viewModel.NavigateToInternal(parameters);
        }

        public async UniTask Clear(IRegionManager regionManager, RegionName regionName)
        {
            var maybeRegionInfos = TryFindRegion(regionManager, regionName);

            if (maybeRegionInfos.IsNone)
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

        private Maybe<(IRegionManager, IReadOnlyList<IRegion>)> TryFindRegion(IRegionManager regionManager,
            RegionName regionName)
        {
            return !regionManager.TryFindRegion(regionName, out var region)
                ? Maybe.None<(IRegionManager, IReadOnlyList<IRegion>)>()
                : Maybe.Some((regionManager, region));
        }
    }
}
