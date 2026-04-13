using System.Collections.Generic;
using Bebop.Monads;

namespace Sim.Faciem
{
    public class RegionManager
    {
        private readonly Dictionary<RegionName, List<IRegion>> _regions = new();
        
        public Maybe<RegionManager> Parent { get; set; }

        public RegionManager()
        {
            Parent = Maybe.Nothing<RegionManager>();
        }
        
        public RegionManager(RegionManager parent)
        {
            Parent = Maybe.From(parent);
        }

        public void AddRegion(IRegion region)
        {
            if (!_regions.TryGetValue(region.RegionName, out var regionList))
            {
                regionList = new List<IRegion>();
                _regions.Add(region.RegionName, regionList);
            }
            
            regionList.Add(region);
        }

        public void RemoveRegion(IRegion region)
        {
            if (_regions.TryGetValue(region.RegionName, out var regionList))
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
            var hasItem = _regions.TryGetValue(regionName, out var outRegionList);
            regionList = outRegionList;
            return hasItem;
        }
    }
}