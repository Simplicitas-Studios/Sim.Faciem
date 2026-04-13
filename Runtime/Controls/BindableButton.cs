using Plugins.Sim.Faciem.Shared;
using R3;
using Sim.Faciem.CommandBinding;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    [UxmlElement]
    public partial class BindableButton : Button
    {
        private SerializedCommand _command;
        private DisposableBag _commandSubscriptions;

        [UxmlAttribute, CreateProperty]
        public SerializedCommand Command
        {
            get => _command;
            set
            {
                _command = value;
                RegisterCommandCallbacks();
            }
        }

        public BindableButton()
        {
            var lifeTimeDisposables = this.RegisterDisposableBag();
            _commandSubscriptions = new DisposableBag();
            
            lifeTimeDisposables.Add(_commandSubscriptions);
            
            lifeTimeDisposables.Add(Observable.FromEvent(
                x => clickable.clicked += x,
                x => clickable.clicked -= x)
                .Subscribe(_ =>
                {
                    _command?.Command?.Execute(Unit.Default);
                }));
        }

        private void RegisterCommandCallbacks()
        {
            if (_command?.Command == null)
            {
                return;
            }
            
            _commandSubscriptions.Dispose();
            _commandSubscriptions = new DisposableBag();
            
            _commandSubscriptions.Add(
                _command.Command.CanExecuteObs
                    .Prepend(_command.Command.CanExecute)
                    .Subscribe(x => SetEnabled(x)));
            
            _commandSubscriptions.Add(
                _command.Command.IsVisibleObs
                    .Subscribe(isVisible =>
                    {
                        style.display = isVisible 
                            ? DisplayStyle.Flex 
                            : DisplayStyle.None;
                    }));
        }
    }
}