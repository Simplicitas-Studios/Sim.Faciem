using Sim.Faciem.CommandBinding;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UIElements;

namespace Plugins.Sim.Faciem.InternalEditor.Controls
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