namespace Sim.Faciem.Internal
{
    public interface IViewIdRegistry
    {
        bool TryGetViewId(ViewId viewId, out ViewIdAsset viewIdAsset);
    }
}