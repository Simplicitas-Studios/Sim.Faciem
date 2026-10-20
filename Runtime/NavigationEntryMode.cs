namespace Sim.Faciem
{
    /// <summary>Explicit entry behavior; parameters and application identity never select re-entry implicitly.</summary>
    public enum NavigationEntryMode
    {
        Default = 0,
        ReenterIfActive = 1
    }
}
