using UnityEngine;

namespace Sim.Faciem.Editor.DI
{
    public abstract class EditorServiceInstaller : ScriptableObject
    {
        public abstract void Install(IEditorInjector injector);
    }
}
