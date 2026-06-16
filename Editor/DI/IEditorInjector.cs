using System;

namespace Sim.Faciem.Editor.DI
{
    public interface IEditorInjector : IDIContainerBridge
    {
        void Register<TInterface, TImplementation>(bool nonLazy = false, params Type[] aliases)
            where TImplementation : TInterface;

        void RegisterInstance<TInterface, TImplementation>(TImplementation instance) where TImplementation : TInterface;
    }
}
