namespace Deepshaft.Roster
{
    /// <summary>
    /// Tells the roster whether the Adventure team is out on an automated Expedition
    /// (team decision 2026-11-12: the team is busy until the player stops the Expedition
    /// and collects). The Expedition system (SCRUM-7) implements this; until then the team
    /// is never busy (<see cref="NoExpeditions"/>).
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
