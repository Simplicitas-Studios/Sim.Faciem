using System;

namespace Sim.Faciem
{
    public struct RegionName : IEquatable<RegionName>
    {
        public string Name { get; }

        private RegionName(string name)
        {
            Name = name;
        }
        
        public static RegionName From(string name)
        {
            return new RegionName(name);
        }

        public bool Equals(RegionName other)
        {
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            return obj is RegionName other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }
    }
}