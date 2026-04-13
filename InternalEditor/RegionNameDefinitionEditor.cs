using Bebop.Monads;
using Plugins.Sim.Faciem.Shared;
using R3;
using Sim.Faciem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Plugins.Sim.Faciem.Editor
{
    [CustomEditor(typeof(RegionNameDefinition))]
    public class RegionNameDefinitionEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var disposables = root.RegisterDisposableBag();

            if (target is RegionNameDefinition definition)
            {
                definition.ExecuteCodeGeneration = RegionNameCodeGenerator.Generate;
            }
            
            var nameProperty = serializedObject.FindProperty("_name");
            var namePropertyField = new PropertyField(nameProperty);
            root.Add(namePropertyField);
            
            var sourceGenerationProperty = serializedObject.FindProperty(nameof(RegionNameDefinition.SourceCodeGeneration));
            var sourceGenerationPropertyField = new PropertyField(sourceGenerationProperty);
            root.Add(sourceGenerationPropertyField);

            var sourceFileVisibilityObs = sourceGenerationPropertyField
                .ObservePropertyChanges()
                .Select(property => property.changedProperty.boolValue)
                .Prepend(sourceGenerationProperty.boolValue);

            var sourceFileContainer = new VisualElement();
            disposables.Add(sourceFileContainer.BindVisibility(sourceFileVisibilityObs));
            
            var sourceFileProperty = serializedObject.FindProperty(nameof(RegionNameDefinition.SourceFile));
            var sourceFileField = new PropertyField(sourceFileProperty);
            sourceFileContainer.Add(sourceFileField);
            
            var validationField = new Label
            {
                style =
                {
                    marginLeft = 16,
                    color = Color.red
                },
                selection =
                {
                    isSelectable = true,
                    doubleClickSelectsWord = true,
                    tripleClickSelectsLine = true
                }
            };
            disposables.Add(sourceFileField
                .ObservePropertyChanges()
                .Select(property => property.changedProperty.objectReferenceValue)
                .OfType<Object, MonoScript>()
                .Select(CheckSourceFile)
                .Subscribe(maybeText =>
                {
                    validationField.text = maybeText.OrElse(string.Empty);
                }));
            
            sourceFileContainer.Add(validationField);
            
            root.Add(sourceFileContainer);
            
            return root;
        }

        private Maybe<string> CheckSourceFile(MonoScript monoScript)
        {
            if (!monoScript.text.Contains(RegionNameCodeGenerator.RegionNameCodeGenerationStart))
            {
                return "Missing Comment for Code generation. Please add the following comment ot the file:\n" + RegionNameCodeGenerator.RegionNameCodeGenerationStart;
            }
            
            return Maybe.Nothing<string>();
        }
    }
}