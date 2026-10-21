using R3;

namespace Sim.Faciem.Samples
{
    /// <summary>Example presentation state only; a consumer supplies its selected item.</summary>
    public sealed class SelectedItemViewModel : ViewModel<SelectedItemViewModel>
    {
        public ReactiveProperty<int> SelectedIndex { get; } = new(0);

        public SelectedItemViewModel()
        {
            Disposables.Add(SelectedIndex);
        }
    }
}
