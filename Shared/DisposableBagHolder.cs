using System;
using R3;

namespace Plugins.Sim.Faciem.Shared
{
    public class DisposableBagHolder : IDisposable
    {
        private DisposableBag _disposableBag;

        public DisposableBagHolder()
        {
            _disposableBag = new DisposableBag();
        }
        
        public void Add(IDisposable disposable)
        {
            _disposableBag.Add(disposable);
        }

        public void Dispose()
        {
            _disposableBag.Dispose();
        }
    }
}