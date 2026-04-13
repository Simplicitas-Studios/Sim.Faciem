using System;

namespace Sim.Faciem
{
    public interface IDisposableContainer
    {
        public void Add(IDisposable disposable);
    }
}