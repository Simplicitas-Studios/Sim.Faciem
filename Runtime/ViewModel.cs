
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using R3;
using Sim.Faciem.PropertyObserver;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    public abstract class ViewModel<T> : BaseViewModel, IDataSourceViewHashProvider, INotifyBindablePropertyChanged where T : ViewModel<T>
    {
        private long _viewHashCode = 0;
        private Dictionary<string, IDisposable> _nestedSubscriptions = new();
        
        private event EventHandler<BindablePropertyChangedEventArgs> PropertyChanged;

        internal Observable<BindablePropertyChangedEventArgs> PropertyChangedObs { get; set; }
        
        protected IViewModelPropertyObserver<T> Property { get; }
        
        protected ViewModel()
        {
            PropertyChangedObs = Observable.FromEvent<EventHandler<BindablePropertyChangedEventArgs>, BindablePropertyChangedEventArgs>(
                    x => (_, args) => x(args),
                    x => PropertyChanged += x,
                    x => PropertyChanged -= x);

            Property = new ViewModelPropertyObserver<T>(this as T);
            
            Disposables.Add(
                Disposable.Create(() =>
                {
                    foreach (var nestedSubscription in _nestedSubscriptions)
                    {
                        nestedSubscription.Value.Dispose();
                    }
                }));
        }

        public void AddNestedBindable<TProperty>(Expression<Func<T, TProperty>> propertyExpression) where TProperty : Bindable<TProperty>
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
            
            var bindable = compiledExpression(this as T);
            
            if(_nestedSubscriptions.TryGetValue(propInfo.Name, out var oldSubscription))
            {
                oldSubscription.Dispose();
            }
            
            _nestedSubscriptions[propInfo.Name] = bindable
                .PropertyChangedObs
                .Subscribe(changedProperty =>
                {
                    PropertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs($"{propInfo.Name}.{changedProperty.propertyName.ToString()}"));
                    _viewHashCode++;
                });
        }
        
        protected void SetProperty<TItem>(ref TItem item, TItem newValue, [CallerMemberName] string name = "")
        {
            if (item?.Equals(newValue) ?? false)
            {
                return;
            }
            
            item = newValue;
            PropertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(name));
            _viewHashCode++;
        }
        
        event EventHandler<BindablePropertyChangedEventArgs> INotifyBindablePropertyChanged.propertyChanged
        {
            add => PropertyChanged += value;
            remove => PropertyChanged -= value;
        }

        public long GetViewHashCode() => _viewHashCode;
    }
}