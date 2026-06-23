using Sim.Faciem.Editor.DI;
using Sim.Faciem.Editor.Services;
using Sim.Faciem.Internal;

namespace Sim.Faciem.Editor
{
    internal class FaciemEditorServiceInstaller : EditorServiceInstaller
    {
        public override void Install(IEditorInjector injector)
        {
            injector.Register<IViewModelConstructionService, ViewModelConstructionService>();
            injector.Register<IViewIdRegistry, EditorViewIdRegistry>();
            injector.Register<INavigationService, NavigationService>();
            injector.Register<IGlobalRegionManager, GlobalRegionRegistry>(false, typeof(IGlobalRegionManagerInternal));
        }
    }
}
