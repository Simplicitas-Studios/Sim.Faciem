using System;

namespace Sim.Faciem
{
    public interface IDIRegistrationBridge
    {
        void RegisterSingleton<TInterface, TImpl>(params Type[] aliases) where TImpl : class, TInterface;

        void RegisterTransient<TInterface, TImpl>(params Type[] aliases) where TImpl : class, TInterface;

        void RegisterTransient(Type tInterface, Type tImpl);
    }
}
