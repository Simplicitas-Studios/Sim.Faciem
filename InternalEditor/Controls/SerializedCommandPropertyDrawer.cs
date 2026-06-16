using Sim.Faciem.CommandBinding;
using UnityEditor;
using UnityEngine.UIElements;
#if UNITY_EDITOR
#endif

namespace Sim.Faciem.Controls.Editor
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SerializedCommand))]
    public class SerializedCommandPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new Label("Command Binding");
        }
    }
#endif
}
