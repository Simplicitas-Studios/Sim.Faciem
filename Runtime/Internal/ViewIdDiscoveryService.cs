using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sim.Faciem.Internal
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class ViewIdDiscoveryService
    {
        public static Dictionary<ViewId, ViewIdAsset> ViewIds { get; } = new();

        static ViewIdDiscoveryService()
        {
            ViewIds.Clear();
        }

        // TODO: Carefully - this is a blocking call as not all frameworks supports async registration
        public static void RegisterViewIds(IDIRegistrationBridge registrationBridge)
        {
            if (ViewIds.Any())
            {
                RegisterViewIdsInternal(registrationBridge, ViewIds.Values);
                return;
            }

            var viewIds = QueryViewIdsSync();

            if (viewIds == null)
            {
                return;
            }

            var viewIdList = viewIds.ToList();
            SetupViewIds(viewIdList);
            RegisterViewIdsInternal(registrationBridge, viewIdList);
        }

        public static async UniTask RegisterViewIdsAsync(IDIRegistrationBridge registrationBridge)
        {
            if (ViewIds.Any())
            {
                RegisterViewIdsInternal(registrationBridge, ViewIds.Values);
                return;
            }

            var viewIds = await QueryViewIdsAsync();

            if (viewIds == null)
            {
                return;
            }

            var viewIdList = viewIds.ToList();
            SetupViewIds(viewIdList);
            RegisterViewIdsInternal(registrationBridge, viewIdList);
        }

        private static IList<ViewIdAsset> QueryViewIdsSync()
        {
            var viewIds = Addressables
                .LoadAssetsAsync<ViewIdAsset>(new[] { FaciemAddressables.ViewId }, _ => { },
                    Addressables.MergeMode.Intersection)
                .WaitForCompletion();
            return viewIds;
        }

        private static async UniTask<IList<ViewIdAsset>> QueryViewIdsAsync()
        {
            var label = new AssetLabelReference { labelString = FaciemAddressables.ViewId };
            var viewIds = await Addressables
                .LoadAssetsAsync<ViewIdAsset>(new[] { label }, _ => { }, Addressables.MergeMode.Intersection)
                .Task;
            return viewIds;
        }

        private static void SetupViewIds(IReadOnlyCollection<ViewIdAsset> viewIds)
        {
            foreach (var viewIdAsset in viewIds)
            {
                ViewIds[viewIdAsset.ViewId] = viewIdAsset;
            }
        }

        private static void RegisterViewIdsInternal(IDIRegistrationBridge registrationBridge, IReadOnlyCollection<ViewIdAsset> viewIds)
        {
            foreach (var viewIdAsset in viewIds)
            {
                var dataContextType = viewIdAsset.DataContext.GetReferencedType();
                var viewModelType = viewIdAsset.ViewModel.GetReferencedType();
                registrationBridge.RegisterTransient(
                    dataContextType,
                    viewModelType);
            }
        }
    }
}
