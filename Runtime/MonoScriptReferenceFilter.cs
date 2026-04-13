using System;

namespace Sim.Faciem
{
    public class MonoScriptReferenceFilter : Attribute
    {
        public Type TargetType;

        public MonoScriptReferenceFilter()
        {
        }

        public MonoScriptReferenceFilter(Type targetType)
        {
            TargetType = targetType;
        }
    }
}