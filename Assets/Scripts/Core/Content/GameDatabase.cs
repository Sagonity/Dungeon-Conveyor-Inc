using System.Collections.Generic;
using Deepshaft.Minions;
using Deepshaft.Mining;
using UnityEngine;

namespace Deepshaft.Content
{
    /// <summary>
    /// All game content in one place, so systems can look things up by id.
    /// Create one asset (Assets → Create → Deepshaft → Game Database) and list every definition in it.
    /// Tiers, equipment and bosses get added here as their tickets are built.
    /// </summary>
    [CreateAssetMenu(menuName = "Deepshaft/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        [SerializeField] List<MinionDefinition> minions = new List<MinionDefinition>();
        [SerializeField] MiningRules mining = MiningRules.CreateDefault();

        Dictionary<string, MinionDefinition> minionsById;

        public IReadOnlyList<MinionDefinition> Minions => minions;

        /// <summary>Mining rates per tier and trait bonuses (SCRUM-5). Never null.</summary>
        public MiningRules Mining => mining ?? (mining = MiningRules.CreateDefault());

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
#endif
    }
}
