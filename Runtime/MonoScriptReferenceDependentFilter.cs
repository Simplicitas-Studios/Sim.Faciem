using System;

namespace Sim.Faciem
{
    public class MonoScriptReferenceDependentFilter : Attribute
    {
        public string FieldName { get; }

        public MonoScriptReferenceDependentFilter()
        {
        }

        public MonoScriptReferenceDependentFilter(string fieldName)
        {
            FieldName = fieldName;
        }
    }
}