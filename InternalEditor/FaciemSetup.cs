using Sim.Faciem.Internal;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Sim.Faciem.InternalEditor
{
    public static class FaciemSetup
    {
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            if (!AddressableAssetSettingsDefaultObject.Settings)
            {
                Debug.LogWarning("Addressable Asset Settings not found. Please ensure that the Addressables package is installed and configured.");
                return;
            }

            var labels = AddressableAssetSettingsDefaultObject.Settings.GetLabels();

            if (!labels.Contains(FaciemAddressables.ViewId))
            {
                AddressableAssetSettingsDefaultObject.Settings.AddLabel(FaciemAddressables.ViewId);
            }
        }
    }
}
