using System.Collections.Generic;
using Deepshaft.Bosses;
using Deepshaft.Combat;
using Deepshaft.Equipment;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Progression;
using Deepshaft.Roster;
using Deepshaft.Summoning;
using UnityEngine;

namespace Deepshaft.Content
{
    /// <summary>
    /// All game content in one place, so systems can look things up by id.
    /// Create one asset (Assets → Create → Deepshaft → Game Database) and list every definition in it.
    /// </summary>
    [CreateAssetMenu(menuName = "Deepshaft/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        [SerializeField] List<MinionDefinition> minions = new List<MinionDefinition>();
        [SerializeField] MiningRules mining = MiningRules.CreateDefault();
        [SerializeField] RosterRules roster = RosterRules.CreateDefault();

        [Header("Bosses, Expeditions and tiers (SCRUM-7)")]
        [SerializeField] CombatRules combat = CombatRules.CreateDefault();
        [SerializeField] List<BossDefinition> bosses = DefaultAdventureContent.Bosses();
        [SerializeField] List<ExpeditionDefinition> expeditions = DefaultAdventureContent.Expeditions();
        [SerializeField] List<TierRequirement> tierRequirements = DefaultAdventureContent.TierRequirements();

        [Header("Minion levels, breakthroughs and gear (SCRUM-10)")]
        [SerializeField] MinionProgressionRules minionProgression = MinionProgressionRules.CreateDefault();
        [SerializeField] EquipmentRules equipment = EquipmentRules.CreateDefault();

        [Header("Summoning (SCRUM-11)")]
        [SerializeField] SummonRules summoning = SummonRules.CreateDefault();

        [Header("New game (SCRUM-30)")]
        [Tooltip("Minion definition ids a new game starts with. Miners fill the Mine and Adventure Minions the team, in this order.")]
        [SerializeField] List<string> starterMinions = new List<string>();

        Dictionary<string, MinionDefinition> minionsById;

        public IReadOnlyList<MinionDefinition> Minions => minions;

        /// <summary>Mining rates per tier and trait bonuses (SCRUM-5). Never null.</summary>
        public MiningRules Mining => mining ?? (mining = MiningRules.CreateDefault());

        /// <summary>Miner and Adventure team slot counts (SCRUM-6). Never null.</summary>
        public RosterRules Roster => roster ?? (roster = RosterRules.CreateDefault());

        /// <summary>Autobattle numbers and Adventure class behaviour (SCRUM-7). Never null.</summary>
        public CombatRules Combat => combat ?? (combat = CombatRules.CreateDefault());

        /// <summary>Mine Tier Bosses, one per tier (SCRUM-7).</summary>
        public IReadOnlyList<BossDefinition> Bosses => bosses ?? (bosses = new List<BossDefinition>());

        /// <summary>Expeditions, each unlocked by a boss (SCRUM-7).</summary>
        public IReadOnlyList<ExpeditionDefinition> Expeditions =>
            expeditions ?? (expeditions = new List<ExpeditionDefinition>());

        /// <summary>What unlocks each mine tier after the first (SCRUM-7; final rules from SCRUM-2).</summary>
        public IReadOnlyList<TierRequirement> TierRequirements =>
            tierRequirements ?? (tierRequirements = new List<TierRequirement>());

        /// <summary>Level-up and breakthrough costs (SCRUM-10). Never null.</summary>
        public MinionProgressionRules MinionProgression =>
            minionProgression ?? (minionProgression = MinionProgressionRules.CreateDefault());

        /// <summary>Gear slots and their bonuses (SCRUM-10). Never null.</summary>
        public EquipmentRules Equipment => equipment ?? (equipment = EquipmentRules.CreateDefault());

        /// <summary>Shard costs, rarity rates and Essence per duplicate (SCRUM-11). Never null.</summary>
        public SummonRules Summoning => summoning ?? (summoning = SummonRules.CreateDefault());

        /// <summary>Minion definition ids a new game starts with (SCRUM-30).</summary>
        public IReadOnlyList<string> StarterMinions => starterMinions ?? (starterMinions = new List<string>());

        /// <summary>Returns null if no boss has this id.</summary>
        public BossDefinition FindBoss(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (BossDefinition b in Bosses) if (b != null && b.id == id) return b;
            return null;
        }

        /// <summary>The boss guarding <paramref name="tier"/>, or null.</summary>
        public BossDefinition FindBossForTier(int tier)
        {
            foreach (BossDefinition b in Bosses) if (b != null && b.tier == tier) return b;
            return null;
        }

        /// <summary>Returns null if no Expedition has this id.</summary>
        public ExpeditionDefinition FindExpedition(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (ExpeditionDefinition e in Expeditions) if (e != null && e.id == id) return e;
            return null;
        }

        /// <summary>The requirement row for <paramref name="tier"/>, or null if that tier can't be unlocked.</summary>
        public TierRequirement FindTierRequirement(int tier)
        {
            foreach (TierRequirement r in TierRequirements) if (r != null && r.tier == tier) return r;
            return null;
        }

        /// <summary>Returns null if no definition has this id (e.g. content removed after a save was made).</summary>
        public MinionDefinition FindMinion(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (minionsById == null) BuildIndex();
            minionsById.TryGetValue(id, out MinionDefinition definition);
            return definition;
        }

        void OnEnable() => minionsById = null;

        void BuildIndex()
        {
            minionsById = new Dictionary<string, MinionDefinition>();
            foreach (MinionDefinition definition in minions)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Id)) continue;
                if (minionsById.ContainsKey(definition.Id))
                {
                    Debug.LogError("GameDatabase: duplicate Minion id '" + definition.Id + "'.", this);
                    continue;
                }
                minionsById.Add(definition.Id, definition);
            }
        }

#if UNITY_EDITOR
        void OnValidate() => minionsById = null;

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetMinions(IEnumerable<MinionDefinition> definitions)
        {
            minions = new List<MinionDefinition>(definitions);
            minionsById = null;
        }

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetMining(MiningRules rules) => mining = rules;

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetRoster(RosterRules rules) => roster = rules;

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetCombat(CombatRules rules) => combat = rules;

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetBosses(IEnumerable<BossDefinition> definitions) =>
            bosses = new List<BossDefinition>(definitions);

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetExpeditions(IEnumerable<ExpeditionDefinition> definitions) =>
            expeditions = new List<ExpeditionDefinition>(definitions);

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetMinionProgression(MinionProgressionRules rules) => minionProgression = rules;

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetSummoning(SummonRules rules) => summoning = rules;

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetEquipment(EquipmentRules rules) => equipment = rules;

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetStarterMinions(IEnumerable<string> ids) => starterMinions = new List<string>(ids);

        /// <summary>For tests and editor tools only.</summary>
        public void EditorSetTierRequirements(IEnumerable<TierRequirement> requirements) =>
            tierRequirements = new List<TierRequirement>(requirements);
#endif
    }
}
