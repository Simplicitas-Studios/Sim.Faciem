using System;
using R3;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine.UIElements;

namespace Plugins.Sim.Faciem.Shared
{
    public static class VisualElementsExtensions
    {
        public static DisposableBagHolder RegisterDisposableBag(this VisualElement element)
        {
            var bag = new DisposableBagHolder();

            var subscription = element
                .ObserveEvent<DetachFromPanelEvent>()
                .Subscribe(_ =>
                {
                    bag.Dispose();
                });
            bag.Add(subscription);

            return bag;
        }

        public static void Show(this VisualElement visualElement)
        {
            visualElement.style.display = DisplayStyle.Flex;
        }

        public static void Hide(this VisualElement visualElement)
        {
            visualElement.style.display = DisplayStyle.None;
        }
        
        public static Observable<T> ObserveEvent<T>(this VisualElement element)
            where T : EventBase<T>, new()
        {
            return Observable.FromEvent<EventCallback<T>, T>(
                action => t => action(t),
                action => element.RegisterCallback(action),
                action => element.UnregisterCallback(action));
        }

#if UNITY_EDITOR
        public static Observable<SerializedPropertyChangeEvent> ObservePropertyChanges(this PropertyField element)
        {
            return element.ObserveEvent<SerializedPropertyChangeEvent>();
        }

#endif
        public static Observable<T> ObserveValueChanged<T>(this INotifyValueChanged<T> element)
        {
            return Observable.FromEvent<EventCallback<ChangeEvent<T>>, T>(
                action => t => action(t.newValue),
                action => element.RegisterValueChangedCallback(action),
                action => element.UnregisterValueChangedCallback(action));
        }

        public static IDisposable BindVisibility(this VisualElement visualElement, Observable<bool> visibility)
        {
            return visibility.Subscribe(isVisible =>
            {
                visualElement.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }
    }
}