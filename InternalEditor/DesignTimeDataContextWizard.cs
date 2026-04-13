using System;
using System.Linq;
using Plugins.Sim.Faciem.Shared;
using R3;
using Sim.Faciem;
using UnityEditor;
using UnityEngine.UIElements;

namespace Plugins.Sim.Faciem.Editor
{
    public class DesignTimeDataContextWizard : EditorWindow
    {
        [MenuItem("Assets/Create/Faciem/Design-Time Data Context", false, 10)]
        static void CreateCustomObj()
        {
            var window = CreateWindow<DesignTimeDataContextWizard>("Create Design-Time Data Context");
            window.ShowPopup();
        }

        private void CreateGUI()
        {
            var disposables = rootVisualElement.RegisterDisposableBag();
            
            var designTimeDataContexts = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(DesignTimeDataContext).IsAssignableFrom(type) && !type.IsAbstract)
                .ToList();
                
            var dropDown = new DropdownField(designTimeDataContexts.Select(x => x.Name).Prepend("-").ToList(), 0);

            var isEnabledObs = dropDown
                .ObserveValueChanged()
                .Select(_ => dropDown.index)
                .Prepend(dropDown.index)
                .Select(index => index != 0);
            
            var btCreate = new Button(() =>
            {
                var currentType = dropDown.index;

                if (currentType == 0)
                {
                    return;
                }

                var type = designTimeDataContexts[currentType - 1];
                var instance = CreateInstance(type);

                var path = AssetDatabase.GetAssetPath(Selection.activeObject);
                AssetDatabase.CreateAsset(instance, path + "/" + type.Name + ".asset");
                
                Close();
            })
            {
                text = "Create"
            };
            
            disposables.Add(
                isEnabledObs
                    .Subscribe(isEnabled => btCreate.SetEnabled(isEnabled)));
            
            rootVisualElement.Add(dropDown);
            rootVisualElement.Add(btCreate);
        }
    }
}