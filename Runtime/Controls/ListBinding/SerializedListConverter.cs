#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif

namespace Sim.Faciem.ListBinding
{
#if UNITY_EDITOR
    public class SerializedListConverter: UxmlAttributeConverter<SerializedListReference>
    {
        public override SerializedListReference FromString(string value)
        {
            return new SerializedListReference();
        }
    }
#endif
}