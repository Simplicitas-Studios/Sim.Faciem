using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Plugins.Sim.Faciem.Editor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sim.Faciem.Internal
{
    public static class ViewIdDiscoveryService
    {
        public static Dictionary<ViewId, ViewIdAsset> ViewIds { get; } = new();

        // TODO: Carefully - this is a blocking call as not all frameworks supports async registration
        public static void RegisterViewIds(IDIRegistrationBridge registrationBridge)
        {
            ViewIds.Clear();
            var label = new AssetLabelReference { labelString = FaciemAddressables.ViewId };
            var viewIds = Addressables
                .LoadAssetsAsync<ViewIdAsset>(new [] { FaciemAddressables.ViewId  }, _ => { }, Addressables.MergeMode.Intersection)
                .WaitForCompletion();

            if (viewIds == null)
            {
                return;
            }

            RegisterViewIdsInternal(registrationBridge, viewIds.ToList());
        }

        public static async UniTask RegisterViewIdsAsync(IDIRegistrationBridge registrationBridge)
        {
            ViewIds.Clear();
            var label = new AssetLabelReference { labelString = FaciemAddressables.ViewId };
            var viewIds = await Addressables
                .LoadAssetsAsync<ViewIdAsset>(new[] { label }, _ => { }, Addressables.MergeMode.Intersection)
                .Task;

            if (viewIds == null)
            {
                return;
            }

            RegisterViewIdsInternal(registrationBridge, viewIds.ToList());
        }

        private static void RegisterViewIdsInternal(IDIRegistrationBridge registrationBridge, IReadOnlyList<ViewIdAsset> viewIds)
        {
            foreach (var viewIdAsset in viewIds)
            {
                ViewIds.Add(viewIdAsset.ViewId, viewIdAsset);

                var dataContextType = viewIdAsset.DataContext.GetReferencedType();
                var viewModelType = viewIdAsset.ViewModel.GetReferencedType();
                registrationBridge.RegisterTransient(
                    dataContextType,
                    viewModelType);   
            }
        }
    }
}