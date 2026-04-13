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
    [CustomEditor(typeof(ViewIdAsset), true)]
    public class ViewIdAssetEditor : UnityEditor.Editor
    {
        [SerializeField]
        private VisualTreeAsset _template;
        
        public override VisualElement CreateInspectorGUI()
        {
            var view = _template.CloneTree();
            view.dataSource = target as ViewIdAsset;
            if (target is ViewIdAsset definition)
            {
                definition.ExecuteCodeGeneration = ViewIdCodeGenerator.Generate;
            }
            
            var disposables = view.RegisterDisposableBag();
            
            var sourceFileField = view.Q<PropertyField>("pfSourceGenerationFile");
            var validationField = view.Q<Label>("lbGenerationValidation");
            
            disposables.Add(sourceFileField
                .ObservePropertyChanges()
                .Select(property => property.changedProperty.objectReferenceValue)
                .OfType<Object, MonoScript>()
                .Select(CheckSourceFile)
                .Subscribe(maybeText =>
                {
                    validationField.text = maybeText.OrElse(string.Empty);
                }));
            
            return view;
        }
        
        private Maybe<string> CheckSourceFile(MonoScript monoScript)
        {
            if (!monoScript.text.Contains(ViewIdCodeGenerator.ViewIdCodeGenerationStart))
            {
                return "Missing Comment for Code generation. Please add the following comment ot the file:\n" + ViewIdCodeGenerator.ViewIdCodeGenerationStart;
            }
            
            return Maybe.Nothing<string>();
        }
    }
}
