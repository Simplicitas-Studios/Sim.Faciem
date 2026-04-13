using System;
using UnityEngine;

namespace Sim.Faciem
{
    [Serializable]
    public class ViewId : IEquatable<ViewId>
    {
        [SerializeField]
        private string _id;

        public string Id => _id;

        public bool Equals(ViewId other)
        {
            return other != null && _id == other._id;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ViewId);
        }

        public override int GetHashCode()
        {
            return _id != null ? _id.GetHashCode() : 0;
        }

        public static ViewId From(string id)
        {
            var instance = new ViewId
            {
                _id = id
            };
            return instance;
        }
    }
}