using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    [CreateAssetMenu(fileName = "ViewIdAsset", menuName = "Sim/Faciem/ViewId", order = 0)]
    public class ViewIdAsset : ScriptableObject
    {
        public ViewId ViewId;

        public VisualTreeAsset View;

        [MonoScriptReferenceFilter(typeof(IDataContext))]
        public MonoScriptReference DataContext;

        [MonoScriptReferenceDependentFilter(nameof(DataContext))]
        [MonoScriptReferenceFilter(typeof(ViewModel<>))]
        public MonoScriptReference ViewModel;

        public bool SourceCodeGeneration;

#if UNITY_EDITOR
        public MonoScript SourceFile;
        internal Action<ViewIdAsset> ExecuteCodeGeneration { get; set; }

        private void OnValidate()
        {
            ExecuteCodeGeneration?.Invoke(this);

            if (ViewModel != null && ViewModel.Script != null)
            {
                var classType = ViewModel.Script.GetClass();

                if (classType != null)
                {
                    ViewModel.TypeName = classType.AssemblyQualifiedName;
                }
            }

            if (DataContext != null && DataContext.Script != null)
            {
                var classType = DataContext.Script.GetClass();

                if (classType != null)
                {
                    DataContext.TypeName = classType.AssemblyQualifiedName;
                }
            }
        }

#endif
    }
}
