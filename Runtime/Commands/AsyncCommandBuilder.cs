using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Sim.Faciem.Commands
{
    public struct AsyncCommandBuilder
    {
        private readonly IDisposableContainer _disposableContainer;
        private readonly Func<CancellationToken, UniTask> _executionFunc;
        
        private Observable<bool> _canExecuteObservable;
        
        private Observable<bool> _isVisibleObservable;
        
        public AsyncCommandBuilder(Func<CancellationToken, UniTask> executionFunc, IDisposableContainer disposableContainer)
        {
            _executionFunc = executionFunc;
            _canExecuteObservable = Observable.Return(true);
            _isVisibleObservable = Observable.Return(true);
            _disposableContainer = disposableContainer;
        }

        public AsyncCommandBuilder WithCanExecute(Observable<bool> canExecuteObservable)
        {
            _canExecuteObservable = canExecuteObservable;
            return this;
        }
        
        public AsyncCommandBuilder WithIsVisible(Observable<bool> isVisible)
        {
            _isVisibleObservable = isVisible;
            return this;
        }

        public Command Build()
        {
            var command = new Command(_canExecuteObservable, _isVisibleObservable, true);

            var asyncAction = _executionFunc;
            _disposableContainer.Add(
                command
                    .SubscribeAwait( async (_, ct) =>
                    {
                        await asyncAction.Invoke(ct);
                    }));
            
            _disposableContainer.Add(command);
            return command;
        }
        
        public static implicit operator Command(AsyncCommandBuilder builder) => builder.Build();
    }
}