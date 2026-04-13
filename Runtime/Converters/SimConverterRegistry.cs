using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sim.Faciem.Converters
{
    internal class SimConverterRegistry
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        public static void RegisterConverters()
        {
            // bool to DisplayStyle
            ConverterGroups.RegisterGlobalConverter((ref bool handle) => handle
                ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex)
                : new StyleEnum<DisplayStyle>(DisplayStyle.None));

            var group = new ConverterGroup("Inverse-BoolVisibility");

            group.AddConverter((ref bool handle) => handle
                ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : new StyleEnum<DisplayStyle>(DisplayStyle.Flex));

            ConverterGroups.RegisterConverterGroup(group);

            ConverterGroups.RegisterGlobalConverter((ref string handle) => string.IsNullOrEmpty(handle));
            
            ConverterGroups.RegisterGlobalConverter((ref string handle) => string.IsNullOrEmpty(handle)
                ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : new StyleEnum<DisplayStyle>(DisplayStyle.Flex));
            
            var stringToBoolInverseGroup = new ConverterGroup("String Not Empty");
            
            group.AddConverter((ref string handle) => !string.IsNullOrEmpty(handle));
            
            ConverterGroups.RegisterConverterGroup(stringToBoolInverseGroup);
            
            var stringToVisibilityInverseGroup = new ConverterGroup("Show If String Empty");
            
            group.AddConverter((ref string handle) => string.IsNullOrEmpty(handle)
                ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex)
                : new StyleEnum<DisplayStyle>(DisplayStyle.None));
            
            ConverterGroups.RegisterConverterGroup(stringToVisibilityInverseGroup);
            
            // // Create local Converters.
            // var group = new ConverterGroup("Bool-Visibility");
            //
            // // Converter groups can have multiple converters. This example converts a float to both a color and a string.
            // group.AddConverter((ref bool active) => active
            //     ?new StyleEnum<DisplayStyle>(DisplayStyle.Flex)
            //     : new StyleEnum<DisplayStyle>(DisplayStyle.None));
            //
            // group.AddConverter((ref StyleEnum<DisplayStyle>displayStyle) => displayStyle == DisplayStyle.Flex);
            //
            // // Register the converter group in InitializeOnLoadMethod to make it accessible from the UI Builder.
            // ConverterGroups.RegisterConverterGroup(group);
        }
    }
}