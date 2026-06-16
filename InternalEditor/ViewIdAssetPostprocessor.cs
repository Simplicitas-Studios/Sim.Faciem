using InternalEditor;
using Sim.Faciem.Internal;
using UnityEditor;

namespace Sim.Faciem.InternalEditor.InternalEditor
{
    public class ViewIdAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ViewIdAsset>(path);

                if (asset)
                {
                    AddressableHelper.CreateAssetEntry(asset, FaciemAddressables.ViewId);
                }
            }
        }
    }
}
