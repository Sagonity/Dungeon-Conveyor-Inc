namespace Deepshaft.Roster
{
    /// <summary>
    /// Tells the roster whether the Adventure team is out on an automated Expedition
    /// (team decision 2026-11-12: the team is busy until the player stops the Expedition
    /// and collects). <see cref="Expeditions.ExpeditionService"/> implements this (SCRUM-7);
    /// <see cref="NoExpeditions"/> is a team that's never busy, for tests and tools.
    /// </summary>
    public interface ITeamBusySource
    {
        bool IsAdventureTeamBusy { get; }
    }

    public sealed class NoExpeditions : ITeamBusySource
    {
        public static readonly NoExpeditions Instance = new NoExpeditions();

        NoExpeditions() { }

        public bool IsAdventureTeamBusy => false;
    }
}
