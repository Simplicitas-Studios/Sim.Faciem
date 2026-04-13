using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sim.Faciem
{
    [Serializable]
    public class MonoScriptReference
    {
        public string TypeName;

        public Type GetReferencedType() => Type.GetType(TypeName);
        
#if UNITY_EDITOR
        public MonoScript Script;
#endif
        
    }
}