using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Sim.Faciem.Internal
{
    // Per-request observation only. The existing Region queue remains the sole operation owner.
    internal sealed class NavigationExecution
    {
        internal NavigationStage Stage = NavigationStage.Validation;
        internal NavigationStage FailureStage;
        internal NavigationStatus? Status;
        internal NavigationRecovery Recovery;
        internal RegionNavigationOperations.Operation Operation;
        internal Exception Error;
        internal bool Mutated;
        internal bool Performed;
        internal readonly List<BaseViewModel> Models = new();
        internal readonly List<BaseViewModel> ExitedModels = new();
        internal readonly List<(IRegion Region, ViewId Id, VisualElement View)> Views = new();

        internal void TrackView(IRegion region, ViewId id, VisualElement view)
        {
            if (!Views.Any(previous => ReferenceEquals(previous.Region, region) && ReferenceEquals(previous.View, view)))
            {
                Views.Add((region, id, view));
            }
        }

        internal void At(NavigationStage stage)
        {
            Stage = stage;
        }

        internal void Change()
        {
            Mutated = true;
            Performed = true;
        }

        internal void Track(BaseViewModel model)
        {
            if (model != null && !Models.Any(previous => ReferenceEquals(previous, model)))
            {
                Models.Add(model);
            }
        }

        internal void Reject(NavigationStatus status, string message)
        {
            Status = status;
            throw new InvalidOperationException(message);
        }

        internal void Capture(Exception error)
        {
            if (Error == null)
            {
                Error = error;
                FailureStage = Stage;
                if (!Mutated)
                {
                    Recovery = NavigationRecovery.Preserved;
                }
            }
        }

        internal NavigationResult Result()
        {
            if (Error == null)
            {
                return new NavigationResult(Status ?? (Performed ? NavigationStatus.Succeeded : NavigationStatus.NoOp),
                    Status.HasValue ? Stage : NavigationStage.None, NavigationRecovery.NotRequired);
            }
            NavigationStatus status;
            if (Error is OperationCanceledException)
            {
                status = Operation != null && Operation.Superseded ? NavigationStatus.Superseded : NavigationStatus.Cancelled;
            }
            else if (Recovery == NavigationRecovery.Incomplete)
            {
                status = NavigationStatus.Failed;
            }
            else if (Status.HasValue)
            {
                status = Status.Value;
            }
            else if (FailureStage == NavigationStage.Validation || FailureStage == NavigationStage.Queue)
            {
                status = Error is NotSupportedException ? NavigationStatus.UnsupportedOperation :
                    Error is ArgumentException || Error is InvalidOperationException ? NavigationStatus.InvalidConfiguration : NavigationStatus.Failed;
            }
            else
            {
                status = NavigationStatus.Failed;
            }
            return new NavigationResult(status, FailureStage, Recovery, Error);
        }

        internal static NavigationResult Unsupported(string message)
        {
            return new NavigationResult(NavigationStatus.UnsupportedOperation, NavigationStage.Validation,
                NavigationRecovery.Preserved, new NotSupportedException(message));
        }
    }
}
