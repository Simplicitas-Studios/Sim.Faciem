using System;

namespace Plugins.Sim.Faciem.Editor.DI
{
    public struct ServiceRegistration : IEquatable<ServiceRegistration>
    {
        public Type InstanceType { get; }
        
        public bool IsSingleton { get; }
        
        public bool NonLazy { get; }

        private ServiceRegistration(Type instanceType, bool isSingleton, bool nonLazy)
        {
            InstanceType = instanceType;
            IsSingleton = isSingleton;
            NonLazy = nonLazy;
        }

        public static ServiceRegistration Singleton(Type instanceType, bool nonLazy = false)
        {
            return new ServiceRegistration(instanceType, true, nonLazy);
        }
        
        public static ServiceRegistration Transient(Type instanceType, bool nonLazy = false)
        {
            return new ServiceRegistration(instanceType, false, nonLazy);
        }

        public bool Equals(ServiceRegistration other)
        {
            return InstanceType == other.InstanceType && IsSingleton == other.IsSingleton && NonLazy == other.NonLazy;
        }

        public override bool Equals(object obj)
        {
            return obj is ServiceRegistration other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(InstanceType, IsSingleton, NonLazy);
        }
    }
}