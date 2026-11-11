using Deepshaft.State;

namespace Deepshaft.Save
{
    /// <summary>
    /// Upgrades saves from older versions to <see cref="GameState.CurrentVersion"/>.
    /// When you bump CurrentVersion, add a step here that turns version N-1 into N,
    /// and a test that loads an old save.
    /// </summary>
    public static class SaveMigrations
    {
        public static void Migrate(GameState state)
        {
            // Example for the first change:
            // if (state.version == 1) { /* move or rename data */ state.version = 2; }

            state.version = GameState.CurrentVersion;
        }
    }
}
