using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Deepshaft.Bosses;
using Deepshaft.Economy;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Progression;
using Deepshaft.Roster;
using Deepshaft.Session;
using UnityEngine;

/// <summary>
/// Grey-box game screen for the core loop playtest (SCRUM-30, SCRUM-8): resources, Mine,
/// Team, Bosses, Expeditions and debug tools, drawn with IMGUI so it needs no scene setup.
/// Shown while the UI is in the Game state. Throwaway: the real screens come with the UI
/// design system (SCRUM-12) and the wireframes (SCRUM-3).
///
/// Sizes are in units of a 1080-wide reference screen (shortest side), so buttons keep the
/// 130-unit minimum touch target from the wireframes doc on every device.
/// </summary>
public class GreyboxGameScreen : MonoBehaviour
{
    enum Tab { Mine, Team, Boss, Expeditions, Debug }

    const float ReferenceSize = 1080f;
    const float TouchTarget = 130f;
    const float Padding = 24f;

    static readonly string[] TabNames = { "Mine", "Team", "Bosses", "Expeditions", "Debug" };

    Tab tab;
    Vector2 scroll;
    string message = "";
    bool resetArmed;
    UIManager ui;

    GUIStyle background;
    GUIStyle label;
    GUIStyle heading;
    GUIStyle small;
    GUIStyle button;
    GUIStyle tabButton;
    GUIStyle box;
    GUIStyle messageBox;

    static GameSession Session => GameSessionRunner.Instance != null ? GameSessionRunner.Instance.Session : null;

    void Start()
    {
        ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (GameSessionRunner.Instance != null) GameSessionRunner.Instance.SessionStarted += OnSessionStarted;
        if (Session != null) OnSessionStarted(Session);
    }

    void OnDestroy()
    {
        if (GameSessionRunner.Instance != null) GameSessionRunner.Instance.SessionStarted -= OnSessionStarted;
    }

    void OnSessionStarted(GameSession session)
    {
        message = DescribeWelcome(session.Offline);
        tab = Tab.Mine;
    }

    bool IsVisible => ui == null || ui.CurrentState == UIState.Game;

    // ---------------------------------------------------------------- Layout

    void OnGUI()
    {
        GameSession s = Session;
        if (s == null || !IsVisible) return;
        EnsureStyles();

        float scale = Mathf.Min(Screen.width, Screen.height) / ReferenceSize;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        // Safe area (notches), converted from screen pixels (y up) to GUI units (y down).
        Rect safe = Screen.safeArea;
        var area = new Rect(safe.x / scale + Padding, (Screen.height - safe.yMax) / scale + Padding,
            safe.width / scale - 2 * Padding, safe.height / scale - 2 * Padding);

        GUI.Box(new Rect(0, 0, width, height), GUIContent.none, background);
        GUILayout.BeginArea(area);

        DrawResourceBar(s);
        DrawTabs();
        if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, messageBox);

        HandleTouchScroll();
        scroll = GUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case Tab.Mine: DrawMine(s); break;
            case Tab.Team: DrawTeam(s); break;
            case Tab.Boss: DrawBosses(s); break;
            case Tab.Expeditions: DrawExpeditions(s); break;
            case Tab.Debug: DrawDebug(s); break;
        }
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    void HandleTouchScroll()
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDrag && Mathf.Abs(e.delta.y) > 0f)
        {
            scroll.y = Mathf.Max(0f, scroll.y - e.delta.y);
            e.Use();
        }
    }

    void DrawResourceBar(GameSession s)
    {
        var parts = new List<string>();
        foreach (ResourceId id in (ResourceId[])Enum.GetValues(typeof(ResourceId)))
        {
            long amount = s.Wallet.Get(id);
            bool always = id == ResourceId.Gold || id == ResourceId.Iron;
            if (amount > 0 || always) parts.Add(Name(id) + " " + Format(amount));
        }
        GUILayout.Label(string.Join("   ·   ", parts), box);
    }

    void DrawTabs()
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < TabNames.Length; i++)
        {
            bool selected = (int)tab == i;
            GUI.color = selected ? new Color(1f, 0.82f, 0.36f) : Color.white;
            if (GUILayout.Button(TabNames[i], tabButton))
            {
                tab = (Tab)i;
                scroll = Vector2.zero;
                resetArmed = false;
                Exit();
            }
        }
        GUI.color = Color.white;
        if (ui != null && GUILayout.Button("Menu", tabButton, GUILayout.Width(200f))) { ui.ShowMainMenu(); Exit(); }
        GUILayout.EndHorizontal();
    }

    // ---------------------------------------------------------------- Mine

    void DrawMine(GameSession s)
    {
        MiningService mining = s.Mining;
        GUILayout.Label("Mine · Tier " + mining.CurrentTier, heading);
        GUILayout.Label("Miners working: " + mining.GetWorkingMiners().Count + " / " + s.Roster.MinerSlots +
                        "  (change them on the Team tab)", label);

        GUILayout.Label("Per hour: " + Describe(mining.GetHourlyRates(), "/h"), label);
        List<MinedAmount> pending = mining.GetPending();
        GUILayout.Label("Waiting to collect: " + Describe(pending, ""), label);

        if (Button("Collect"))
        {
            List<ResourceAmount> collected = s.CollectMine();
            message = collected.Count > 0 ? "Collected " + Describe(collected) + "." : "Nothing to collect yet.";
            Exit();
        }

        GUILayout.Space(30f);
        DrawNextTier(s);
    }

    void DrawNextTier(GameSession s)
    {
        TierUnlockCheck check = s.Tiers.CheckNext(s.Wallet);
        GUILayout.Label("Next tier", heading);
        switch (check.Status)
        {
            case TierUnlockStatus.MaxTierReached:
                GUILayout.Label("Tier " + s.Tiers.HighestUnlockedTier + " is the deepest tier in the prototype.", label);
                break;
            case TierUnlockStatus.BossNotDefeated:
                GUILayout.Label("Tier " + check.Tier + ": beat " + BossNames(s, check.MissingBosses) + ".", label);
                break;
            case TierUnlockStatus.ExpeditionNotCleared:
                GUILayout.Label("Tier " + check.Tier + ": first-clear " + ExpeditionNames(s, check.MissingFirstClears) + ".", label);
                break;
            case TierUnlockStatus.CantAfford:
                GUILayout.Label("Tier " + check.Tier + " costs " + Describe(check.Cost) + ". Missing " +
                                Describe(check.Shortfall) + ".", label);
                break;
            case TierUnlockStatus.Ok:
                if (Button("Unlock Tier " + check.Tier + " (" + Describe(check.Cost) + ")"))
                {
                    TierUnlockCheck result = s.UnlockNextTier();
                    message = result.CanUnlock ? "Tier " + result.Tier + " unlocked! Miners moved down." : "Couldn't unlock: " + result.Status;
                    Exit();
                }
                break;
        }
    }

    // ---------------------------------------------------------------- Team

    void DrawTeam(GameSession s)
    {
        RosterService roster = s.Roster;

        GUILayout.Label("Miners in the Mine (" + roster.GetMiners().Count + " / " + roster.MinerSlots + ")", heading);
        foreach (OwnedMinion m in roster.GetMiners())
        {
            if (Row(Describe(s, m), "Remove")) { Report(s.UnassignMiner(m.instanceId)); Exit(); }
        }
        foreach (OwnedMinion m in roster.GetUnassigned(MinionKind.Miner))
        {
            if (Row(Describe(s, m) + "  (resting)", "Assign", roster.GetMiners().Count < roster.MinerSlots))
            {
                Report(s.AssignMiner(m.instanceId));
                Exit();
            }
        }

        GUILayout.Space(30f);
        GUILayout.Label("Adventure team (" + roster.GetTeam().Count + " / " + roster.AdventureSlots + ")  ·  Power " +
                        Format(s.Bosses.GetTeamPower()), heading);
        if (roster.IsTeamBusy) GUILayout.Label("The team is on an Expedition. Stop it on the Expeditions tab to change the team.", small);
        foreach (OwnedMinion m in roster.GetTeam())
        {
            if (Row(Describe(s, m), "Remove", !roster.IsTeamBusy)) { Report(s.RemoveFromTeam(m.instanceId)); Exit(); }
        }
        foreach (OwnedMinion m in roster.GetUnassigned(MinionKind.Adventure))
        {
            bool canAdd = !roster.IsTeamBusy && roster.GetTeam().Count < roster.AdventureSlots;
            if (Row(Describe(s, m) + "  (bench)", "Add", canAdd)) { Report(s.AssignToTeam(m.instanceId)); Exit(); }
        }
    }

    void Report(AssignResult result)
    {
        message = result == AssignResult.Ok ? "" : "Can't do that: " + result + ".";
    }

    // ---------------------------------------------------------------- Bosses

    void DrawBosses(GameSession s)
    {
        GUILayout.Label("Team power " + Format(s.Bosses.GetTeamPower()), heading);
        foreach (BossDefinition boss in s.Database.Bosses)
        {
            if (boss == null) continue;
            bool defeated = s.Bosses.IsDefeated(boss.id);
            string title = boss.displayName + " · Tier " + boss.tier + " · Recommended " + Format(boss.recommendedPower) +
                           (defeated ? "  (beaten)" : "");
            BossCheck check = s.Bosses.CanFight(boss.id);
            string action = defeated ? "Fight again" : "Fight";
            if (check != BossCheck.Ok) title += "\n" + Explain(check);
            if (Row(title, action, check == BossCheck.Ok))
            {
                message = DescribeFight(s, boss, s.FightBoss(boss.id));
                Exit();
            }
        }
    }

    string DescribeFight(GameSession s, BossDefinition boss, BossFightResult result)
    {
        if (result.Check != BossCheck.Ok) return Explain(result.Check);
        if (!result.Won) return "Lost to " + boss.displayName + " after " + Format(result.Battle.DurationSeconds) + " s. " + result.DescribeLoss();

        var text = new StringBuilder("Beat " + boss.displayName + " in " + Format(result.Battle.DurationSeconds) + " s! +" +
                                     Describe(result.Rewards) + ".");
        if (result.UnlockedExpeditions.Count > 0) text.Append(" New Expedition: " + ExpeditionNames(s, result.UnlockedExpeditions) + ".");
        foreach (int tier in result.UnlockedTiers) text.Append(" Tier " + tier + " unlocked!");
        return text.ToString();
    }

    static string Explain(BossCheck check)
    {
        switch (check)
        {
            case BossCheck.TierLocked: return "Reach this tier first.";
            case BossCheck.NoTeam: return "Put Adventure Minions in the team first.";
            case BossCheck.TeamBusy: return "The team is on an Expedition. Stop it first.";
            case BossCheck.Unknown: return "Unknown boss.";
            default: return "";
        }
    }

    // ---------------------------------------------------------------- Expeditions

    void DrawExpeditions(GameSession s)
    {
        ExpeditionService expeditions = s.Expeditions;
        ExpeditionDefinition active = expeditions.Active;
        if (active != null)
        {
            GUILayout.Label("Running: " + active.displayName + " for " + FormatHours(expeditions.HoursRunning), heading);
            GUILayout.Label("Earns " + Describe(expeditions.GetHourlyRates(), "/h") + ". Earned so far: " +
                            Describe(expeditions.GetPending(), "") + ".", label);
            if (Button("Stop & Collect"))
            {
                ExpeditionCollectResult result = s.StopExpedition();
                message = "Expedition stopped after " + FormatHours(result.HoursRun) + ". Collected " +
                          Describe(result.Collected) + ". The team is free.";
                Exit();
            }
            GUILayout.Space(30f);
        }

        GUILayout.Label("Team power " + Format(expeditions.GetTeamPower()), heading);
        foreach (ExpeditionDefinition e in s.Database.Expeditions)
        {
            if (e == null) continue;
            string title = e.displayName + " · Tier " + e.tier + " · Recommended " + Format(e.recommendedPower);
            switch (expeditions.GetStatus(e.id))
            {
                case ExpeditionStatus.Locked:
                    GUILayout.Label(title + "\nLocked: beat " + BossNames(s, new[] { e.unlockedByBoss }) + ".", box);
                    break;
                case ExpeditionStatus.FirstClearAvailable:
                {
                    ExpeditionCheck check = expeditions.CanFirstClear(e.id);
                    if (check != ExpeditionCheck.Ok) title += "\n" + Explain(check);
                    if (Row(title + "\nFirst clear (can fail)", "First clear", check == ExpeditionCheck.Ok))
                    {
                        message = DescribeFirstClear(e, s.FirstClearExpedition(e.id));
                        Exit();
                    }
                    break;
                }
                case ExpeditionStatus.ReadyToAutomate:
                {
                    ExpeditionCheck check = expeditions.CanStart(e.id);
                    title += "\nCleared · automated: " + Describe(MiningMath.ToRates(e.automatedPerHour), "/h");
                    if (check != ExpeditionCheck.Ok) title += "\n" + Explain(check);
                    if (Row(title, "Automate", check == ExpeditionCheck.Ok))
                    {
                        ExpeditionCheck started = s.StartExpedition(e.id);
                        message = started == ExpeditionCheck.Ok
                            ? e.displayName + " is running. It keeps going while the app is closed."
                            : Explain(started);
                        Exit();
                    }
                    break;
                }
                case ExpeditionStatus.Running:
                    GUILayout.Label(title + "\nRunning now.", box);
                    break;
            }
        }
    }

    static string DescribeFirstClear(ExpeditionDefinition e, FirstClearResult result)
    {
        if (result.Check != ExpeditionCheck.Ok) return Explain(result.Check);
        if (!result.Won) return "First clear of " + e.displayName + " failed. " + result.Battle.DescribeLoss("Enemy");
        string text = "Cleared " + e.displayName + "! +" + Describe(result.Rewards) + ". You can automate it now.";
        foreach (int tier in result.UnlockedTiers) text += " Tier " + tier + " unlocked!";
        return text;
    }

    static string Explain(ExpeditionCheck check)
    {
        switch (check)
        {
            case ExpeditionCheck.Locked: return "Locked.";
            case ExpeditionCheck.AlreadyCleared: return "Already cleared.";
            case ExpeditionCheck.NotCleared: return "First clear it.";
            case ExpeditionCheck.NoTeam: return "Put Adventure Minions in the team first.";
            case ExpeditionCheck.TeamBusy: return "The team is on an Expedition. Stop it first.";
            case ExpeditionCheck.AlreadyRunning: return "Already running.";
            case ExpeditionCheck.AnotherRunning: return "Another Expedition is running. Only one at a time.";
            case ExpeditionCheck.NotRunning: return "No Expedition is running.";
            default: return "";
        }
    }

    // ---------------------------------------------------------------- Debug

    void DrawDebug(GameSession s)
    {
        GameSessionRunner runner = GameSessionRunner.Instance;
        GUILayout.Label("Debug tools (playtest only)", heading);
        GUILayout.Label("Clock skipped ahead: " + FormatHours(runner.Clock.Offset.TotalHours) +
                        (s.SavingAllowed ? "" : "\nSaving is OFF: the save on this device is from a newer build."), small);

        GUILayout.BeginHorizontal();
        if (Button("+1 hour")) { Skip(runner, 1); Exit(); }
        if (Button("+8 hours")) { Skip(runner, 8); Exit(); }
        if (Button("+24 hours")) { Skip(runner, 24); Exit(); }
        GUILayout.EndHorizontal();

        GUILayout.Label("Minion levels aren't built yet (SCRUM-10). Use these to reach Tier 2 and 3.", small);
        GUILayout.BeginHorizontal();
        if (Button("All Minions +1 level")) { LevelUp(s, 1); Exit(); }
        if (Button("+5 levels")) { LevelUp(s, 5); Exit(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(30f);
        if (Button(resetArmed ? "Tap again to DELETE the save" : "Reset game"))
        {
            if (!resetArmed)
            {
                resetArmed = true;
                Exit();
                return;
            }
            resetArmed = false;
            runner.DebugResetGame();
            message = "New game started.";
            Exit();
        }
    }

    void Skip(GameSessionRunner runner, int hours)
    {
        runner.DebugSkipTime(TimeSpan.FromHours(hours));
        message = "Skipped " + hours + " h. Check the Mine and the Expedition.";
    }

    void LevelUp(GameSession s, int levels)
    {
        s.DebugLevelUpAll(levels);
        message = "All Minions +" + levels + " level(s), up to their cap. Team power " + Format(s.Bosses.GetTeamPower()) + ".";
    }

    // ---------------------------------------------------------------- Widgets

    /// <summary>
    /// Ends this GUI pass right after a tap changed the game. Without it, IMGUI would keep
    /// drawing a different number of controls than it laid out and log layout errors.
    /// </summary>
    static void Exit() => GUIUtility.ExitGUI();

    bool Button(string text, bool enabled = true)
    {
        bool was = GUI.enabled;
        GUI.enabled = was && enabled;
        bool clicked = GUILayout.Button(text, button);
        GUI.enabled = was;
        return clicked;
    }

    /// <summary>A text line with a button on the right. Returns true when the button is tapped.</summary>
    bool Row(string text, string action, bool enabled = true)
    {
        GUILayout.BeginHorizontal(box);
        GUILayout.Label(text, label, GUILayout.ExpandWidth(true));
        bool was = GUI.enabled;
        GUI.enabled = was && enabled;
        bool clicked = GUILayout.Button(action, button, GUILayout.Width(300f));
        GUI.enabled = was;
        GUILayout.EndHorizontal();
        return clicked;
    }

    void EnsureStyles()
    {
        if (button != null) return;

        var dark = MakeTexture(new Color(0.07f, 0.06f, 0.09f, 1f));
        var panel = MakeTexture(new Color(0.14f, 0.12f, 0.16f, 1f));
        var note = MakeTexture(new Color(0.25f, 0.18f, 0.08f, 1f));
        Color text = new Color(0.95f, 0.93f, 0.88f);

        background = new GUIStyle { normal = { background = dark } };
        label = new GUIStyle(GUI.skin.label) { fontSize = 34, wordWrap = true, normal = { textColor = text } };
        small = new GUIStyle(label) { fontSize = 28, normal = { textColor = new Color(0.75f, 0.72f, 0.68f) } };
        heading = new GUIStyle(label) { fontSize = 40, fontStyle = FontStyle.Bold, margin = new RectOffset(4, 4, 16, 8),
            normal = { textColor = new Color(1f, 0.82f, 0.36f) } };
        box = new GUIStyle(GUI.skin.box) { fontSize = 32, wordWrap = true, alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(20, 20, 14, 14), margin = new RectOffset(0, 0, 6, 6),
            normal = { background = panel, textColor = text } };
        messageBox = new GUIStyle(box) { normal = { background = note, textColor = text } };
        button = new GUIStyle(GUI.skin.button) { fontSize = 34, fixedHeight = TouchTarget, wordWrap = true };
        tabButton = new GUIStyle(button) { fontSize = 30 };
    }

    static Texture2D MakeTexture(Color color)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, color);
        t.Apply();
        return t;
    }

    // ---------------------------------------------------------------- Text

    static string DescribeWelcome(OfflineReport offline)
    {
        if (offline == null || offline.HoursAway < 1.0 / 60.0) return "";
        string text = "Welcome back! You were away " + FormatHours(offline.HoursAway) + ". Mine: " +
                      Describe(offline.Mine, "") + " waiting.";
        if (offline.Expedition.Count > 0) text += " Expedition: " + Describe(offline.Expedition, "") + " earned.";
        return text;
    }

    static string Describe(GameSession s, OwnedMinion m)
    {
        MinionDefinition d = s.Database.FindMinion(m.definitionId);
        if (d == null) return "Unknown Minion";
        string kind = d.Kind == MinionKind.Miner ? d.MinerTrait + " Miner" : d.Role.ToString();
        return d.DisplayName + " · Lv " + m.level + " · " + kind + " · " + d.Rarity;
    }

    static string Describe(IEnumerable<ResourceAmount> amounts)
    {
        var parts = new List<string>();
        foreach (ResourceAmount a in amounts) if (a.amount > 0) parts.Add(Format(a.amount) + " " + Name(a.id));
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    static string Describe(IEnumerable<MinedAmount> amounts, string suffix)
    {
        var parts = new List<string>();
        foreach (MinedAmount a in amounts)
        {
            if (a.amount <= 0) continue;
            string number = suffix.Length > 0 ? a.amount.ToString("0.#", CultureInfo.InvariantCulture) : Format(Math.Floor(a.amount));
            parts.Add(number + suffix + " " + Name(a.id));
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    static string BossNames(GameSession s, IEnumerable<string> ids)
    {
        var names = new List<string>();
        foreach (string id in ids)
        {
            BossDefinition b = s.Database.FindBoss(id);
            names.Add(b != null ? b.displayName : id);
        }
        return string.Join(", ", names);
    }

    static string ExpeditionNames(GameSession s, IEnumerable<string> ids)
    {
        var names = new List<string>();
        foreach (string id in ids)
        {
            ExpeditionDefinition e = s.Database.FindExpedition(id);
            names.Add(e != null ? e.displayName : id);
        }
        return string.Join(", ", names);
    }

    static string Format(double value) => Math.Floor(value).ToString("#,0", CultureInfo.InvariantCulture);

    static string FormatHours(double hours)
    {
        if (hours < 1.0) return Math.Floor(hours * 60.0).ToString(CultureInfo.InvariantCulture) + " min";
        return hours.ToString("0.#", CultureInfo.InvariantCulture) + " h";
    }

    static string Name(ResourceId id)
    {
        switch (id)
        {
            case ResourceId.BasicShard: return "Basic Shards";
            case ResourceId.AdvancedShard: return "Advanced Shards";
            case ResourceId.MinionEssence: return "Essence";
            case ResourceId.SporeSilk: return "Spore Silk";
            case ResourceId.EmberCore: return "Ember Core";
            case ResourceId.PrismDust: return "Prism Dust";
            case ResourceId.ForemanSigil: return "Foreman Sigil";
            case ResourceId.GolemHeart: return "Golem Heart";
            case ResourceId.WyrmScale: return "Wyrm Scale";
            default: return id.ToString();
        }
    }
}
