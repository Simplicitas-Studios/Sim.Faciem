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
            IViewIdRegistry viewIdRegistry, IGlobalRegionManagerInternal globalRegionManagerInternal)
        {
            _viewModelConstructionService = viewModelConstructionService;
            _viewIdRegistry = viewIdRegistry;
            _globalRegionManagerInternal = globalRegionManagerInternal;
        }

        public UniTask NavigateTo(IRegionManager regionManager, ViewId viewId, RegionName regionName,
            NavigationParameters parameters)
        {
            return NavigateToFrom(null, regionManager, viewId, regionName, parameters);
        }

        internal async UniTask NavigateToFrom(BaseViewModel caller, IRegionManager regionManager,
            ViewId viewId, RegionName regionName, NavigationParameters parameters,
            NavigationPolicy policy = NavigationPolicy.Fifo, NavigationEntryMode entryMode = NavigationEntryMode.Default)
        {
            RegionNavigationOperations.Validate(policy);
            NavigationPolicyExtensions.ValidateEntryMode(entryMode);
            var maybeRegionInfos = TryFindRegion(regionManager, regionName);
            if (maybeRegionInfos.IsNone || !_viewIdRegistry.TryGetViewId(viewId, out var viewAsset))
            {
                return;
            }
            IRegion[] regions = RegionNavigationOperations.Snapshot(maybeRegionInfos.Value.Item2);
            await RegionNavigationOperations.Run(regions, caller, operation =>
                NavigateToOwned(maybeRegionInfos.Value.Item1, regions, viewAsset, viewId, parameters, operation, entryMode), policy);
        }

        private async UniTask NavigateToOwned(IRegionManager regionManager, IRegion[] regions,
            ViewIdAsset viewAsset, ViewId viewId, NavigationParameters parameters,
            RegionNavigationOperations.Operation operation, NavigationEntryMode entryMode)
        {
            BaseViewModel viewModel = null;
            NavigationActivation activation = null;
            var newlyActivated = new List<IRegion>();
            try
            {
                foreach (IRegion region in regions)
                {
                    if (region.TryGetView(viewAsset.ViewId, out var cached))
                    {
                        viewModel = cached.dataSource as BaseViewModel;
                        break;
                    }
                }
                operation.ThrowIfSuperseded();
                viewModel ??= _viewModelConstructionService.CreateInstance(viewAsset.DataContext.GetReferencedType());
                if (entryMode == NavigationEntryMode.ReenterIfActive)
                {
                    if (regions.Any(region => region.SupportMultipleViews))
                    {
                        throw new NotSupportedException("Explicit re-entry requires single-view Regions.");
                    }
                    if (regions.Any(region => region.ActiveViews.Contains(viewId)))
                    {
                        await RestartActiveTargets(operation, regions, viewId, viewModel);
                    }
                }

                foreach (IRegion region in regions)
                {
                    operation.ThrowIfSuperseded();
                    RegionNavigationOperations.ValidateModelOwnership(operation, viewModel);
                    if (region.ActiveViews.Contains(viewId) || region.SupportMultipleViews)
                    {
                        // Multi-view navigation remains outside this change.
                        continue;
                    }
                    foreach (ViewId oldId in region.ActiveViews.ToList())
                    {
                        if (!region.TryGetView(oldId, out var oldView) || oldView.dataSource is not BaseViewModel oldModel)
                        {
                            return;
                        }
                        RegionNavigationOperations.ValidateModelOwnership(operation, oldModel);
                        region.DeactivateView(oldId);
                        await Leave(operation, oldModel);
                        operation.ThrowIfSuperseded();
                    }
                    operation.ThrowIfSuperseded();
                    RegionNavigationOperations.ValidateModelOwnership(operation, viewModel);
                    if (!region.TryGetView(viewAsset.ViewId, out var view))
                    {
                        view = viewAsset.View.Instantiate();
                        view.style.flexGrow = 1;
                        view.pickingMode = PickingMode.Ignore;
                        foreach (Region innerRegion in view.Query<Region>().ToList())
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
                    operation.ThrowIfSuperseded();
                    region.ActivateView(viewId);
                    viewModel.TrackActiveView(region, viewId);
                    newlyActivated.Add(region);
                }
                if (viewModel == null)
                {
                    throw new InvalidOperationException("View has wrong Data Source");
                }
                RegionNavigationOperations.ValidateModelOwnership(operation, viewModel);
                viewModel.RegionManager.Parent = Maybe.Some(regionManager);
                await RegionNavigationOperations.InvokeLifecycle(operation, viewModel, async () =>
                {
                    activation = await viewModel.EnsureActivationAsync();
                });
                operation.Track(activation);
                operation.ThrowIfSuperseded();
                await RegionNavigationOperations.InvokeLifecycle(operation, viewModel,
                    () => viewModel.NavigateToInternal(parameters));
                operation.ThrowIfSuperseded();
            }
            catch (Exception error)
            {
                Exception cleanupError = null;
                if (activation != null)
                {
                    try
                    {
                        await RegionNavigationOperations.InvokeLifecycle(operation, viewModel,
                            () => viewModel.EndActivationAsync(activation));
                    }
                    catch (Exception cleanup)
                    {
                        cleanupError = cleanup;
                    }
                }
                if (operation.Superseded && viewModel != null)
                {
                    // Only touch captured, owned Regions. Do not remove cached Views or dataSources.
                    foreach (IRegion region in regions)
                    {
                        if ((activation != null || newlyActivated.Contains(region)) &&
                            region.TryGetView(viewId, out var view) && ReferenceEquals(view.dataSource, viewModel) &&
                            region.ActiveViews.Contains(viewId))
                        {
                            region.DeactivateView(viewId);
                        }
                    }
                }
                if (cleanupError != null)
                {
                    throw new AggregateException(error, cleanupError);
                }
                throw;
            }
        }

        private static async UniTask RestartActiveTargets(RegionNavigationOperations.Operation operation,
            IRegion[] regions, ViewId requestedId, BaseViewModel targetModel)
        {
            var exits = new List<(IRegion Region, ViewId ViewId, BaseViewModel Model)>();
            var models = new List<BaseViewModel>();
            foreach (IRegion region in regions)
            {
                if (region.TryGetView(requestedId, out var cached) && !ReferenceEquals(cached.dataSource, targetModel))
                {
                    throw new InvalidOperationException(
                        "Re-entry across Regions requires the requested cached Views to share the same ViewModel.");
                }
                foreach (ViewId id in region.ActiveViews.ToList())
                {
                    if (!region.TryGetView(id, out var view) || view.dataSource is not BaseViewModel model)
                    {
                        throw new InvalidOperationException("Re-entry requires owned active ViewModel dataSources.");
                    }
                    RegionNavigationOperations.ValidateModelOwnership(operation, model);
                    model.ValidateReentryOwnership(regions);
                    exits.Add((region, id, model));
                    if (!models.Any(previous => ReferenceEquals(previous, model)))
                    {
                        models.Add(model);
                    }
                }
            }
            RegionNavigationOperations.ValidateModelOwnership(operation, targetModel);
            targetModel.ValidateReentryOwnership(regions);
            operation.ThrowIfSuperseded();
            // Preflight the entire group before hiding or ending any activation.
            foreach (var exit in exits)
            {
                exit.Region.DeactivateView(exit.ViewId);
            }
            foreach (BaseViewModel model in models)
            {
                await Leave(operation, model);
                operation.ThrowIfSuperseded();
            }
        }

        public UniTask Clear(IRegionManager regionManager, RegionName regionName)
        {
            return ClearFrom(null, regionManager, regionName);
        }

        internal async UniTask ClearFrom(BaseViewModel caller, IRegionManager regionManager, RegionName regionName,
            NavigationPolicy policy = NavigationPolicy.Fifo)
        {
            RegionNavigationOperations.Validate(policy);
            var maybeRegionInfos = TryFindRegion(regionManager, regionName);
            if (maybeRegionInfos.IsNone)
            {
                return;
            }
            IRegion[] regions = RegionNavigationOperations.Snapshot(maybeRegionInfos.Value.Item2);
            await RegionNavigationOperations.Run(regions, caller, operation => ClearOwned(regions, operation), policy);
        }

        private static async UniTask ClearOwned(IRegion[] regions, RegionNavigationOperations.Operation operation)
        {
            foreach (IRegion region in regions)
            {
                operation.ThrowIfSuperseded();
                foreach (ViewId oldId in region.ActiveViews.ToList())
                {
                    if (!region.TryGetView(oldId, out var view) || view.dataSource is not BaseViewModel model)
                    {
                        return;
                    }
                    RegionNavigationOperations.ValidateModelOwnership(operation, model);
                    region.DeactivateView(oldId);
                    await Leave(operation, model);
                    operation.ThrowIfSuperseded();
                }
            }
        }

        private static UniTask Leave(RegionNavigationOperations.Operation operation, BaseViewModel model)
        {
            return RegionNavigationOperations.InvokeLifecycle(operation, model, async () =>
            {
                await model.EndActivationAsync();
                await model.NavigateAwayInternal();
            });
        }

        private Maybe<(IRegionManager, IReadOnlyList<IRegion>)> TryFindRegion(IRegionManager manager, RegionName name)
        {
            return !manager.TryFindRegion(name, out var regions)
                ? Maybe.None<(IRegionManager, IReadOnlyList<IRegion>)>()
                : Maybe.Some((manager, regions));
        }
    }
}
