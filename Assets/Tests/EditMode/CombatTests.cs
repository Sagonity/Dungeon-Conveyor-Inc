using System;
using System.Collections.Generic;
using Deepshaft.Bosses;
using Deepshaft.Combat;
using Deepshaft.Content;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using NUnit.Framework;
using UnityEngine;

namespace Deepshaft.Tests
{
    /// <summary>The autobattle itself (SCRUM-7): outcomes, class behaviour, loss feedback, default balance.</summary>
    public class CombatTests
    {
        CombatRules rules;
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp() => rules = CombatRules.CreateDefault();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in created) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        Combatant Member(MinionRole role, float hp = 100f, float atk = 10f)
        {
            RoleCombatStats r = rules.GetRole(role);
            return new Combatant(role.ToString(), role, hp, atk, r.attackInterval, r.damageMultiplier,
                r.damageTakenMultiplier, r.taunts, r.hitsAllEnemies, r.healsAllies);
        }

        static Combatant Enemy(float hp, float atk, float interval = 2f, int areaEvery = 0) =>
            Combatant.ForEnemy(new EnemyDefinition("Enemy", hp, atk, interval, areaEvery));

        BattleResult Fight(IReadOnlyList<Combatant> team, IReadOnlyList<Combatant> enemies, int seed,
            float recommended = 100f) =>
            CombatSimulator.Simulate(rules, team, enemies, recommended, new System.Random(seed));

        // ---------- Power ----------

        [Test]
        public void TeamPower_IsAtkPlusHpOverTen_PlusTenPercentWithASupport()
        {
            var fighters = new[] { Member(MinionRole.Fighter), Member(MinionRole.Fighter) };
            var withSupport = new[] { Member(MinionRole.Fighter), Member(MinionRole.Support) };

            Assert.AreEqual(40f, CombatSimulator.GetTeamPower(fighters, rules), 1e-4);
            Assert.AreEqual(44f, CombatSimulator.GetTeamPower(withSupport, rules), 1e-4);
        }

        [Test]
        public void MinionStats_ComeFromDefinitionAndLevel()
        {
            MinionDefinition definition = Definition("fighter", MinionRole.Fighter);
            var minion = new OwnedMinion { instanceId = 7, definitionId = "fighter", level = 5 };

            Combatant c = Combatant.ForMinion(definition, minion, rules);

            Assert.AreEqual(140f, c.MaxHp, 1e-4); // 100 + 10 × 4
            Assert.AreEqual(14f, c.Atk, 1e-4);    // 10 + 1 × 4
            Assert.AreEqual(7, c.InstanceId);
            Assert.AreEqual(MinionRole.Fighter, c.Role);
        }

        // ---------- Outcomes ----------

        [Test]
        public void SameTeamEnemiesAndSeed_GiveTheSameFight()
        {
            var team = new[] { Member(MinionRole.Tank), Member(MinionRole.Fighter), Member(MinionRole.Support) };
            var enemies = new[] { Enemy(900, 25, 2f, 4) };

            BattleResult a = Fight(team, enemies, 7);
            BattleResult b = Fight(team, enemies, 7);

            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.AreEqual(a.DurationSeconds, b.DurationSeconds);
            Assert.AreEqual(a.Events.Count, b.Events.Count);
            for (int i = 0; i < a.Events.Count; i++) Assert.AreEqual(a.Events[i].ToString(), b.Events[i].ToString());
        }

        [Test]
        public void MuchStrongerTeam_AlwaysWins_MuchWeakerTeam_AlwaysLoses()
        {
            var strong = new[] { Member(MinionRole.Fighter, 1000, 100), Member(MinionRole.Fighter, 1000, 100) };
            var weak = new[] { Member(MinionRole.Fighter, 50, 2), Member(MinionRole.Fighter, 50, 2) };
            var enemies = new[] { Enemy(500, 20) };

            for (int seed = 0; seed < 30; seed++)
            {
                Assert.IsTrue(Fight(strong, enemies, seed).Won, "strong team, seed " + seed);
                Assert.IsFalse(Fight(weak, enemies, seed).Won, "weak team, seed " + seed);
            }
        }

        [Test]
        public void AWin_KnocksOutEveryEnemy_AndHasNoLossText()
        {
            BattleResult r = Fight(new[] { Member(MinionRole.Fighter, 1000, 100) }, new[] { Enemy(150, 1) }, 1);

            Assert.AreEqual(BattleOutcome.Won, r.Outcome);
            Assert.AreEqual(0f, r.EnemyHpLeftFraction, 1e-6);
            BattleEvent last = r.Events[r.Events.Count - 1];
            Assert.AreEqual(BattleEventType.Defeated, last.Type);
            Assert.AreEqual(BattleSide.Enemies, last.TargetSide);
            Assert.AreEqual(last.Time, r.DurationSeconds, 1e-4);
            Assert.AreEqual("", r.DescribeLoss());
        }

        [Test]
        public void EnemiesStillStandingAtTheTimeLimit_IsALoss()
        {
            BattleResult r = Fight(new[] { Member(MinionRole.Fighter) }, new[] { Enemy(1_000_000, 0) }, 1);

            Assert.AreEqual(BattleOutcome.TimeRanOut, r.Outcome);
            Assert.AreEqual(rules.timeLimitSeconds, r.DurationSeconds, 1e-4);
            Assert.Greater(r.EnemyHpLeftFraction, 0.99f);
            foreach (BattleEvent e in r.Events) Assert.LessOrEqual(e.Time, rules.timeLimitSeconds);
            StringAssert.Contains("Out of time.", r.DescribeLoss("Boss"));
            StringAssert.Contains("Boss HP left: 100%", r.DescribeLoss("Boss"));
        }

        [Test]
        public void ATeamThatIsKnockedOut_Loses()
        {
            BattleResult r = Fight(new[] { Member(MinionRole.Fighter, 50, 1) }, new[] { Enemy(1000, 100) }, 1);

            Assert.AreEqual(BattleOutcome.TeamDefeated, r.Outcome);
            StringAssert.Contains("Your team was knocked out.", r.DescribeLoss());
        }

        [Test]
        public void AnEmptyTeam_LosesAtOnce()
        {
            BattleResult r = Fight(new Combatant[0], new[] { Enemy(100, 10) }, 1);

            Assert.AreEqual(BattleOutcome.TeamDefeated, r.Outcome);
            Assert.AreEqual(0, r.Events.Count);
        }

        // ---------- Loss feedback ----------

        [Test]
        public void ALoss_SaysHowMuchHpWasLeft_AndThatTheTeamIsBelowRecommendedPower()
        {
            var team = new[] { Member(MinionRole.Fighter, 100, 10) }; // power 20
            BattleResult r = Fight(team, new[] { Enemy(1000, 50) }, 3, recommended: 70f);

            Assert.IsFalse(r.Won);
            Assert.IsTrue(r.IsBelowRecommendedPower);
            string text = r.DescribeLoss("Boss");
            int expectedHpLeft = (int)Math.Ceiling(r.EnemyHpLeftFraction * 100 - 1e-4);
            StringAssert.Contains("Boss HP left: " + expectedHpLeft + "%", text);
            StringAssert.Contains("Team power 20 is below the recommended 70", text);
        }

        [Test]
        public void ALossAtRecommendedPower_SuggestsAnotherTeam()
        {
            var team = new[] { Member(MinionRole.Fighter, 100, 10) };
            BattleResult r = Fight(team, new[] { Enemy(1000, 50) }, 3, recommended: 20f);

            Assert.IsFalse(r.IsBelowRecommendedPower);
            StringAssert.Contains("meets the recommended 20", r.DescribeLoss());
        }

        // ---------- Classes ----------

        [Test]
        public void ATank_TakesEverySingleTargetHit_WhileItStands()
        {
            var team = new[] { Member(MinionRole.Fighter), Member(MinionRole.Tank, 100_000), Member(MinionRole.Archer) };
            BattleResult r = Fight(team, new[] { Enemy(1_000_000, 5, 1f) }, 4);

            int enemyHits = 0;
            foreach (BattleEvent e in r.Events)
            {
                if (e.ActorSide != BattleSide.Enemies || e.Type != BattleEventType.Attack) continue;
                Assert.AreEqual(1, e.TargetIndex);
                enemyHits++;
            }
            Assert.Greater(enemyHits, 10);
        }

        [Test]
        public void ATank_TakesLessDamage()
        {
            BattleResult r = Fight(new[] { Member(MinionRole.Tank, 100_000) }, new[] { Enemy(1_000_000, 100, 1f) }, 4);

            foreach (BattleEvent e in r.Events)
            {
                if (e.ActorSide != BattleSide.Enemies) continue;
                // 100 ATK ± 10%, × 0.75 for the Tank.
                Assert.That(e.Amount, Is.InRange(67.4f, 82.6f));
            }
        }

        [Test]
        public void AMage_HitsEveryEnemy()
        {
            var enemies = new[] { Enemy(1000, 0), Enemy(1000, 0), Enemy(1000, 0) };
            BattleResult r = Fight(new[] { Member(MinionRole.Mage) }, enemies, 5);

            var firstVolley = new List<int>();
            float firstTime = r.Events[0].Time;
            foreach (BattleEvent e in r.Events)
            {
                if (e.Time > firstTime) break;
                Assert.AreEqual(BattleEventType.Attack, e.Type);
                firstVolley.Add(e.TargetIndex);
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, firstVolley);
        }

        [Test]
        public void OtherClasses_HitTheFrontEnemyFirst()
        {
            var enemies = new[] { Enemy(1000, 0), Enemy(1000, 0) };
            BattleResult r = Fight(new[] { Member(MinionRole.Fighter) }, enemies, 5);

            foreach (BattleEvent e in r.Events)
            {
                if (e.Time > 5f) break;
                Assert.AreEqual(0, e.TargetIndex);
            }
        }

        [Test]
        public void ASupport_HealsTheMostHurtAlly_AndBoostsTeamDamage()
        {
            var team = new[] { Member(MinionRole.Tank, 1000), Member(MinionRole.Support) };
            BattleResult r = Fight(team, new[] { Enemy(1_000_000, 120, 1f) }, 6);

            bool healed = false;
            foreach (BattleEvent e in r.Events)
            {
                if (e.Type != BattleEventType.Heal) continue;
                healed = true;
                Assert.AreEqual(BattleSide.Team, e.TargetSide);
                Assert.AreEqual(0, e.TargetIndex);
                Assert.Greater(e.Amount, 0f);
                Assert.LessOrEqual(e.TargetHpAfter, 1000f);
            }
            Assert.IsTrue(healed);

            // Tank alone: 10 ATK × 0.7. With a Support in the team: × 1.1 as well.
            foreach (BattleEvent e in r.Events)
            {
                if (e.ActorSide == BattleSide.Team && e.ActorIndex == 0 && e.Type == BattleEventType.Attack)
                {
                    Assert.That(e.Amount, Is.InRange(7f * 1.1f * 0.9f - 1e-3f, 7f * 1.1f * 1.1f + 1e-3f));
                }
            }
        }

        [Test]
        public void AnAreaAttack_HitsTheWholeTeam()
        {
            var team = new[] { Member(MinionRole.Tank, 1000), Member(MinionRole.Fighter, 1000), Member(MinionRole.Mage, 1000) };
            BattleResult r = Fight(team, new[] { Enemy(1_000_000, 100, 1f, areaEvery: 1) }, 8);

            var hit = new HashSet<int>();
            foreach (BattleEvent e in r.Events)
            {
                if (e.ActorSide != BattleSide.Enemies || e.Type == BattleEventType.Defeated) continue;
                Assert.AreEqual(BattleEventType.AreaAttack, e.Type);
                if (e.Time <= 1f) hit.Add(e.TargetIndex);
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, hit);
        }

        // ---------- Default content balance ----------

        MinionDefinition Definition(string id, MinionRole role)
        {
            var d = ScriptableObject.CreateInstance<MinionDefinition>();
            d.EditorSetup(id, MinionKind.Adventure, role, Rarity.Common);
            created.Add(d);
            return d;
        }

        List<Combatant> Team(int level, params MinionRole[] roles)
        {
            var team = new List<Combatant>();
            foreach (MinionRole role in roles)
            {
                var minion = new OwnedMinion { definitionId = role.ToString(), level = level };
                team.Add(Combatant.ForMinion(Definition(role.ToString(), role), minion, rules));
            }
            return team;
        }

        static List<Combatant> Enemies(IEnumerable<EnemyDefinition> definitions)
        {
            var list = new List<Combatant>();
            foreach (EnemyDefinition e in definitions) list.Add(Combatant.ForEnemy(e));
            return list;
        }

        // Tank + Fighter + Support at ~120% of the recommended power; three Fighters at ~75%.
        [TestCase(0, 6, 1)]
        [TestCase(1, 16, 7)]
        [TestCase(2, 35, 21)]
        public void DefaultBosses_AreWonAboveRecommendedPower_AndLostWellBelowIt(int index, int strongLevel, int weakLevel)
        {
            BossDefinition boss = DefaultAdventureContent.Bosses()[index];
            var enemies = Enemies(new[] { boss.enemy });
            List<Combatant> strong = Team(strongLevel, MinionRole.Tank, MinionRole.Fighter, MinionRole.Support);
            List<Combatant> weak = Team(weakLevel, MinionRole.Fighter, MinionRole.Fighter, MinionRole.Fighter);
            Assert.Greater(CombatSimulator.GetTeamPower(strong, rules), boss.recommendedPower * 1.15f);
            Assert.Less(CombatSimulator.GetTeamPower(weak, rules), boss.recommendedPower * 0.8f);

            for (int seed = 0; seed < 20; seed++)
            {
                BattleResult win = Fight(strong, enemies, seed, boss.recommendedPower);
                Assert.IsTrue(win.Won, boss.id + " strong, seed " + seed);
                Assert.That(win.DurationSeconds, Is.InRange(15f, 60f), "a boss fight should take ~30–60 s on screen");
                Assert.IsFalse(Fight(weak, enemies, seed, boss.recommendedPower).Won, boss.id + " weak, seed " + seed);
            }
        }

        [TestCase(0, 10, 1)]
        [TestCase(1, 23, 12)]
        [TestCase(2, 46, 28)]
        public void DefaultExpeditions_AreWonAboveRecommendedPower_AndLostWellBelowIt(int index, int strongLevel, int weakLevel)
        {
            ExpeditionDefinition expedition = DefaultAdventureContent.Expeditions()[index];
            var enemies = Enemies(expedition.enemies);
            List<Combatant> strong = Team(strongLevel, MinionRole.Tank, MinionRole.Fighter, MinionRole.Support);
            List<Combatant> weak = Team(weakLevel, MinionRole.Fighter, MinionRole.Fighter, MinionRole.Fighter);
            Assert.Greater(CombatSimulator.GetTeamPower(strong, rules), expedition.recommendedPower * 1.15f);
            Assert.Less(CombatSimulator.GetTeamPower(weak, rules), expedition.recommendedPower * 0.8f);

            for (int seed = 0; seed < 20; seed++)
            {
                Assert.IsTrue(Fight(strong, enemies, seed, expedition.recommendedPower).Won, expedition.id + " strong, seed " + seed);
                Assert.IsFalse(Fight(weak, enemies, seed, expedition.recommendedPower).Won, expedition.id + " weak, seed " + seed);
            }
        }
    }
}
