using UnityEngine;

namespace Deepshaft.Minions
{
    /// <summary>
    /// What a kind of Minion is: name, job, class or miner trait, rarity and how its stats grow.
    /// One asset per Minion type (create via Assets → Create → Deepshaft → Minion Definition).
    /// Owned copies are <see cref="OwnedMinion"/> entries in the save, which point here by <see cref="Id"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Deepshaft/Minion Definition", fileName = "Minion_New")]
    public class MinionDefinition : ScriptableObject
    {
        [Tooltip("Stable id stored in save files, e.g. \"spore_bruiser\". Never change it after release.")]
        [SerializeField] string id = "";
        [SerializeField] string displayName = "New Minion";
        [SerializeField] MinionKind kind = MinionKind.Adventure;
        [Tooltip("Adventure Minions only. Miners use None.")]
        [SerializeField] MinionRole role = MinionRole.Fighter;
        [Tooltip("Miner Minions only: fixed specialization (Gold, ore or speed). Adventure Minions use None.")]
        [SerializeField] MinerTrait minerTrait = MinerTrait.None;
        [SerializeField] Rarity rarity = Rarity.Common;

        [Header("Level cap")]
        [SerializeField, Min(1)] int maxLevelBeforeBreakthrough = 20;
        [Tooltip("Each rare-material breakthrough raises the level cap by this much (v3.4 §5).")]
        [SerializeField, Min(0)] int maxLevelPerBreakthrough = 10;

        [Header("Miner stats (level 1, then + per level)")]
        [SerializeField, Min(0)] float baseMiningPower = 10f;
        [SerializeField, Min(0)] float miningPowerPerLevel = 2f;

        [Header("Adventure stats (level 1, then + per level)")]
        [SerializeField, Min(0)] float baseHp = 100f;
        [SerializeField, Min(0)] float hpPerLevel = 10f;
        [SerializeField, Min(0)] float baseAtk = 10f;
        [SerializeField, Min(0)] float atkPerLevel = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public MinionKind Kind => kind;
        public MinionRole Role => kind == MinionKind.Miner ? MinionRole.None : role;
        public MinerTrait MinerTrait => kind == MinionKind.Miner ? minerTrait : MinerTrait.None;
        public Rarity Rarity => rarity;

        public int GetMaxLevel(int breakthroughs) =>
            maxLevelBeforeBreakthrough + Mathf.Max(0, breakthroughs) * maxLevelPerBreakthrough;

        public float GetMiningPower(int level) =>
            kind == MinionKind.Miner ? Grow(baseMiningPower, miningPowerPerLevel, level) : 0f;

        public float GetHp(int level) =>
            kind == MinionKind.Adventure ? Grow(baseHp, hpPerLevel, level) : 0f;

        public float GetAtk(int level) =>
            kind == MinionKind.Adventure ? Grow(baseAtk, atkPerLevel, level) : 0f;

        static float Grow(float atLevelOne, float perLevel, int level) =>
            atLevelOne + perLevel * (Mathf.Max(1, level) - 1);

#if UNITY_EDITOR
        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("MinionDefinition '" + name + "' has no id. Save files can't refer to it.", this);
            }
        }

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetup(string newId, MinionKind newKind, MinionRole newRole, Rarity newRarity,
            MinerTrait newMinerTrait = MinerTrait.None)
        {
            id = newId;
            kind = newKind;
            role = newRole;
            rarity = newRarity;
            minerTrait = newMinerTrait;
        }
#endif
    }
}
