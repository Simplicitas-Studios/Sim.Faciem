using UnityEditor;
using UnityEditor.AddressableAssets;

namespace Plugins.Sim.Faciem.Editor
{
    public static class FaciemSetup
    {
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            var labels = AddressableAssetSettingsDefaultObject.Settings.GetLabels();

            if (!labels.Contains(FaciemAddressables.ViewId))
            {
                AddressableAssetSettingsDefaultObject.Settings.AddLabel(FaciemAddressables.ViewId);
            }
        }
    }
}