using Sim.Faciem.Editor;
using UnityEditor;
using UnityEngine;

namespace Sim.Faciem.InternalEditor
{
    public static class ViewIdAssetFactory
    {
        [MenuItem("Assets/Create/Sim/Faciem/Editor/ViewId", false, 11)]
        public static void CreateEditorViewIdAsset()
        {
            var viewIdAsset = ScriptableObject.CreateInstance<EditorViewIdAsset>();
            viewIdAsset.name = "EditorViewId";

            var currentPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            AssetDatabase.CreateAsset(viewIdAsset, currentPath + "/" + viewIdAsset.name + ".asset");
        }
    }
}
