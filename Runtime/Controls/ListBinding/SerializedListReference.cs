using System;
using System.Collections;

namespace Sim.Faciem.ListBinding
{
    [Serializable]
    public class SerializedListReference
    {
        public IList ItemSource;

        public SerializedListReference()
        {
            
        }

        public SerializedListReference(IList itemSource)
        {
            ItemSource = itemSource;
        }
    }
}