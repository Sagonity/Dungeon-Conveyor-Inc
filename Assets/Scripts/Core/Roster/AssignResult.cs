namespace Deepshaft.Roster
{
    /// <summary>Why an assignment did or didn't happen, so the UI can explain it.</summary>
    public enum AssignResult
    {
        Ok,
        /// <summary>No owned Minion with that instance id, or its definition was removed.</summary>
        NotOwned,
        /// <summary>Miner Minions only mine; Adventure Minions only fight.</summary>
        WrongKind,
        AlreadyAssigned,
        NotAssigned,
        NoFreeSlot,
        /// <summary>The Adventure team is on an automated Expedition. Stop it and collect first.</summary>
        TeamBusy,
    }
}
