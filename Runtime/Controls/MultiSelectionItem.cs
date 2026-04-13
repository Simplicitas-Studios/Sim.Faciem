using System;

namespace Sim.Faciem.Controls
{
    [Serializable]
    public class MultiSelectionItem : Bindable<MultiSelectionItem>
    {
        public string DisplayName { get; set; }
        
        public int Index { get; set; }
        public bool Selected { get; set; }
    }
}