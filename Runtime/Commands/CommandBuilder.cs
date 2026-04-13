using System;
using R3;

namespace Sim.Faciem.Commands
{
    public struct CommandBuilder
    {
        private readonly IDisposableContainer _disposableContainer;

        private readonly Action _executionAction;
        
        private Observable<bool> _canExecuteObservable;
        
        private Observable<bool> _isVisibleObservable;
        
        public CommandBuilder(Action executionAction, IDisposableContainer disposableContainer)
        {
            _executionAction = executionAction;
            _disposableContainer = disposableContainer;
            _canExecuteObservable = Observable.Return(true);
            _isVisibleObservable = Observable.Return(true);
        }

        public CommandBuilder WithCanExecute(Observable<bool> canExecuteObservable)
        {
            _canExecuteObservable = canExecuteObservable;
            return this;
        }
        
        public CommandBuilder WithIsVisible(Observable<bool> isVisible)
        {
            _isVisibleObservable = isVisible;
            return this;
        }

        public Command Build()
        {
            var command = new Command(_canExecuteObservable, _isVisibleObservable, true);
            
            var action = _executionAction;
            _disposableContainer.Add(
                command
                    .Subscribe(_ => action.Invoke()));
            
            
            _disposableContainer.Add(command);
            return command;
        }
        
        public static implicit operator Command(CommandBuilder builder) => builder.Build();
    }
}