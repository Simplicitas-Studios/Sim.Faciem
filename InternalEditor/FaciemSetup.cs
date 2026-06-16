using Sim.Faciem.Internal;
using UnityEditor;
using UnityEditor.AddressableAssets;

namespace Sim.Faciem.InternalEditor
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
