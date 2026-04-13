using System.Collections;
using Sim.Faciem.CommandBinding;
using Sim.Faciem.Commands;
using Sim.Faciem.ListBinding;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
namespace Sim.Faciem
{
    public class ControlConverterRegistry
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
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