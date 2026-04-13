using System;
using System.Linq.Expressions;
using R3;

namespace Sim.Faciem.PropertyObserver
{
    public interface IViewModelPropertyObserver<T> where T : ViewModel<T>
    {
        Observable<TProperty> Observe<TProperty>(Expression<Func<T, TProperty>> propertyExpression);
    }
}