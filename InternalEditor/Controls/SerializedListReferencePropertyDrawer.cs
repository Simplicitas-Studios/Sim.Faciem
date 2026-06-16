using Sim.Faciem.ListBinding;
using UnityEditor;
using UnityEngine.UIElements;
#if UNITY_EDITOR
#endif

namespace Sim.Faciem.Controls.Editor
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
