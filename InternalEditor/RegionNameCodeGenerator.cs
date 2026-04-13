using System;
using System.IO;
using Sim.Faciem;
using UnityEditor;

namespace Plugins.Sim.Faciem.Editor
{
    internal static class RegionNameCodeGenerator
    {
        public const string RegionNameCodeGenerationStart = "// BEGIN: Auto-generated code for Region Name Definitions";
        
        private const string Usings = "using Sim.Faciem;";
        private const string PropertyHeader = "\n        // Generated Property for {0} with asset id {1}\n";
        private const string PropertySyntax = "        public static RegionName {0} {{ get; }} = RegionName.From(\"{1}\");\n";

        internal static void Generate(RegionNameDefinition regionNameDefinition)
        {
            if (!regionNameDefinition.SourceCodeGeneration
                || regionNameDefinition.SourceFile == null
                || string.IsNullOrEmpty(regionNameDefinition.Name.Name))
            {
                return;
            }
            
            var guid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(regionNameDefinition));
            var text = regionNameDefinition.SourceFile.text;
            var sourceFilePath = AssetDatabase.GetAssetPath(regionNameDefinition.SourceFile);

            // Check if using is added
            if (!text.Contains(Usings))
            {
                var namespaceIndex = text.IndexOf("namespace", StringComparison.Ordinal);

                var insertIndex = namespaceIndex != -1 ? namespaceIndex : 0;

                text = text.Insert(insertIndex, Usings + "\n\n");
            }

            var guidIndex = text.IndexOf(guid.ToString(), StringComparison.Ordinal);

            var index = -1;
            
            if (guidIndex != -1)
            {
                var subText = text[..guidIndex];
                var lineStartIndex = subText.LastIndexOf('\n') + 1;
                var headerLineEnd = text.IndexOf('\n', lineStartIndex + 1);
                var propertyLineEnd = text.IndexOf('\n', headerLineEnd + 1);
                
                text = text.Remove(lineStartIndex - 2, (propertyLineEnd - lineStartIndex) + 3);
                index = lineStartIndex - 2;
            }
            else
            {
                index = text.IndexOf(RegionNameCodeGenerationStart, StringComparison.Ordinal);
                if (index != -1)
                {
                    index += RegionNameCodeGenerationStart.Length + 1;
                }
            }

            if (index == -1)
            {
                return;
            }

            var name = regionNameDefinition.Name.Name;
            var sanitizedName = MakeValidIdentifier(name);
            
            // Generate Header
            var headerLine = string.Format(PropertyHeader, regionNameDefinition.name + ".asset", guid);
            text = text.Insert(index, headerLine);
            index += headerLine.Length;
            
            // Generate Property
            text = text.Insert(index, string.Format(PropertySyntax, sanitizedName, name));

            File.WriteAllText(sourceFilePath, text);

        }
        
        private static string MakeValidIdentifier(string name)
        {
            // Replace any non-alphanumeric characters with an underscore
            string validName = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
    
            // Ensure the identifier doesn't start with a digit
            if (char.IsDigit(validName[0]))
            {
                validName = "_" + validName;
            }

            // Return a default name if the result is empty
            return string.IsNullOrEmpty(validName) ? "_Unnamed" : validName;
        }
    }
}