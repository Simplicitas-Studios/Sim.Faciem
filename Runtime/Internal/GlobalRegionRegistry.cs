using System.Collections.Generic;
using Sim.Faciem.Internal;

namespace Sim.Faciem
{
    internal class GlobalRegionRegistry : IGlobalRegionManagerInternal
    {
        private readonly Dictionary<RegionName, List<IRegion>> _regions = new();

        public void AddRegion(IRegion region)
        {
            if(!_regions.TryGetValue(region.RegionName, out var regionList))
            {
                regionList = new List<IRegion>();
                _regions.Add(region.RegionName, regionList);
            }

            regionList.Add(region);
        }

        public void RemoveRegion(IRegion region)
        {
            if(_regions.TryGetValue(region.RegionName, out var regionList))
            {
                regionList.Remove(region);
                if (regionList.Count == 0)
                {
                    _regions.Remove(region.RegionName);
                }
            }
        }

        public bool TryFindRegion(RegionName regionName, out IReadOnlyList<IRegion> regionList)
        {
            if (_regions.TryGetValue(regionName, out var regions))
            {
                regionList = regions;
                return true;
            }

            regionList = null;
            return false;
        }
    }
}
