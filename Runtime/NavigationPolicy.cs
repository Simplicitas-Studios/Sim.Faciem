namespace Sim.Faciem
{
    /// <summary>Determines whether overlapping requests remain FIFO or supersede older opted-in requests.</summary>
    public enum NavigationPolicy
    {
        Fifo = 0,
        LatestRequestWins = 1
    }
}
