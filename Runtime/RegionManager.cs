using System.Collections.Generic;
using Sim.Utility;

namespace Sim.Faciem
{
    public class RegionManager : IViewModelRegionManager
    {
        private readonly Dictionary<RegionName, List<IRegion>> _regions = new();

        public Maybe<IRegionManager> Parent { get; set; }

        public RegionManager()
        {
            Parent = Maybe.None<IRegionManager>();
        }

        public RegionManager(IRegionManager parent)
        {
            Parent = Maybe.Some(parent);
        }

        internal bool HasLocalRegions => _regions.Count != 0;

        internal void ReleaseReferences()
        {
            _regions.Clear();
            Parent = Maybe.None<IRegionManager>();
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
            if (_regions.TryGetValue(regionName, out var outRegionList))
            {
                regionList = outRegionList;
                return true;
            }

            if (Parent.IsSome)
            {
                return Parent.Value.TryFindRegion(regionName, out regionList);
            }

            regionList = null;
            return false;
        }
    }
}
