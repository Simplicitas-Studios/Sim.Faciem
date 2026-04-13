using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
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
                ViewModel.TypeName = ViewModel.Script.GetClass().AssemblyQualifiedName;
            }
            
            if (DataContext != null && DataContext.Script != null)
            {
                DataContext.TypeName = DataContext.Script.GetClass().AssemblyQualifiedName;
            }
        }

#endif
    }
}
