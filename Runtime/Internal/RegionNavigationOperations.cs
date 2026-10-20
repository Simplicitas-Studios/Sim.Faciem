using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;

namespace Sim.Faciem.Internal
{
    internal static class RegionNavigationOperations
    {
        private static readonly object Gate = new object();
        private static readonly ConditionalWeakTable<IRegion, RegionTail> Tails = new();
        private static readonly ConditionalWeakTable<BaseViewModel, CallbackState> Callbacks = new();

        internal sealed class Operation
        {
            internal readonly IRegion[] Regions;
            internal readonly UniTaskCompletionSource Completion = new();
            internal readonly HashSet<Operation> WaitingOn = new();
            internal readonly Operation[] Parents;
            internal readonly NavigationPolicy Policy;
            internal readonly HashSet<NavigationActivation> Activations = new();
            internal Operation[] Predecessors;
            internal NavigationActivation[] Invalidations = Array.Empty<NavigationActivation>();
            internal bool Finished;
            internal bool Superseded;
            internal bool KeepClosed;

            internal Operation(IRegion[] regions, Operation[] predecessors, Operation[] parents, NavigationPolicy policy)
            {
                Regions = regions;
                Predecessors = predecessors;
                Parents = parents;
                Policy = policy;
                WaitingOn.UnionWith(predecessors);
            }

            internal void ThrowIfSuperseded()
            {
                if (Superseded)
                {
                    throw new OperationCanceledException("Superseded by a newer LatestRequestWins operation.");
                }
            }

            internal void Track(NavigationActivation activation)
            {
                bool invalidate;
                lock (Gate)
                {
                    Activations.Add(activation);
                    invalidate = Superseded;
                }
                if (invalidate)
                {
                    activation.Invalidate();
                }
            }
        }

        internal static IRegion[] Snapshot(IReadOnlyList<IRegion> regions)
        {
            return regions.Distinct(RegionIdentity.Instance).ToArray();
        }

        internal static async UniTask Run(IRegion[] regions, BaseViewModel caller,
            Func<Operation, UniTask> action, NavigationPolicy policy = NavigationPolicy.Fifo,
            NavigationExecution execution = null, bool teardown = false)
        {
            Operation operation = Reserve(regions, caller, policy, teardown);
            if (execution != null)
            {
                execution.Operation = operation;
            }
            try
            {
                // Notify outside the bookkeeping lock; callbacks may submit more requests.
                foreach (NavigationActivation activation in operation.Invalidations)
                {
                    activation.Invalidate();
                }
                operation.Invalidations = Array.Empty<NavigationActivation>();
                foreach (Operation predecessor in operation.Predecessors)
                {
                    await predecessor.Completion.Task;
                }
                lock (Gate)
                {
                    operation.WaitingOn.ExceptWith(operation.Predecessors);
                    operation.Predecessors = Array.Empty<Operation>();
                    foreach (IRegion region in regions)
                    {
                        RegionTail tail = Tails.GetValue(region, _ => new RegionTail());
                        if (!teardown && tail.Closed)
                        {
                            throw new ObjectDisposedException(nameof(Region), "The Region cache has been released.");
                        }
                        if (teardown && !tail.Closed && tail.Finalizing == null)
                        {
                            tail.Finalizing = operation;
                        }
                    }
                }
                operation.ThrowIfSuperseded();
                await action(operation);
                operation.ThrowIfSuperseded();
            }
            finally
            {
                lock (Gate)
                {
                    operation.Finished = true;
                    operation.WaitingOn.Clear();
                    foreach (Operation parent in operation.Parents)
                    {
                        parent.WaitingOn.Remove(operation);
                    }
                    foreach (IRegion region in operation.Regions)
                    {
                        RegionTail tail = Tails.GetValue(region, _ => new RegionTail());
                        tail.Pending.Remove(operation);
                        if (ReferenceEquals(tail.Finalizing, operation))
                        {
                            tail.Finalizing = null;
                            tail.Closed = operation.KeepClosed;
                        }
                        if (ReferenceEquals(tail.Last, operation))
                        {
                            tail.Last = null;
                        }
                    }
                }
                // Completion releases successors even when this operation faults.
                operation.Completion.TrySetResult();
            }
        }

        internal static void ValidateModelOwnership(Operation operation, BaseViewModel model)
        {
            lock (Gate)
            {
                if ((model.FinalOwner != null && !ReferenceEquals(model.FinalOwner, operation)) ||
                    (model.IsDisposed && !ReferenceEquals(model.FinalOwner, operation)))
                {
                    throw new ObjectDisposedException(nameof(BaseViewModel), "The cached model is finalizing or disposed.");
                }
                if (Callbacks.TryGetValue(model, out CallbackState state) &&
                    state.Active.Any(owner => !ReferenceEquals(owner, operation)))
                {
                    throw new InvalidOperationException(
                        "A ViewModel with an in-flight lifecycle cannot be shared by independent Region operations. " +
                        "Use distinct ViewModels for independent activation lifetimes.");
                }
            }
        }

        internal static async UniTask InvokeLifecycle(Operation operation, BaseViewModel model,
            Func<UniTask> callback)
        {
            CallbackState state;
            lock (Gate)
            {
                ValidateModelOwnership(operation, model);
                state = Callbacks.GetValue(model, _ => new CallbackState());
                state.Active.Add(operation);
            }
            try
            {
                await callback();
            }
            finally
            {
                lock (Gate)
                {
                    state.Active.Remove(operation);
                }
            }
        }

        internal static void Validate(NavigationPolicy policy)
        {
            if (policy != NavigationPolicy.Fifo && policy != NavigationPolicy.LatestRequestWins)
            {
                throw new ArgumentOutOfRangeException(nameof(policy));
            }
        }

        private static Operation Reserve(IRegion[] regions, BaseViewModel caller, NavigationPolicy policy, bool teardown)
        {
            Validate(policy);
            lock (Gate)
            {
                Operation[] parents = caller != null && Callbacks.TryGetValue(caller, out CallbackState state)
                    ? state.Active.Distinct().ToArray()
                    : Array.Empty<Operation>();
                Operation[] predecessors = regions.Select(region => Tails.GetValue(region, _ => new RegionTail()).Last)
                    .Where(operation => operation != null).Distinct().ToArray();

                foreach (Operation parent in parents)
                {
                    if (regions.Any(region => parent.Regions.Contains(region, RegionIdentity.Instance)) ||
                        predecessors.Any(predecessor => Reaches(predecessor, parent, new HashSet<Operation>())))
                    {
                        throw new InvalidOperationException(
                            "Lifecycle navigation cannot reenter an owned Region or create a navigation dependency cycle. " +
                            "Use a different independent Region, or request navigation after the lifecycle callback completes.");
                    }
                }

                if (caller != null && (caller.IsDisposed || caller.FinalOwner != null))
                {
                    throw new ObjectDisposedException(nameof(BaseViewModel), "A stale/finalizing scoped handle cannot submit navigation or teardown.");
                }
                foreach (IRegion region in regions)
                {
                    RegionTail tail = Tails.GetValue(region, _ => new RegionTail());
                    if (!teardown && (tail.Closed || tail.Finalizing != null))
                    {
                        throw new ObjectDisposedException(nameof(Region), "Region teardown has closed navigation admission.");
                    }
                    if (teardown && tail.Finalizing != null &&
                        (tail.Finalizing.Regions.Length != regions.Length ||
                            !tail.Finalizing.Regions.All(target => regions.Contains(target, RegionIdentity.Instance))))
                    {
                        throw new InvalidOperationException("An overlapping teardown must use the exact in-flight Region set.");
                    }
                }
                var operation = new Operation(regions, predecessors, parents, policy);
                if (policy == NavigationPolicy.LatestRequestWins)
                {
                    Operation[] obsolete = regions.SelectMany(region => Tails.GetValue(region, _ => new RegionTail()).Pending)
                        .Where(old => !old.Finished && old.Policy == NavigationPolicy.LatestRequestWins).Distinct().ToArray();
                    foreach (Operation old in obsolete)
                    {
                        old.Superseded = true;
                    }
                    operation.Invalidations = obsolete.SelectMany(old => old.Activations).Distinct().ToArray();
                }
                foreach (Operation parent in parents)
                {
                    parent.WaitingOn.Add(operation);
                }
                foreach (IRegion region in regions)
                {
                    RegionTail tail = Tails.GetValue(region, _ => new RegionTail());
                    tail.Last = operation;
                    tail.Pending.Add(operation);
                    if (teardown && !tail.Closed && tail.Finalizing == null)
                    {
                        tail.Finalizing = operation;
                    }
                }
                return operation;
            }
        }

        private static bool Reaches(Operation current, Operation target, HashSet<Operation> visited)
        {
            if (current.Finished || !visited.Add(current))
            {
                return false;
            }
            return ReferenceEquals(current, target) || current.WaitingOn.Any(next => Reaches(next, target, visited));
        }

        private sealed class RegionTail
        {
            internal Operation Last;
            internal Operation Finalizing;
            internal bool Closed;
            internal readonly HashSet<Operation> Pending = new();
        }

        private sealed class CallbackState
        {
            internal readonly List<Operation> Active = new();
        }

        private sealed class RegionIdentity : IEqualityComparer<IRegion>
        {
            internal static readonly RegionIdentity Instance = new();

            public bool Equals(IRegion x, IRegion y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(IRegion region)
            {
                return RuntimeHelpers.GetHashCode(region);
            }
        }
    }
}
