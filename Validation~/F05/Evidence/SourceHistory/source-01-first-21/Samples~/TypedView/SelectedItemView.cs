using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Sim.Faciem.Samples
{
    /// <summary>Imperative USS behavior on the existing UXML root; not a VisualElement subclass.</summary>
    public sealed class SelectedItemView : View<SelectedItemViewModel>
    {
        private readonly VisualElement[] _items;

        public SelectedItemView(SelectedItemViewModel viewModel, VisualElement root) : base(viewModel, root)
        {
            _items = Enumerable.Range(0, 4).Select(index => root.Q<Label>("item-" + index)).Cast<VisualElement>().ToArray();
            if (_items.Any(item => item == null))
            {
                throw new InvalidOperationException("SelectedItem UXML requires item-0 through item-3.");
            }
        }

        protected override void OnBind()
        {
            Bind(ViewModel.SelectedIndex, selected =>
            {
                for (int index = 0; index < _items.Length; index++)
                {
                    _items[index].EnableInClassList("item--selected", index == selected);
                }
            });
        }
    }
}
