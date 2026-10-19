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
            internal Operation[] Predecessors;
            internal bool Finished;

            internal Operation(IRegion[] regions, Operation[] predecessors, Operation[] parents)
            {
                Regions = regions;
                Predecessors = predecessors;
                Parents = parents;
                WaitingOn.UnionWith(predecessors);
            }
        }

        internal static IRegion[] Snapshot(IReadOnlyList<IRegion> regions)
        {
            return regions.Distinct(RegionIdentity.Instance).ToArray();
        }

        internal static async UniTask Run(IRegion[] regions, BaseViewModel caller,
            Func<Operation, UniTask> action)
        {
            Operation operation = Reserve(regions, caller);
            try
            {
                foreach (Operation predecessor in operation.Predecessors)
                {
                    await predecessor.Completion.Task;
                }
                lock (Gate)
                {
                    operation.WaitingOn.ExceptWith(operation.Predecessors);
                    operation.Predecessors = Array.Empty<Operation>();
                }
                await action(operation);
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

        internal static async UniTask InvokeLifecycle(Operation operation, BaseViewModel model,
            Func<UniTask> callback)
        {
            CallbackState state;
            lock (Gate)
            {
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

        private static Operation Reserve(IRegion[] regions, BaseViewModel caller)
        {
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

                var operation = new Operation(regions, predecessors, parents);
                foreach (Operation parent in parents)
                {
                    parent.WaitingOn.Add(operation);
                }
                foreach (IRegion region in regions)
                {
                    Tails.GetValue(region, _ => new RegionTail()).Last = operation;
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
