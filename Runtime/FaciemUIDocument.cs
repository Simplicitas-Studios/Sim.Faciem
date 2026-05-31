using System;
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

        private async UniTaskVoid Awake()
        {
            await WaitApplicationSetup();
            FaciemBridge.RootViewId = _rootViewId.ViewId;

            var document = GetComponent<UIDocument>();

            if (FaciemBridge.ContainerBridge == null)
            {
                Debug.LogError("Faciem DI Bridge is not setup!");
                return;
            }

            var constructionService = CreateConstructionService();

            var vm = constructionService.CreateInstance<ShellViewModel>();
            document.rootVisualElement.dataSource = vm;

            var regions = document.rootVisualElement.Query<Region>().ToList();

            foreach (var innerRegion in regions)
            {
                innerRegion.RegisterDirect(vm);
            }

            await UniTask.Delay(TimeSpan.FromMilliseconds(100));
            await vm.NavigateToInternal();
        }

        protected virtual ValueTask WaitApplicationSetup() => default;

        protected virtual IViewModelConstructionService CreateConstructionService()
        {
            return FaciemBridge.ContainerBridge.ResolveInstance<IViewModelConstructionService>();
        }
    }
}
