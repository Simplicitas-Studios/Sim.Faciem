using System.Collections;
using Sim.Faciem.CommandBinding;
using Sim.Faciem.Commands;
using Sim.Faciem.ListBinding;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    public class ControlConverterRegistry
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        public static void RegisterConverters()
        {
            // ReactiveProperties

            // Command
            ConverterGroups.RegisterGlobalConverter((ref Command command) => new SerializedCommand(command));

            // List
            ConverterGroups.RegisterGlobalConverter((ref IList list) => new SerializedListReference(list));

            var group = new ConverterGroup("Inverters");

            group.AddConverter((ref bool value) => !value);
            ConverterGroups.RegisterConverterGroup(group);
        }
    }
}
