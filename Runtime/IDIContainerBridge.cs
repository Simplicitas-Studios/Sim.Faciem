using System;

namespace Sim.Faciem
{
    public interface IDIContainerBridge
    {
        T ResolveInstance<T>() where T : class;
        
        object ResolveInstance(Type type);
    }
}