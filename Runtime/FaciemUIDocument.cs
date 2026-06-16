using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    [RequireComponent(typeof(UIDocument))]
    public class FaciemUIDocument : MonoBehaviour
    {
        [SerializeField]
        private ViewIdAsset _rootViewId;

        private IDisposable _detachDisposable;

        private async UniTaskVoid OnEnable()
        {
            await WaitApplicationSetup();
            var document = GetComponent<UIDocument>();

            if (FaciemBridge.ContainerBridge == null)
            {
                Debug.LogError("Faciem DI Bridge is not setup!");
                return;
            }

            var constructionService = CreateConstructionService();

            await SetupSimFaciem(document, constructionService, destroyCancellationToken);
        }

        private async ValueTask SetupSimFaciem(UIDocument document, IViewModelConstructionService constructionService, CancellationToken ct)
        {
            var vm = constructionService.CreateInstance<ShellViewModel>();
            document.rootVisualElement.dataSource = vm;

            var regions = document.rootVisualElement.Query<Region>().ToList();

            foreach (var innerRegion in regions)
            {
                innerRegion.RegisterDirect(vm);
            }

            var navParams = ConstructNavigationParameters();
            navParams.Add(WellKnownShellNavigationParameters.InitialViewId, _rootViewId.ViewId);
            await UniTask.Delay(TimeSpan.FromMilliseconds(100), cancellationToken: ct); // Wait a frame for everything to be registered
            await vm.NavigateToInternal(navParams);
        }

        protected virtual NavigationParameters ConstructNavigationParameters() => NavigationParameters.Empty;

        protected virtual ValueTask WaitApplicationSetup() => default;

        protected virtual IViewModelConstructionService CreateConstructionService()
        {
            return FaciemBridge.ContainerBridge.ResolveInstance<IViewModelConstructionService>();
        }
    }
}
