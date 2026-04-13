using System.Collections.Generic;
using Bebop.Monads;
using R3;
using UnityEngine.UIElements;
using Unit = R3.Unit;

namespace Sim.Faciem
{
    public interface IRegion
    {
        bool SupportMultipleViews { get; }
        
        RegionName RegionName { get; }
        
        IReadOnlyList<ViewId> ActiveViews { get; }
        
        Observable<Unit> Destroyed { get; }

        void AddView(ViewId viewId, VisualElement view);
        
        bool TryGetView(ViewId viewId, out VisualElement view);
        
        void ActivateView(ViewId viewId);
        
        void DeactivateView(ViewId viewId);

        void DeactivateAllViews();
    }
}