using System;
using System.IO;
using Sim.Faciem;
using UnityEditor;

namespace Plugins.Sim.Faciem.Editor
{
    internal class ViewIdCodeGenerator
    {
        public const string ViewIdCodeGenerationStart = "// BEGIN: Auto-generated code for View Id Assets";

        private const string Usings = "using Sim.Faciem;";
        private const string PropertyHeader = "\n        // Generated Property for {0} with asset id {1}\n";

        private const string PropertySyntax =
            "        public static ViewId {0} {{ get; }} = ViewId.From(\"{1}\");\n";

        internal static void Generate(ViewIdAsset regionNameDefinition)
        {
            if (!regionNameDefinition.SourceCodeGeneration
                || regionNameDefinition.SourceFile == null
                || string.IsNullOrEmpty(regionNameDefinition.ViewId.Id))
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

                text = text.Remove(lineStartIndex, (propertyLineEnd - lineStartIndex) + 2);
                index = lineStartIndex - 1;
            }
            else
            {
                index = text.IndexOf(ViewIdCodeGenerationStart, StringComparison.Ordinal);
                if (index != -1)
                {
                    index += ViewIdCodeGenerationStart.Length + 1;
                }
            }

            if (index == -1)
            {
                return;
            }

            var name = regionNameDefinition.ViewId.Id;
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