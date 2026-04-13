namespace Sim.Faciem.Internal
{
    internal class ViewIdRegistry : IViewIdRegistry
    {
        public bool TryGetViewId(ViewId viewId, out ViewIdAsset viewIdAsset)
        {
            return ViewIdDiscoveryService.ViewIds.TryGetValue(viewId, out viewIdAsset);
        }
    }
}