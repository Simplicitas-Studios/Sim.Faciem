using Sim.Faciem.ListBinding;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UIElements;

namespace Plugins.Sim.Faciem.InternalEditor.Controls
{

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SerializedListReference))]
    public class SerializedListReferencePropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new Label("Item Source Binding");
        }
    }
#endif
}