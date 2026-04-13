using System;
using R3;

namespace Sim.Faciem.Commands
{
    public class Command<T> : ReactiveCommand<T>
    {
        private IDisposable _executionSubscription;
        
        public Observable<bool> IsVisibleObs { get; }

        public Observable<bool> CanExecuteObs { get; }
        
        public Command(Observable<bool> canExecute, bool initialCanExecute)
            : this(canExecute, Observable.Return(true), initialCanExecute)
        {
        }
        
        public Command(Observable<bool> canExecute, Observable<bool> isVisibleObs, bool initialCanExecute)
            : base(canExecute, initialCanExecute)
        {
            IsVisibleObs = isVisibleObs;
            
            CanExecuteObs = Observable.FromEvent<EventHandler, object>(
                    
                    x => (obj, args) => x(obj),
                    x => CanExecuteChanged += x,
                    x => CanExecuteChanged -= x)
                .OfType<object, Command>()
                .Select(command => command.CanExecute())
                .Prepend(CanExecute());
        }

    }
}