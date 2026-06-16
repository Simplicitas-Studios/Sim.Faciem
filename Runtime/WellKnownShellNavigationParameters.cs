using Sim.Utility.PropertyModel;

namespace Sim.Faciem
{
    internal static class WellKnownShellNavigationParameters
    {
        public static IPropertyKey<ViewId> InitialViewId { get; } = PropertyKey.From<ViewId>("Faciem/InitialViewId");
    }
}
