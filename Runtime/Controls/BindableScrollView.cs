using Plugins.Sim.Faciem.Shared;
using R3;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sim.Faciem.Controls
{
    [UxmlElement]
    public partial class BindableScrollView : ScrollView
    {
        private static readonly BindingId s_verticalScrollPositionId = new(nameof(VerticalScrollPosition));
        private static readonly BindingId s_horizontalScrollPositionId = new(nameof(HorizontalScrollPosition));
        
        [UxmlAttribute, CreateProperty]
        public float VerticalScrollPosition
        {
            get => verticalScroller.value;
            set
            {
                if (Mathf.Approximately(verticalScroller.value, value))
                {
                    return;
                }
                
                verticalScroller.value = value;
            }
        }
        
        [UxmlAttribute, CreateProperty]
        public float HorizontalScrollPosition
        {
            get => horizontalScroller.value;
            set
            {
                if (Mathf.Approximately(horizontalScroller.value, value))
                {
                    return;
                }
                
                horizontalScroller.value = value;
            }
        }

        public BindableScrollView()
        {
            var disposables = this.RegisterDisposableBag();
            
            disposables.Add(horizontalScroller.slider
                .ObserveChanges()
                .Subscribe(newValue =>
                {
                    HorizontalScrollPosition = newValue;
                    NotifyPropertyChanged(s_horizontalScrollPositionId);
                }));
            
            disposables.Add(verticalScroller.slider
                .ObserveChanges()
                .Subscribe(newValue =>
                {
                    VerticalScrollPosition = newValue;
                    NotifyPropertyChanged(s_verticalScrollPositionId);
                }));
        }
    }
    
}