using System;
using R3;

namespace Sim.Faciem.Commands
{
    public struct CommandBuilder<T>
    {
        private readonly IDisposableContainer _disposableContainer;

        private readonly Action<T> _executionAction;
        
        private Observable<bool> _canExecuteObservable;
        
        private Observable<bool> _isVisibleObservable;
        
        public CommandBuilder(Action<T> executionAction, IDisposableContainer disposableContainer)
        {
            _executionAction = executionAction;
            _disposableContainer = disposableContainer;
            _canExecuteObservable = Observable.Return(true);
            _isVisibleObservable = Observable.Return(true);
        }

        public CommandBuilder<T> WithCanExecute(Observable<bool> canExecuteObservable)
        {
            _canExecuteObservable = canExecuteObservable;
            return this;
        }
        
        public CommandBuilder<T> WithIsVisible(Observable<bool> isVisible)
        {
            _isVisibleObservable = isVisible;
            return this;
        }

        public Command<T> Build()
        {
            var command = new Command<T>(_canExecuteObservable, _isVisibleObservable, true);
            
            var action = _executionAction;
            _disposableContainer.Add(
                command
                    .Subscribe(next => action.Invoke(next)));
            
            
            _disposableContainer.Add(command);
            return command;
        }
        
        public static implicit operator Command<T>(CommandBuilder<T> builder) => builder.Build();
    }
}