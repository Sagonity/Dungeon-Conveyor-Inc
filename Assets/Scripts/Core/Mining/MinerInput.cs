using Deepshaft.Minions;

namespace Deepshaft.Mining
{
    /// <summary>What one working Miner brings to the Mine. Input to <see cref="MiningMath"/>.</summary>
    public readonly struct MinerInput
    {
        public MinerInput(float miningPower, MinerTrait trait, float toolBonus = 0f)
        {
            MiningPower = miningPower;
            Trait = trait;
            ToolBonus = toolBonus;
        }

        /// <summary>From the Miner's definition and level.</summary>
        public float MiningPower { get; }

        /// <summary>Fixed per named Miner: Gold, Ore or Speed.</summary>
        public MinerTrait Trait { get; }

        /// <summary>Extra output from the Miner's tool, e.g. 0.2 = +20%.</summary>
        public float ToolBonus { get; }
    }

    /// <summary>
    /// Extra mining output from equipment. The Smith (SCRUM-9) implements this;
    /// until then Miners have no tools (<see cref="None"/>).
    /// </summary>
    public interface IMinerToolBonus
    {
        /// <summary>Extra output for this Miner's tool, e.g. 0.2 = +20%. 0 for no tool.</summary>
        float GetToolBonus(OwnedMinion miner);
    }

    public sealed class NoMinerTools : IMinerToolBonus
    {
        public static readonly NoMinerTools Instance = new NoMinerTools();

        NoMinerTools() { }

        public float GetToolBonus(OwnedMinion miner) => 0f;
    }
}
