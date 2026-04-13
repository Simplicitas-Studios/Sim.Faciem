using R3;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    public static class VisualElementObservableExtensions
    {
        public static Observable<ChangeEvent<T>> ObservableValueChanged<T>(this INotifyValueChanged<T> source)
        {
            return Observable.FromEvent<EventCallback<ChangeEvent<T>>, ChangeEvent<T>>(
                x => y => x(y),
                x => source.RegisterValueChangedCallback(x),
                x => source.UnregisterValueChangedCallback(x));
        }
        
        public static Observable<DetachFromPanelEvent> DetachFromPanelAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<DetachFromPanelEvent>();
        }

        public static Observable<AttachToPanelEvent> AttachToPanelAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<AttachToPanelEvent>();
        }
        
        #if UNITY_EDITOR
        public static Observable<DragPerformEvent> DragPerformedAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<DragPerformEvent>();
        }
        #endif

        public static Observable<PointerEnterEvent> PointerEnterAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<PointerEnterEvent>();
        }

        public static Observable<PointerUpEvent> PointerUpAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<PointerUpEvent>();
        }

        public static Observable<PointerDownEvent> PointerDownAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<PointerDownEvent>();
        }

        public static Observable<MouseEnterEvent> MouseEnterAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<MouseEnterEvent>();
        }

        public static Observable<MouseLeaveEvent> MouseLeaveAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<MouseLeaveEvent>();
        }
        
        public static Observable<MouseOverEvent> MouseOverAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<MouseOverEvent>();
        }
        
        public static Observable<MouseDownEvent> MouseDownAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<MouseDownEvent>();
        }
        
        public static Observable<MouseMoveEvent> MouseMoveObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<MouseMoveEvent>();
        }
        
        public static Observable<MouseUpEvent> MouseUpAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<MouseUpEvent>();
        }
        
        public static Observable<WheelEvent> WheelEventAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<WheelEvent>();
        }

        public static Observable<ClickEvent> ClickAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<ClickEvent>();
        }

        public static Observable<FocusInEvent> FocusInAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<FocusInEvent>();
        }

        public static Observable<BlurEvent> BlurAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<BlurEvent>();
        }

        public static Observable<FocusOutEvent> FocusOutAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<FocusOutEvent>();
        }

        public static Observable<GeometryChangedEvent> GeometryChangedAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<GeometryChangedEvent>();
        }

        #if UNITY_EDITOR
        public static Observable<SerializedPropertyChangeEvent> ObserveValueChanged(this PropertyField field)
        {
            return field.VisualElementEventAsObservable<SerializedPropertyChangeEvent>();
        }
        #endif

        public static Observable<KeyDownEvent> KeyDownAsObservable(this VisualElement visualElement)
        {
            return visualElement.VisualElementEventAsObservable<KeyDownEvent>();
        }
        
        internal static Observable<T> VisualElementEventAsObservable<T>(this CallbackEventHandler visualElement) where T : EventBase<T>, new()
        {
            return Observable.FromEvent<EventCallback<T>, T>(
                x => x.Invoke,
                x => visualElement.RegisterCallback(x),
                x => visualElement.UnregisterCallback(x));
        }
    }
}