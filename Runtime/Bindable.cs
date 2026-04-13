using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using R3;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    public abstract class Bindable<T> : IDataSourceViewHashProvider, INotifyBindablePropertyChanged where T : Bindable<T>
    {
        private int _viewHashCode;
        
        private Subject<Unit> _anyPropertyChanged = new();
        private event EventHandler<BindablePropertyChangedEventArgs> PropertyChanged;
        private readonly Dictionary<string, Observable<Unit>> _propertyChangeTriggers;
        
        internal Observable<BindablePropertyChangedEventArgs> PropertyChangedObs { get; }
        
        internal Observable<Unit> AnyPropertyChanged => _anyPropertyChanged;
        
        public Bindable()
        {
            _propertyChangeTriggers = new Dictionary<string, Observable<Unit>>();
            PropertyChangedObs = Observable.FromEvent<EventHandler<BindablePropertyChangedEventArgs>, BindablePropertyChangedEventArgs>(
                x => (_, args) => x(args),
                x => PropertyChanged += x,
                x => PropertyChanged -= x);
        }

        public Observable<T> ObserveAnyChange()
        {
            return PropertyChangedObs
                .Select(_ => this as T)
                .Share();
        }
        
        public Observable<TProperty> Observe<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            if (propertyExpression.Body is not MemberExpression member)
            {
                throw new ArgumentException(
                    $"Expression '{propertyExpression}' refers to a method, not a property.");
            }

            if (member.Member is not PropertyInfo propInfo)
            {
                throw new ArgumentException(
                    $"Expression '{propertyExpression}' refers to a field, not a property.");
            }
            
            var compiledExpression = propertyExpression.Compile();

            if (!_propertyChangeTriggers.TryGetValue(propInfo.Name, out var changeObs))
            { 
                changeObs = PropertyChangedObs
                    .Where(change => change.propertyName.Equals(propInfo.Name))
                    .AsUnitObservable()
                    .Share();
                _propertyChangeTriggers.Add(propInfo.Name, changeObs);
            }
            
            return changeObs
                .Select(_ => compiledExpression(this as T));
        }
        
        protected void SetProperty<TItem>(ref TItem item, TItem newValue, bool checkEqual = true, [CallerMemberName] string name = "")
        {
            if (checkEqual && (item?.Equals(newValue) ?? false))
            {
                return;
            }
            
            item = newValue;
            PropertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(name));
            _viewHashCode++;
            _anyPropertyChanged.OnNext(Unit.Default);
        }
        
        event EventHandler<BindablePropertyChangedEventArgs> INotifyBindablePropertyChanged.propertyChanged
        {
            add => PropertyChanged += value;
            remove => PropertyChanged -= value;
        }

        public long GetViewHashCode() => _viewHashCode;
    }
}