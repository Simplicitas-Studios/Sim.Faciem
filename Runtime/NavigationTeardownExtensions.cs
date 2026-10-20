using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sim.Faciem.Internal;

namespace Sim.Faciem
{
    /// <summary>Explicit final release, unlike Clear. The application must affirm final ownership of every cached model.</summary>
    public static class NavigationTeardownExtensions
    {
        /// <summary>
        /// Terminal FIFO teardown of explicitly selected native Regions. Earlier operations drain; later submissions reject.
        /// ownsModel transfers final-disposal authority, not discovery of DI ownership. Use a scoped handle inside callbacks.
        /// </summary>
        public static UniTask<NavigationResult> TeardownResult(this INavigationService service,
            IReadOnlyList<Region> regions, Func<BaseViewModel, bool> ownsModel)
        {
            return service is NavigationService native
                ? native.TeardownFrom(null, regions, ownsModel)
                : UniTask.FromResult(NavigationExecution.Unsupported("Custom navigation services do not support final teardown."));
        }

        /// <summary>Same teardown contract with lifecycle-origin reentrancy/cycle protection.</summary>
        public static UniTask<NavigationResult> TeardownResult(this IViewModelNavigationService service,
            IReadOnlyList<Region> regions, Func<BaseViewModel, bool> ownsModel)
        {
            return service is ViewModelNavigationService scoped
                ? scoped.TeardownResult(regions, ownsModel)
                : UniTask.FromResult(NavigationExecution.Unsupported("Custom scoped navigation services do not support final teardown."));
        }
    }
}
