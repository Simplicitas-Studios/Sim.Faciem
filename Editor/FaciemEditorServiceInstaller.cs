using Plugins.Sim.Faciem.Editor.DI;
using Plugins.Sim.Faciem.Editor.Services;
using Sim.Faciem;
using Sim.Faciem.Internal;
using UnityEngine;

namespace Plugins.Sim.Faciem.Editor
{
    internal class FaciemEditorServiceInstaller : EditorServiceInstaller
    {
        public override void Install(IEditorInjector injector)
        {
            injector.Register<IViewModelConstructionService, ViewModelConstructionService>();
            injector.Register<IViewIdRegistry, EditorViewIdRegistry>();
            injector.Register<INavigationService, NavigationService>();
        }
    }
}