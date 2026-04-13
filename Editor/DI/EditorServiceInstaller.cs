using UnityEngine;

namespace Plugins.Sim.Faciem.Editor.DI
{
    public abstract class EditorServiceInstaller : ScriptableObject
    {
        public abstract void Install(IEditorInjector injector);
    }
}