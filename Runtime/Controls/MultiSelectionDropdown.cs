using System.Collections.Generic;
using System.Linq;
using Plugins.Sim.Faciem.Shared;
using R3;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sim.Faciem.Controls
{
    [UxmlElement]
    public partial class MultiSelectionDropdown : VisualElement
    {
        public const string OverlayContainerClassName = "multi-selection-dropdown__overlay-container";
        public const string ItemClassName = "multi-selection-dropdown__item";

        private readonly DisposableBagHolder _disposables;
        private readonly Label _selectionLabel;
        private VisualElement _overlayContainer;
        private ScrollView _itemScrollView;
        private List<MultiSelectionItem> _items;
        private string _watermarkText;

        [UxmlAttribute]
        public string WatermarkText
        {
            get => _watermarkText;
            set
            {
                _watermarkText = value;
                _selectionLabel.text = string.IsNullOrEmpty(_watermarkText)
                    ? "Select options"
                    : _watermarkText;
            }
        }

        [UxmlAttribute]
        public string OverlayContainerId { get; set; }
        
        [UxmlAttribute, CreateProperty]
        public List<MultiSelectionItem> Items
        {
            get => _items;
            set
            {
                _items = value;
                UpdateMenuItems();
            }
        }

        [CreateProperty]
        public List<MultiSelectionItem> SelectedItems => _items?.Where(item => item.Selected)
            .ToList() ?? new List<MultiSelectionItem>();

        public MultiSelectionDropdown()
        {
            _disposables = this.RegisterDisposableBag();
            _disposables.Add(this.AttachToPanelAsObservable()
                .Subscribe(SetupOverlayContainer));
            
            _disposables.Add(this.PointerDownAsObservable()
                .Subscribe(ToggleMenu));
            
            _disposables.Add(this.BlurAsObservable()
                .Subscribe(_ => CloseMenu()));
            
            _disposables.Add(this.GeometryChangedAsObservable()
                .Subscribe(_ => SetOverlayPosition()));

            Items = new List<MultiSelectionItem>
            {
                new()
                {
                    DisplayName = "Option 1",
                    Index = 0,
                    Selected = false
                },
                new()
                {
                    DisplayName = "Option 2",
                    Index = 1,
                    Selected = true
                },
                new()
                {
                    DisplayName = "Option 3",
                    Index = 2,
                    Selected = false
                }
            };

            _selectionLabel = new Label(_watermarkText);
            Add(_selectionLabel);
        }

        private void SetupOverlayContainer(AttachToPanelEvent _)
        {
            if (string.IsNullOrEmpty(OverlayContainerId))
            {
                return;
            }

            var overlayContainer = panel.visualTree.Q<VisualElement>(OverlayContainerId);
            _overlayContainer = new VisualElement
            {
                name = name + "-dropdown-overlay",
            };
            _overlayContainer.AddToClassList(OverlayContainerClassName);
            _overlayContainer.style.display = DisplayStyle.Flex;

            _itemScrollView = new ScrollView();
            _overlayContainer.Add(_itemScrollView);
            overlayContainer.Add(_overlayContainer);
            UpdateMenuItems();
            
        }

        private void UpdateMenuItems()
        {
            if (_overlayContainer == null || Items == null)
            {
                return;
            }
            
            _itemScrollView.Clear();
            foreach (var item in Items)
            {
                var itemContainer = new VisualElement();
                itemContainer.AddToClassList(ItemClassName);
                var checkbox = new Toggle
                {
                    text = item.DisplayName,
                    value = item.Selected
                };
                checkbox.SetBinding(nameof(checkbox.value), new DataBinding
                {
                    dataSourcePath = PropertyPath.FromName(nameof(MultiSelectionItem.Selected)),
                    dataSource = item
                });
                checkbox.SetBinding(nameof(checkbox.label), new DataBinding
                {
                    dataSourcePath = PropertyPath.FromName(nameof(MultiSelectionItem.DisplayName)),
                    dataSource = item
                });
                itemContainer.Add(checkbox);
                _itemScrollView.Add(itemContainer);
            }
        }

        private void ToggleMenu(PointerDownEvent _)
        {
            _overlayContainer.style.display = _overlayContainer.style.display == DisplayStyle.Flex 
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void CloseMenu()
        {
            // _overlayContainer.style.display = DisplayStyle.None;
        }

        private void SetOverlayPosition()
        {
            if (_overlayContainer == null)
            {
                return;
            }
            
            var localPos = this.ChangeCoordinatesTo(_overlayContainer.parent, Vector2.zero);
            _overlayContainer.style.position = Position.Absolute;
            _overlayContainer.style.left = localPos.x + layout.width - resolvedStyle.width; // top-right
            _overlayContainer.style.top = localPos.y + layout.height; // just below
        }
    }
}