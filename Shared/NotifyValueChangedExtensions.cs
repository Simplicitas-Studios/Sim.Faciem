using R3;
using UnityEngine.UIElements;

namespace Plugins.Sim.Faciem.Shared
{
    public static class NotifyValueChangedExtensions
    {
        public static Observable<T> ObserveChanges<T>(this INotifyValueChanged<T> control)
        {
            return Observable.FromEvent<EventCallback<ChangeEvent<T>>, ChangeEvent<T>>(
                    x => x.Invoke,
                    x => (control as CallbackEventHandler)?.RegisterCallback(x),
                    x => (control as CallbackEventHandler)?.UnregisterCallback(x))
                .Select(change => change.newValue);
        }
    }
}