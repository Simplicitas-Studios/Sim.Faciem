using System.Collections.Generic;
using System.Linq;
using Sim.Faciem;
using Sim.Faciem.Internal;
using UnityEditor;

namespace Plugins.Sim.Faciem.Editor.Services
{
    public class EditorViewIdRegistry : IViewIdRegistry
    {
        private readonly Dictionary<ViewId, ViewIdAsset> _viewIds;

        public EditorViewIdRegistry()
        {
            var viewIdAssets = AssetDatabase.FindAssets($"t:{nameof(EditorViewIdAsset)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EditorViewIdAsset>)
                .Where(asset => asset != null);
            
            _viewIds = viewIdAssets
                .ToDictionary(x => x.ViewId, x => x as ViewIdAsset);
        }

        public bool TryGetViewId(ViewId viewId, out ViewIdAsset viewIdAsset)
        {
            return _viewIds.TryGetValue(viewId, out viewIdAsset);
        }
    }
}