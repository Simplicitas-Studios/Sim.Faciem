using System;

namespace Sim.Faciem
{
    public interface IViewModelConstructionService
    {
        T CreateInstance<T>() where T : BaseViewModel;
        
        BaseViewModel CreateInstance(Type type);
    }
}