using System.Reflection;
using Sim.Faciem.ListBinding;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sim.Faciem
{
    [UxmlElement]
    public partial class BindableListView : ListView
    {
        private readonly ScrollView _scrollView;
        private SerializedListReference _itemSource;

        [UxmlAttribute]
        public bool AlwaysShowHorizontalScroller
        {
            get => _scrollView.verticalScrollerVisibility == ScrollerVisibility.AlwaysVisible;
            set => _scrollView.verticalScrollerVisibility = value
                ? ScrollerVisibility.AlwaysVisible
                : ScrollerVisibility.Auto;
        }
        
        [UxmlAttribute, CreateProperty]
        public SerializedListReference ItemSource
        {
            get => _itemSource;
            set
            {
                _itemSource = value;
                SetDataBinding();
            }
        }
        
        [UxmlAttribute, CreateProperty]
        public int SelectedIndex
        {
            get;
            set;
        }

        public BindableListView()
        {
            _scrollView = this.Q<ScrollView>();
        }
        
        protected override void HandleEventTrickleDown(EventBase evt)
        {
            if (evt is AttachToPanelEvent attachToPanelEvent)
            {
#if UNITY_EDITOR

                if (IsUIBuilderInstance(attachToPanelEvent.destinationPanel))
                {
                    return;
                }

#endif
                SetDataBinding();
            }
        }

        private void SetDataBinding()
        {
#if UNITY_EDITOR

            if (!Application.isPlaying
                && (panel == null 
                || IsUIBuilderInstance(panel)))
            {
                return;
            }

#endif
            
            if (TryGetBinding(nameof(ItemSource), out var itemSourceBinding)
                && itemSourceBinding is DataBinding dataBinding)
            {
                SetBinding(nameof(itemsSource), new DataBinding
                {
                    dataSourcePath = dataBinding.dataSourcePath,
                    bindingMode = dataBinding.bindingMode,
                    updateTrigger = dataBinding.updateTrigger
                });
                
                ClearBinding(nameof(ItemSource));
            }

            if (TryGetBinding(nameof(SelectedIndex), out var selectedIndexBinding)
                && selectedIndexBinding is DataBinding selectedIndexDataBinding)
            {
                SetBinding(nameof(selectedIndex), new DataBinding
                {
                    dataSourcePath = selectedIndexDataBinding.dataSourcePath,
                    bindingMode = selectedIndexDataBinding.bindingMode,
                    updateTrigger = selectedIndexDataBinding.updateTrigger
                });   
            }
        }

        private static bool IsUIBuilderInstance(IPanel destitionPanel)
        {
            if (destitionPanel?.contextType == ContextType.Editor)
            {
                var nameProperty = destitionPanel.GetType()
                    .GetProperty("name", BindingFlags.Instance | BindingFlags.NonPublic);

                if (nameProperty != null)
                {
                    var panelName = nameProperty.GetValue(destitionPanel) as string;
                    if (panelName?.Equals("Builder") ?? false)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}