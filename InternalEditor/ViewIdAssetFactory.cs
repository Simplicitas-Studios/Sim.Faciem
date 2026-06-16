using InternalEditor;
using Sim.Faciem.Editor;
using Sim.Faciem.Internal;
using UnityEditor;
using UnityEngine;

namespace Sim.Faciem.InternalEditor
{
    public static class ViewIdAssetFactory
    {
        [MenuItem("Assets/Create/Sim/Faciem/ViewId", false, 10)]
        public static void CreateViewIdAsset()
        {
            var viewIdAsset = ScriptableObject.CreateInstance<ViewIdAsset>();
            viewIdAsset.name = "ViewId";

            var currentPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            AssetDatabase.CreateAsset(viewIdAsset, currentPath + "/" + viewIdAsset.name + ".asset");

            var entry = AddressableHelper.CreateAssetEntry(viewIdAsset);
            entry.SetLabel(FaciemAddressables.ViewId, true, true);
        }

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
