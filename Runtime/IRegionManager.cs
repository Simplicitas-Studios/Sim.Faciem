using System.Collections.Generic;

namespace Sim.Faciem
{
    public interface IRegionManager
    {
        public bool TryFindRegion(RegionName regionName, out IReadOnlyList<IRegion> regionList);

        void AddRegion(IRegion region);

        void RemoveRegion(IRegion region);
    }
}
