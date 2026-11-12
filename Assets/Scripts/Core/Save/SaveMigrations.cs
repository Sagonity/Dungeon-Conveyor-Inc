using System;
using Deepshaft.Economy;
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
            if (state.version == 1)
            {
                // v3.4 design: Upgrade Materials and Copper were removed (no ore conversion),
                // and star rank was replaced by breakthroughs. JsonUtility already ignores the
                // old "stars" field, so only the retired wallet entries need dropping.
                state.wallet?.balances?.RemoveAll(b => !Enum.IsDefined(typeof(ResourceId), b.id));
                state.version = 2;
            }

            state.version = GameState.CurrentVersion;
        }
    }
}
