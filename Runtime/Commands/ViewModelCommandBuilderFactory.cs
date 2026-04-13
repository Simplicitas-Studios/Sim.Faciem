using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sim.Faciem.Commands
{
    public class ViewModelCommandBuilderFactory : ICommandBuilderFactory
    {
        private readonly IDisposableContainer _container;

        public ViewModelCommandBuilderFactory(IDisposableContainer container)
        {
            _container = container;
        }

        public CommandBuilder Execute(Action action)
        {
            return new CommandBuilder(action, _container);
        }

        public CommandBuilder<T> Execute<T>(Action<T> action)
        {
            return new CommandBuilder<T>(action, _container);
        }

        public AsyncCommandBuilder ExecuteAsync(Func<CancellationToken, UniTask> action)
        {
            return new AsyncCommandBuilder(action, _container);
        }
    }
}