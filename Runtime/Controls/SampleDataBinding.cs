using UnityEngine.UIElements;

namespace Sim.Faciem
{
    public class SampleDataBinding : DataBinding
    {
        protected override BindingResult UpdateSource<TValue>(in BindingContext context, ref TValue value)
        {
            return base.UpdateSource(in context, ref value);
        }

        protected override BindingResult UpdateUI<TValue>(in BindingContext context, ref TValue value)
        {
            return base.UpdateUI(in context, ref value);
        }
    }
}