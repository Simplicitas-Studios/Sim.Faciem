using System;
using Sim.Faciem;
using Sim.Dispositio.Shared;
using UnityEngine;

public sealed class FaciemPlayerSmoke : MonoBehaviour
{
    [SerializeField]
    private SerializableGuid _identifier = new SerializableGuid("12345678-1234-5678-9abc-1234567890ab");

    private void Awake()
    {
        var reference = new MonoScriptReference { TypeName = typeof(INavigationService).AssemblyQualifiedName };
        if (reference.GetReferencedType() != typeof(INavigationService))
        {
            throw new InvalidOperationException("Faciem serialized type resolution failed.");
        }

        Debug.Log("Faciem/Utility smoke scene identifier: " + _identifier);
    }
}
