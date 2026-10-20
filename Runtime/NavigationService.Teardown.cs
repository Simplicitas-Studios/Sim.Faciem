using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sim.Faciem.Internal;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    internal partial class NavigationService
    {
        internal async UniTask<NavigationResult> TeardownFrom(BaseViewModel caller, IReadOnlyList<Region> selected,
            Func<BaseViewModel, bool> ownsModel)
        {
            var execution = new NavigationExecution();
            NavigationResult result = null;
            try
            {
                if (selected == null || selected.Count == 0 || ownsModel == null || selected.Any(region => region == null))
                {
                    execution.Reject(NavigationStatus.InvalidConfiguration, "Explicit Regions and a final model ownership predicate are required.");
                }
                IRegion[] targets = RegionNavigationOperations.Snapshot(selected.Cast<IRegion>().ToArray());
                Region[] regions = targets.Cast<Region>().ToArray();
                if (regions.Any(region => region.OwnerThreadId != Thread.CurrentThread.ManagedThreadId))
                {
                    execution.Reject(NavigationStatus.InvalidConfiguration, "Teardown requires the Regions' owning Unity thread.");
                }
                execution.At(NavigationStage.Queue);
                await RegionNavigationOperations.Run(targets, caller, async operation =>
                {
                    result = await TeardownOwned(regions, targets, ownsModel, operation, execution);
                }, NavigationPolicy.Fifo, execution, teardown: true);
            }
            catch (Exception error)
            {
                execution.Capture(error);
            }
            return result ?? execution.Result();
        }

        private static async UniTask<NavigationResult> TeardownOwned(Region[] regions, IRegion[] targets,
            Func<BaseViewModel, bool> ownsModel, RegionNavigationOperations.Operation operation, NavigationExecution execution)
        {
            execution.At(NavigationStage.Validation);
            if (regions.All(region => region.IsReleased))
            {
                return regions.Select(region => region.ReleaseResult).FirstOrDefault(previous => previous != null && previous.Error != null) ??
                    new NavigationResult(NavigationStatus.NoOp, NavigationStage.None, NavigationRecovery.NotRequired);
            }
            if (regions.Any(region => region.IsReleased))
            {
                execution.Reject(NavigationStatus.InvalidConfiguration, "Do not combine released and live Regions in one teardown.");
            }
            var models = new List<BaseViewModel>();
            var active = new List<BaseViewModel>();
            var views = new List<(Region Region, VisualElement View)>();
            foreach (Region region in regions)
            {
                if (region.SupportMultipleViews || region.IsGlobal)
                {
                    execution.Reject(NavigationStatus.UnsupportedOperation, "Final teardown supports native non-global single-view Regions only.");
                }
                Region.CacheEntry[] entries = region.OwnedEntries;
                foreach (KeyValuePair<ViewId, VisualElement> cached in region.CachedViews)
                {
                    if (!entries.Any(entry => entry.Id.Equals(cached.Key) && ReferenceEquals(entry.View, cached.Value)))
                    {
                        execution.Reject(NavigationStatus.InvalidConfiguration, "Untracked cached Views cannot be adopted for disposal.");
                    }
                }
                foreach (Region.CacheEntry entry in entries)
                {
                    if (entry.View.parent != null && !region.Contains(entry.View))
                    {
                        execution.Reject(NavigationStatus.InvalidConfiguration, "A cached View moved outside its owner.");
                    }
                    if (entry.View.Query<Region>().ToList().Count != 0)
                    {
                        execution.Reject(NavigationStatus.UnsupportedOperation, "Parent caches containing nested Regions require a separate ownership contract.");
                    }
                    if (entry.View.dataSource != null && !entries.Any(known => ReferenceEquals(known.View, entry.View) && ReferenceEquals(known.Model, entry.View.dataSource)))
                    {
                        execution.Reject(NavigationStatus.InvalidConfiguration, "A cached View has a foreign dataSource.");
                    }
                    if (!views.Any(previous => ReferenceEquals(previous.View, entry.View)))
                    {
                        views.Add((region, entry.View));
                    }
                }
                foreach (BaseViewModel model in region.OwnedModels)
                {
                    if (!models.Any(previous => ReferenceEquals(previous, model)))
                    {
                        models.Add(model);
                    }
                }
                foreach (ViewId id in region.ActiveViews)
                {
                    if (!region.TryGetView(id, out var view) || view.dataSource is not BaseViewModel model ||
                        !region.OwnsModel(model))
                    {
                        execution.Reject(NavigationStatus.InvalidConfiguration, "Active cache ownership is invalid.");
                    }
                    else if (!active.Any(previous => ReferenceEquals(previous, model)))
                    {
                        active.Add(model);
                    }
                }
            }
            foreach (BaseViewModel model in models)
            {
                if (model.IsDisposed || !ownsModel(model))
                {
                    execution.Reject(NavigationStatus.InvalidConfiguration, "The application must own every live cached model's final disposal.");
                }
                if (model.RegionManager is not RegionManager manager || manager.HasLocalRegions)
                {
                    execution.Reject(NavigationStatus.UnsupportedOperation, "Models with nested/local Region registrations are outside flat teardown ownership.");
                }
                RegionNavigationOperations.ValidateModelOwnership(operation, model);
                model.ValidateFinalOwnership(targets);
            }
            // No awaits between complete ownership validation and acquiring all final-model leases.
            foreach (BaseViewModel model in models)
            {
                model.FinalOwner = operation;
            }
            operation.KeepClosed = true;
            execution.Change();
            foreach (Region region in regions)
            {
                region.IsReleased = true;
            }
            var errors = new List<Exception>();
            async UniTask Attempt(NavigationStage stage, Func<UniTask> action)
            {
                execution.At(stage);
                try
                {
                    await action();
                }
                catch (Exception error)
                {
                    execution.Capture(error);
                    errors.Add(error);
                }
            }
            try
            {
                foreach (Region region in regions)
                {
                    await Attempt(NavigationStage.ViewRelease, () =>
                    {
                        region.DeactivateAllViews();
                        return UniTask.CompletedTask;
                    });
                }
                foreach (BaseViewModel model in models)
                {
                    await Attempt(NavigationStage.Cleanup, () => RegionNavigationOperations.InvokeLifecycle(operation, model,
                        () => model.EndActivationAsync()));
                }
                foreach (BaseViewModel model in active)
                {
                    // Cleanup failure must not suppress the required exit attempt for another stage/model.
                    await Attempt(NavigationStage.Exit, () => RegionNavigationOperations.InvokeLifecycle(operation, model,
                        () => model.NavigateAwayInternal()));
                }
                foreach (var owned in views)
                {
                    if (owned.View.parent != null && !owned.Region.Contains(owned.View))
                    {
                        await Attempt(NavigationStage.ViewRelease, () =>
                            UniTask.FromException(new InvalidOperationException("A callback reparented an owned View; teardown will not mutate an unowned tree.")));
                        continue;
                    }
                    if (owned.View.dataSource != null && !models.Any(model => ReferenceEquals(model, owned.View.dataSource)))
                    {
                        await Attempt(NavigationStage.ViewRelease, () =>
                            UniTask.FromException(new InvalidOperationException("A foreign dataSource appeared during exit; its tree requires application repair.")));
                        continue;
                    }
                    if (owned.View.Query<Region>().ToList().Count != 0)
                    {
                        await Attempt(NavigationStage.ViewRelease, () =>
                            UniTask.FromException(new NotSupportedException("A nested Region appeared during exit; teardown cannot detach an unreserved scope.")));
                        continue;
                    }
                    VisualElement[] elements = new[] { owned.View }.Concat(owned.View.Query<VisualElement>().ToList()).ToArray();
                    foreach (VisualElement element in elements)
                    {
                        await Attempt(NavigationStage.ViewRelease, () =>
                        {
                            // Explicit IDs also cover registrations still pending their first panel binding update.
                            foreach (BindingInfo binding in element.GetBindingInfos().ToArray())
                            {
                                element.ClearBinding(binding.bindingId);
                            }
                            element.ClearBindings();
                            return UniTask.CompletedTask;
                        });
                        await Attempt(NavigationStage.ViewRelease, () =>
                        {
                            element.dataSource = null;
                            return UniTask.CompletedTask;
                        });
                    }
                    await Attempt(NavigationStage.ViewRelease, () =>
                    {
                        owned.View.style.display = DisplayStyle.None;
                        owned.View.RemoveFromHierarchy();
                        return UniTask.CompletedTask;
                    });
                }
                foreach (Region region in regions)
                {
                    region.ClearCacheReferences();
                }
                foreach (BaseViewModel model in models)
                {
                    await Attempt(NavigationStage.FinalDisposal, () => RegionNavigationOperations.InvokeLifecycle(operation, model, () =>
                    {
                        model.Dispose();
                        return UniTask.CompletedTask;
                    }));
                }
            }
            finally
            {
                foreach (BaseViewModel model in models)
                {
                    model.FinalOwner = null;
                }
            }
            NavigationResult result;
            if (errors.Count != 0)
            {
                result = new NavigationResult(NavigationStatus.Failed, execution.FailureStage, NavigationRecovery.Incomplete,
                    new AggregateException("Final teardown encountered failures; all safely owned stages were attempted.", errors));
            }
            else
            {
                result = new NavigationResult(NavigationStatus.Succeeded, NavigationStage.None, NavigationRecovery.NotRequired);
            }
            foreach (Region region in regions)
            {
                region.ReleaseResult = result;
            }
            return result;
        }
    }
}
