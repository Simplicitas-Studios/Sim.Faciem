using System;

namespace Sim.Faciem
{
    public interface IDIRegistrationBridge
    {
        void RegisterSingleton<TInterface, TImpl>() where TImpl : class, TInterface;
        
        void RegisterTransient(Type tInterface, Type tImpl);
    }
}