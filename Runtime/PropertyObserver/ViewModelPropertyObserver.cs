using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using R3;

namespace Sim.Faciem.PropertyObserver
{
    internal class ViewModelPropertyObserver<T> : IViewModelPropertyObserver<T> where T : ViewModel<T>
    {
        private readonly Dictionary<string, Observable<Unit>> _propertyChangeTriggers;
        
        private readonly T _owner;

        public ViewModelPropertyObserver(T owner)
        {
            _propertyChangeTriggers = new Dictionary<string, Observable<Unit>>();
            _owner = owner;
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
                changeObs = _owner.PropertyChangedObs
                    .Where(change => change.propertyName.Equals(propInfo.Name))
                    .AsUnitObservable()
                    .Share();
                _propertyChangeTriggers.Add(propInfo.Name, changeObs);
            }
            
            return changeObs
                .Select(_ => compiledExpression(_owner));
        }
    }
}