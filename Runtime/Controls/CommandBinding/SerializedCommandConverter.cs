#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif

namespace Sim.Faciem.CommandBinding
{
#if UNITY_EDITOR
    public class SerializedCommandConverter : UxmlAttributeConverter<SerializedCommand>
    {
        public override SerializedCommand FromString(string value)
        {
            return new SerializedCommand
            {
                Name = value
            };
        }
    }
#endif
}