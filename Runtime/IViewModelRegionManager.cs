using Sim.Utility;

namespace Sim.Faciem
{
    public interface IViewModelRegionManager : IRegionManager
    {
        Maybe<IRegionManager> Parent { get; set; }
    }
}
