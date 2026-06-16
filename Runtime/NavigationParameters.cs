using Sim.Utility.PropertyModel;

namespace Sim.Faciem
{
    public class NavigationParameters : PropertySet
    {
        public new NavigationParameters Add<T>(IPropertyKey<T> key, T value) => base.Add(key, value) as NavigationParameters;

        public new static NavigationParameters Empty => new();
    }
}
