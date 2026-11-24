using System;
using System.Collections.Generic;
using System.Globalization;
using Deepshaft.Bosses;
using Deepshaft.Content;
using Deepshaft.Economy;
using Deepshaft.Equipment;
using Deepshaft.Expeditions;
using Deepshaft.Minions;
using Deepshaft.Mining;
using Deepshaft.Progression;
using Deepshaft.Roster;
using Deepshaft.Session;
using Deepshaft.Smith;
using Deepshaft.Summoning;
using UnityEngine;

/// <summary>
/// Game screen for the core loop playtest (SCRUM-30, SCRUM-8), styled after the team's UI
/// mock-up: resource bar, tier header, bronze-framed cards with gold buttons, and a bottom
/// tab bar (HQ, Mine, Exped., Minions, Smith, Boss).
///
/// Drawn with IMGUI and generated textures, so it needs no scene setup and no art. Portraits
/// and boss art are placeholders (SCRUM-13); the final screens come with SCRUM-12.
/// Sizes are in units of a 1080-wide reference screen (shortest side), so buttons keep the
/// 130-unit minimum touch target from the wireframes doc.
/// </summary>
public class GreyboxGameScreen : MonoBehaviour
{
    enum Tab { HQ, Mine, Expeditions, Minions, Smith, Boss }
    enum Overlay { None, Resources, Settings }
    enum Mood { Neutral, Good, Bad }

    static readonly string[] TabNames = { "HQ", "Mine", "Exped.", "Minions", "Smith", "Boss" };

    const float ReferenceSize = 1080f;
    const float MaxColumn = 1080f;
    const float TouchTarget = 130f;
    const float TopBarHeight = 120f;
    const float HeaderHeight = 84f;
    const float NavHeight = 170f;
    const float Side = 24f;
    const float DragThreshold = 18f;

    // Palette (from the mock-up)
    static readonly Color Background = Hex("0E0C12");
    static readonly Color CardFill = Hex("1A1620");
    static readonly Color Bronze = Hex("6B4E26");
    static readonly Color Gold = Hex("F5B83D");
    static readonly Color GoldDark = Hex("2A1B06");
    static readonly Color Text = Hex("EDE6D8");
    static readonly Color Muted = Hex("A59C8C");
    static readonly Color Green = Hex("5FD88C");
    static readonly Color Purple = Hex("B48CFF");
    static readonly Color Red = Hex("FF5A4E");
    static readonly Color Blue = Hex("4FA3FF");

    Tab tab = Tab.HQ;
    Tab? loggedTab; // last tab written to the playtest log (SCRUM-8)
    string playtestNote = "";
    Overlay overlay;
    Vector2 scroll;
    string message = "";
    Mood messageMood;
    bool resetArmed;
    bool showMiners;
    int selectedMinionId; // 0 = none; the Minions tab shows its detail card
    bool summonAdvanced;  // Summon card: Advanced instead of Basic shards

    // SCRUM-32: the last lost fight (for the "what to do next" hint) and a first clear to offer automating.
    string lossName = "";
    float lossPower, lossRecommended;
    string automatePromptId = "";
    UIManager ui;

    Vector2 pressStart;
    bool dragging;

    Styles s;

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
        Say(DescribeWelcome(session), Mood.Good);
        tab = Tab.HQ;
        loggedTab = null;
        playtestNote = "";
        lossName = "";
        automatePromptId = "";
        overlay = Overlay.None;
    }

    bool IsVisible => ui == null || ui.CurrentState == UIState.Game;

    // ================================================================ Frame

    void OnGUI()
    {
        GameSession game = Session;
        if (game == null || !IsVisible) return;
        if (loggedTab != tab)
        {
            // Playtest log (SCRUM-8): where the tester goes, e.g. back to Minions after a lost fight.
            game.LogView("tab " + TabNames[(int)tab]);
            loggedTab = tab;
        }
        if (s == null) s = new Styles();

        float scale = Mathf.Min(Screen.width, Screen.height) / ReferenceSize;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        // Safe area (notches), from screen pixels (y up) to GUI units (y down), then a centred column.
        Rect safe = Screen.safeArea;
        float top = (Screen.height - safe.yMax) / scale;
        float bottom = (safe.y) / scale;
        float columnWidth = Mathf.Min(safe.width / scale, MaxColumn);
        float left = safe.x / scale + (safe.width / scale - columnWidth) / 2f;

        var topBar = new Rect(left, top, columnWidth, TopBarHeight);
        var header = new Rect(left, topBar.yMax, columnWidth, HeaderHeight);
        var nav = new Rect(left, height - bottom - NavHeight, columnWidth, NavHeight);
        var content = new Rect(left + Side, header.yMax, columnWidth - 2 * Side, nav.y - header.yMax);
        Rect overlayRect = GetOverlayRect(topBar);
        overlayRect.height = Mathf.Min(overlayRect.height, nav.yMax - overlayRect.y);

        CloseOverlayOnOutsideTap(overlayRect);
        GUI.DrawTexture(new Rect(0, 0, width, height), s.Solid(Background));

        bool modal = overlay != Overlay.None;
        GUI.enabled = !modal;

        DrawTopBar(game, topBar);
        DrawHeader(game, header);

        HandleTouchScroll();
        GUILayout.BeginArea(content);
        scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUIStyle.none);
        DrawMessage();
        switch (tab)
        {
            case Tab.HQ: DrawHQ(game); break;
            case Tab.Mine: DrawMineTab(game); break;
            case Tab.Expeditions: DrawExpeditionsTab(game); break;
            case Tab.Minions: DrawMinionsTab(game); break;
            case Tab.Smith: DrawSmithTab(game); break;
            case Tab.Boss: DrawBossTab(game); break;
        }
        GUILayout.Space(24f);
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawNav(game, nav);

        GUI.enabled = true;
        if (modal)
        {
            GUI.DrawTexture(new Rect(0, 0, width, height), s.Solid(new Color(0f, 0f, 0f, 0.45f)));
            if (overlay == Overlay.Resources) DrawResourcePanel(game, overlayRect);
            else DrawSettingsPanel(game, overlayRect);
        }
    }

    // ================================================================ Top bar, header, nav

    static readonly ResourceId[] BarResources =
    {
        ResourceId.Gold, ResourceId.Iron, ResourceId.Silver, ResourceId.Mithril,
        ResourceId.BasicShard, ResourceId.AdvancedShard,
    };

    void DrawTopBar(GameSession game, Rect bar)
    {
        float menuSize = 96f;
        float chipWidth = (bar.width - 2 * Side - menuSize) / BarResources.Length;
        for (int i = 0; i < BarResources.Length; i++)
        {
            var chip = new Rect(bar.x + Side + i * chipWidth, bar.y + 26f, chipWidth, 64f);
            DrawIcon(new Rect(chip.x, chip.y + 8f, 48f, 48f), BarResources[i]);
            GUI.Label(new Rect(chip.x + 56f, chip.y, chipWidth - 56f, 64f), Short(game.Wallet.Get(BarResources[i])), s.chip);
        }

        // Tap the resources to see every material.
        var resources = new Rect(bar.x, bar.y, bar.width - menuSize - Side, bar.height);
        if (GUI.Button(resources, GUIContent.none, GUIStyle.none)) { overlay = Overlay.Resources; Exit(); }

        var menu = new Rect(bar.xMax - Side - menuSize, bar.y + 12f, menuSize, menuSize);
        if (GUI.Button(menu, GUIContent.none, s.iconButton)) { overlay = Overlay.Settings; resetArmed = false; Exit(); }
        for (int i = 0; i < 3; i++)
        {
            GUI.DrawTexture(new Rect(menu.x + 26f, menu.y + 30f + i * 16f, menu.width - 52f, 6f), s.Solid(Gold));
        }
    }

    void DrawHeader(GameSession game, Rect header)
    {
        int tier = game.Mining.CurrentTier;
        GUI.Label(new Rect(header.x + Side, header.y, header.width * 0.55f, header.height),
            "<color=#A59C8C>TIER " + tier + " ·</color> " + TierName(tier).ToUpperInvariant(), s.header);

        string next = DescribeNextGoal(game);
        Vector2 size = s.pill.CalcSize(new GUIContent(next));
        var pill = new Rect(header.xMax - Side - size.x, header.y + (header.height - size.y) / 2f, size.x, size.y);
        GUI.Label(pill, next, s.pill);
    }

    void DrawNav(GameSession game, Rect nav)
    {
        GUI.DrawTexture(nav, s.Solid(Hex("15121A")));
        GUI.DrawTexture(new Rect(nav.x, nav.y, nav.width, 3f), s.Solid(Bronze));
        float w = nav.width / TabNames.Length;
        for (int i = 0; i < TabNames.Length; i++)
        {
            var cell = new Rect(nav.x + i * w, nav.y, w, nav.height);
            bool selected = (int)tab == i;
            if (selected) GUI.DrawTexture(new Rect(cell.x + w * 0.2f, cell.y, w * 0.6f, 6f), s.Solid(Gold));

            Color iconColor = selected ? Gold : Muted;
            DrawTinted(new Rect(cell.center.x - 26f, cell.y + 30f, 52f, 52f), s.diamond, iconColor);
            GUI.Label(new Rect(cell.x, cell.y + 92f, w, 50f), TabNames[i], selected ? s.navSelected : s.nav);

            if (NeedsAttention(game, (Tab)i))
            {
                DrawTinted(new Rect(cell.center.x + 22f, cell.y + 24f, 22f, 22f), s.circle, Red);
            }

            if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
            {
                tab = (Tab)i;
                scroll = Vector2.zero;
                Exit();
            }
        }
    }

    bool NeedsAttention(GameSession game, Tab t)
    {
        switch (t)
        {
            case Tab.Mine:
                foreach (MinedAmount a in game.Mining.GetPending()) if (a.amount >= 1.0) return true;
                return false;
            case Tab.Expeditions:
                foreach (ExpeditionDefinition e in game.Database.Expeditions)
                {
                    if (e == null) continue;
                    if (game.Expeditions.CanFirstClear(e.id) == ExpeditionCheck.Ok) return true;
                    if (game.Expeditions.CanStart(e.id) == ExpeditionCheck.Ok) return true;
                }
                return false;
            case Tab.Boss:
                BossDefinition boss = game.Bosses.CurrentBoss;
                return boss != null && !game.Bosses.IsDefeated(boss.id);
            case Tab.Minions:
                return game.Roster.GetUnassigned(MinionKind.Adventure).Count > 0 &&
                       game.Roster.GetTeam().Count < game.Roster.AdventureSlots && !game.Roster.IsTeamBusy;
            default:
                return false;
        }
    }

    void DrawMessage()
    {
        if (string.IsNullOrEmpty(message)) return;
        GUIStyle style = messageMood == Mood.Good ? s.toastGood : messageMood == Mood.Bad ? s.toastBad : s.toast;
        if (GUILayout.Button(message, style)) { message = ""; Exit(); }
    }

    // ================================================================ HQ

    void DrawHQ(GameSession game)
    {
        DrawLossHint(game);
        DrawAutomatePrompt(game);
        DrawProductionCard(game);
        DrawExpeditionCard(game, true);
        DrawSmithSummary(game);
        DrawBossSummary(game);
    }

    /// <summary>HQ overview (SCRUM-9): everything earned per hour, from the Mine and the automated Expedition.</summary>
    void DrawProductionCard(GameSession game)
    {
        MiningService mining = game.Mining;
        ExpeditionDefinition active = game.Expeditions.Active;
        BeginCard();
        CardHeader("HQ · PRODUCTION", mining.GetWorkingMiners().Count > 0 ? "Running" : "No Miners",
            mining.GetWorkingMiners().Count > 0 ? s.pillGreen : s.pillRed);

        GUILayout.Label("Per hour, in total", s.body);
        var perHour = new List<KeyValuePair<ResourceId, double>>();
        foreach (MinedAmount r in game.GetProductionPerHour()) perHour.Add(Pair(r.id, r.amount));
        ResourceLine(perHour, "+");
        GUILayout.Label("Mine: " + DescribeRates(mining.GetHourlyRates()), s.small);
        GUILayout.Label(active != null
            ? "Expedition " + active.displayName + ": " + DescribeRates(game.Expeditions.GetHourlyRates()) + " (collect when you stop it)"
            : "No automated Expedition running.", s.small);

        GUILayout.Space(8f);
        GUILayout.Label("Waiting in the Mine", s.body);
        var waiting = new List<KeyValuePair<ResourceId, double>>();
        List<MinedAmount> pending = mining.GetPending();
        foreach (MinedAmount r in mining.GetHourlyRates()) waiting.Add(Pair(r.id, MiningMath.Get(pending, r.id)));
        ResourceLine(waiting, "+");
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GoldButton("Collect", 300f))
        {
            List<ResourceAmount> collected = game.CollectMine();
            Say(collected.Count > 0 ? "Collected " + Describe(collected) + "." : "Nothing to collect yet.", Mood.Good);
            Exit();
        }
        GUILayout.EndHorizontal();
        EndCard();
    }

    // ================================================================ Mine

    void DrawMineTab(GameSession game)
    {
        DrawMineCard(game);
        DrawMinerCard(game);
        DrawNextTierCard(game);
    }

    void DrawMineCard(GameSession game)
    {
        MiningService mining = game.Mining;
        int miners = mining.GetWorkingMiners().Count;
        BeginCard();
        CardHeader("MINE", miners > 0 ? "Digging · " + miners + (miners == 1 ? " Miner" : " Miners") : "No Miners",
            miners > 0 ? s.pillGreen : s.pillRed);

        GUILayout.Label("Waiting to collect", s.body);
        List<MinedAmount> rates = mining.GetHourlyRates();
        List<MinedAmount> pending = mining.GetPending();
        var waiting = new List<KeyValuePair<ResourceId, double>>();
        foreach (MinedAmount r in rates) waiting.Add(Pair(r.id, MiningMath.Get(pending, r.id)));
        ResourceLine(waiting, "+");

        GUILayout.Label(DescribeRates(rates) + " per hour · no limit", s.small);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GoldButton("Collect", 300f))
        {
            List<ResourceAmount> collected = game.CollectMine();
            Say(collected.Count > 0 ? "Collected " + Describe(collected) + "." : "Nothing to collect yet.", Mood.Good);
            Exit();
        }
        GUILayout.EndHorizontal();
        EndCard();
    }

    void DrawMinerCard(GameSession game)
    {
        RosterService roster = game.Roster;
        BeginCard();
        CardHeader("MINERS (" + roster.GetMiners().Count + "/" + roster.MinerSlots + ")", "Tap to assign", s.pill);
        var all = new List<OwnedMinion>(roster.GetMiners());
        all.AddRange(roster.GetUnassigned(MinionKind.Miner));
        MinionGrid(game, all);
        EndCard();
    }

    void DrawNextTierCard(GameSession game)
    {
        TierUnlockCheck check = game.Tiers.CheckNext(game.Wallet);
        BeginCard();
        CardHeader("NEXT TIER", check.Status == TierUnlockStatus.MaxTierReached ? "Deepest tier" : "Tier " + check.Tier, s.pill);
        switch (check.Status)
        {
            case TierUnlockStatus.MaxTierReached:
                GUILayout.Label(TierName(game.Tiers.HighestUnlockedTier) + " is the deepest tier in the prototype.", s.body);
                break;
            case TierUnlockStatus.BossNotDefeated:
                GUILayout.Label(TierName(check.Tier) + ": beat " + BossNames(game, check.MissingBosses) + ".", s.body);
                break;
            case TierUnlockStatus.ExpeditionNotCleared:
                GUILayout.Label(TierName(check.Tier) + ": first-clear " + ExpeditionNames(game, check.MissingFirstClears) + ".", s.body);
                break;
            case TierUnlockStatus.CantAfford:
                GUILayout.Label(TierName(check.Tier) + " costs " + Describe(check.Cost) + ".", s.body);
                GUILayout.Label("Need " + Describe(check.Shortfall) + " more", s.smallRed);
                break;
            case TierUnlockStatus.Ok:
                GUILayout.BeginHorizontal();
                GUILayout.Label(Describe(check.Cost), s.body);
                GUILayout.FlexibleSpace();
                if (GoldButton("Unlock", 300f))
                {
                    TierUnlockCheck result = game.UnlockNextTier();
                    Say(result.CanUnlock ? TierName(result.Tier) + " unlocked! Miners moved down." : "Couldn't unlock: " + result.Status,
                        result.CanUnlock ? Mood.Good : Mood.Bad);
                    Exit();
                }
                GUILayout.EndHorizontal();
                break;
        }
        EndCard();
    }

    // ================================================================ Expeditions

    void DrawExpeditionsTab(GameSession game)
    {
        DrawLossHint(game);
        DrawAutomatePrompt(game);
        if (game.Expeditions.IsRunning) DrawExpeditionCard(game, false);
        DrawTeamSlotsCard(game, "ADVENTURE TEAM", "Only one Expedition runs at a time.");
        DrawExpeditionList(game);
    }

    void DrawExpeditionCard(GameSession game, bool onHQ)
    {
        ExpeditionService expeditions = game.Expeditions;
        ExpeditionDefinition active = expeditions.Active;
        BeginCard();
        if (active != null)
        {
            CardHeader("EXPEDITION", "Automated · running", s.pillPurple);
            // No "earned so far" for rare materials (team decision 2026-11-24): show the rate, not a running total.
            GUILayout.Label("<b>" + active.displayName + "</b> <color=#A59C8C>· running for " +
                            Hours(expeditions.HoursRunning) + "</color>", s.body);
            GUILayout.Label("Farms " + DescribeRates(expeditions.GetHourlyRates()) + " per hour. Collected when you stop it.", s.body);
            GUILayout.Label("Never fails · no time limit · keeps running while the phone is locked", s.small);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GoldButton("Stop & Collect", 380f))
            {
                ExpeditionCollectResult result = game.StopExpedition();
                Say("Expedition stopped after " + Hours(result.HoursRun) + ". Collected " + Describe(result.Collected) +
                    ". The team is free.", Mood.Good);
                Exit();
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            CardHeader("EXPEDITION", "Idle", s.pill);
            ExpeditionDefinition next = NextExpeditionStep(game, out string what);
            GUILayout.Label(next != null ? "<b>" + next.displayName + "</b> <color=#A59C8C>· " + what + "</color>"
                                         : "Beat a boss to unlock Expeditions.", s.body);
            if (onHQ)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GoldButton("Go", 300f)) { tab = Tab.Expeditions; scroll = Vector2.zero; Exit(); }
                GUILayout.EndHorizontal();
            }
        }
        EndCard();
    }

    void DrawExpeditionList(GameSession game)
    {
        ExpeditionService expeditions = game.Expeditions;
        BeginCard();
        CardHeader("EXPEDITIONS", "Power " + Format(expeditions.GetTeamPower()), s.pill);
        foreach (ExpeditionDefinition e in game.Database.Expeditions)
        {
            if (e == null) continue;
            ExpeditionStatus status = expeditions.GetStatus(e.id);
            GUILayout.BeginHorizontal(s.row);

            Color dot = status == ExpeditionStatus.Locked ? Muted : status == ExpeditionStatus.FirstClearAvailable ? Gold : Green;
            Rect dotRect = GUILayoutUtility.GetRect(30f, 30f, GUILayout.Width(30f), GUILayout.Height(30f));
            DrawTinted(new Rect(dotRect.x, dotRect.y + 34f, 26f, 26f), s.circle, dot);
            GUILayout.Space(12f);

            string detail;
            string action = null;
            bool enabled = false;
            switch (status)
            {
                case ExpeditionStatus.Locked:
                    detail = "beat " + BossNames(game, new[] { e.unlockedByBoss });
                    break;
                case ExpeditionStatus.FirstClearAvailable:
                    detail = "first clear needed · Rec. " + Format(e.recommendedPower);
                    action = "First clear";
                    enabled = expeditions.CanFirstClear(e.id) == ExpeditionCheck.Ok;
                    break;
                case ExpeditionStatus.ReadyToAutomate:
                    detail = "cleared · " + DescribeRates(MiningMath.ToRates(e.automatedPerHour)) + "/h";
                    action = "Automate";
                    enabled = expeditions.CanStart(e.id) == ExpeditionCheck.Ok;
                    break;
                default:
                    detail = "automated · running";
                    break;
            }
            GUILayout.Label("<b>" + e.displayName + "</b> <color=#A59C8C>· " + detail + "</color>", s.body,
                GUILayout.ExpandWidth(true));

            if (action != null && SmallGoldButton(action, enabled))
            {
                if (status == ExpeditionStatus.FirstClearAvailable)
                {
                    float powerBefore = game.Expeditions.GetTeamPower();
                    FirstClearResult result = game.FirstClearExpedition(e.id);
                    Say(DescribeFirstClear(e, result), result.Won ? Mood.Good : Mood.Bad);
                    if (result.Check == ExpeditionCheck.Ok) RememberFight(result.Won, e.displayName, powerBefore, e.recommendedPower);
                    if (result.Won) automatePromptId = e.id;
                }
                else
                {
                    ExpeditionCheck started = game.StartExpedition(e.id);
                    if (started == ExpeditionCheck.Ok) automatePromptId = "";
                    Say(started == ExpeditionCheck.Ok ? e.displayName + " is running. It keeps going while the app is closed."
                        : Explain(started), started == ExpeditionCheck.Ok ? Mood.Good : Mood.Bad);
                }
                Exit();
            }
            GUILayout.EndHorizontal();
        }
        if (game.Roster.IsTeamBusy)
        {
            GUILayout.Label("The team is on an Expedition. Stop it to fight or first-clear.", s.small);
        }
        EndCard();
    }

    ExpeditionDefinition NextExpeditionStep(GameSession game, out string what)
    {
        foreach (ExpeditionDefinition e in game.Database.Expeditions)
        {
            if (e == null) continue;
            ExpeditionStatus status = game.Expeditions.GetStatus(e.id);
            if (status == ExpeditionStatus.FirstClearAvailable) { what = "first clear needed"; return e; }
            if (status == ExpeditionStatus.ReadyToAutomate) { what = "ready to automate"; return e; }
        }
        what = "";
        return null;
    }

    // ================================================================ Minions

    void DrawMinionsTab(GameSession game)
    {
        RosterService roster = game.Roster;
        MinionKind kind = showMiners ? MinionKind.Miner : MinionKind.Adventure;
        int ownedOther = Count(game, showMiners ? MinionKind.Adventure : MinionKind.Miner);

        BeginCard();
        GUILayout.BeginHorizontal();
        GUILayout.Label((showMiners ? "MINERS (" : "ADVENTURE (") + Count(game, kind) + ")", s.title);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button((showMiners ? "Adventure (" : "Miners (") + ownedOther + ")", s.pillButton))
        {
            showMiners = !showMiners;
            selectedMinionId = 0;
            Exit();
        }
        GUILayout.EndHorizontal();

        List<OwnedMinion> assigned = showMiners ? roster.GetMiners() : roster.GetTeam();
        var all = new List<OwnedMinion>(assigned);
        all.AddRange(roster.GetUnassigned(kind));
        MinionGrid(game, all);

        string hint = showMiners
            ? "Tap a Miner to level it up or put it in the Mine (" + roster.MinerSlots + " slots)."
            : "Tap a Minion to level it up or put it in the team (" + roster.AdventureSlots + " slots).";
        GUILayout.Label(hint, s.small);
        EndCard();

        DrawMinionDetail(game);
        DrawSummonCard(game);
    }

    static int Count(GameSession game, MinionKind kind)
    {
        int n = 0;
        foreach (OwnedMinion m in game.State.minions)
        {
            MinionDefinition d = game.Database.FindMinion(m.definitionId);
            if (d != null && d.Kind == kind) n++;
        }
        return n;
    }

    void MinionGrid(GameSession game, List<OwnedMinion> minions)
    {
        const int columns = 3;
        const float gap = 18f;
        const float cardHeight = 400f;
        float available = Mathf.Max(300f, s.lastCardInnerWidth);
        float cardWidth = (available - gap * (columns - 1)) / columns;

        for (int start = 0; start < minions.Count; start += columns)
        {
            GUILayout.BeginHorizontal();
            for (int i = start; i < start + columns; i++)
            {
                if (i > start) GUILayout.Space(gap);
                Rect r = GUILayoutUtility.GetRect(cardWidth, cardHeight, GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
                if (i < minions.Count) MinionCard(game, r, minions[i]);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(gap);
        }
    }

    void MinionCard(GameSession game, Rect r, OwnedMinion m)
    {
        MinionDefinition d = game.Database.FindMinion(m.definitionId);
        if (d == null) return;
        RosterService roster = game.Roster;
        bool isMiner = d.Kind == MinionKind.Miner;
        bool assigned = roster.IsAssigned(m.instanceId);
        bool busy = !isMiner && assigned && roster.IsTeamBusy;

        GUI.Box(r, GUIContent.none, assigned ? s.minionCardOn : s.minionCard);
        if (m.instanceId == selectedMinionId) DrawTinted(new Rect(r.x + 10f, r.y + 4f, r.width - 20f, 8f), s.roundedWhite, Gold);

        // Portrait placeholder: class colour and initial (art: SCRUM-13).
        var portrait = new Rect(r.x + 14f, r.y + 14f, r.width - 28f, r.height * 0.5f);
        DrawTinted(portrait, s.roundedWhite, ClassColor(d) * 0.55f + new Color(0, 0, 0, 0.45f));
        GUI.Label(portrait, Initial(d.DisplayName), s.portraitLetter);

        float y = portrait.yMax + 10f;
        GUI.Label(new Rect(r.x + 16f, y, r.width - 32f, 44f), d.DisplayName, s.minionName);
        string job = isMiner ? d.MinerTrait + " Miner" : d.Role.ToString();
        GUI.Label(new Rect(r.x + 16f, y + 42f, r.width - 32f, 36f), job + " · Lv " + m.level + "/" + game.Minions.GetMaxLevel(m), s.minionSub);

        // Breakthrough pips (up to 3 shown).
        for (int i = 0; i < 3; i++)
        {
            DrawTinted(new Rect(r.x + 16f + i * 32f, y + 84f, 26f, 26f), s.diamond, i < m.breakthroughs ? Gold : Hex("4A4050"));
        }

        string tag = busy ? "Busy" : assigned ? (isMiner ? "Mining" : "Team") : (isMiner ? "Resting" : "Bench");
        GUIStyle tagStyle = busy ? s.tagPurple : assigned ? s.tagGreen : s.tagMuted;
        GUI.Label(new Rect(r.x + 16f, r.yMax - 52f, r.width - 32f, 40f), tag, tagStyle);

        if (GUI.Button(r, GUIContent.none, GUIStyle.none))
        {
            selectedMinionId = selectedMinionId == m.instanceId ? 0 : m.instanceId;
            Exit();
        }
    }

    /// <summary>
    /// The selected Minion (SCRUM-10; wireframes §4.5 "Minion detail"): level and cap, stats now →
    /// next level, Level Up, Breakthrough, gear, and the Mine / team button.
    /// </summary>
    void DrawMinionDetail(GameSession game)
    {
        OwnedMinion m = game.State.FindMinion(selectedMinionId);
        MinionDefinition d = m != null ? game.Database.FindMinion(m.definitionId) : null;
        if (d == null || d.Kind != (showMiners ? MinionKind.Miner : MinionKind.Adventure)) return;

        bool isMiner = d.Kind == MinionKind.Miner;
        MinionProgressionService minions = game.Minions;
        EquipmentRules gear = game.Database.Equipment;
        int max = minions.GetMaxLevel(m);
        int next = Math.Min(m.level + 1, max);

        BeginCard();
        CardHeader(d.DisplayName.ToUpperInvariant(), d.Rarity + " · " + (isMiner ? d.MinerTrait + " Miner" : d.Role.ToString()), s.pill);
        GUILayout.Label("<b>Lv " + m.level + " / " + max + "</b> <color=#A59C8C>· breakthroughs " + m.breakthroughs + "/" + minions.MaxBreakthroughs + "</color>", s.body);

        if (isMiner)
        {
            float now = MinionStatCalculator.GetMiningPower(d, m.level);
            float after = MinionStatCalculator.GetMiningPower(d, next);
            GUILayout.Label("Mining power " + Format(now) + Arrow(now, after), s.body);
        }
        else
        {
            float atk = MinionStatCalculator.GetAtk(d, m, m.level, gear);
            float hp = MinionStatCalculator.GetHp(d, m, m.level, gear);
            float atkNext = MinionStatCalculator.GetAtk(d, m, next, gear);
            float hpNext = MinionStatCalculator.GetHp(d, m, next, gear);
            float power = game.Database.Combat.GetPower(hp, atk);
            float powerNext = game.Database.Combat.GetPower(hpNext, atkNext);
            GUILayout.Label("ATK " + Format(atk) + Arrow(atk, atkNext) + "   HP " + Format(hp) + Arrow(hp, hpNext), s.body);
            GUILayout.Label("Power " + Format(power) + Arrow(power, powerNext), s.small);
        }

        // Level Up
        GUILayout.Space(10f);
        MinionUpgradeCheck level = minions.CheckLevelUp(m.instanceId, game.Wallet);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label(level.Status == MinionUpgradeStatus.AtLevelCap ? "At the level cap" : "Level Up: " + Describe(level.Cost), s.body);
        if (level.Status == MinionUpgradeStatus.CantAfford) Missing(game, level.Shortfall);
        else if (level.Status == MinionUpgradeStatus.AtLevelCap) GUILayout.Label(Explain(level.Status), s.small);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        BulkPurchase levelMax = minions.CheckLevelUpMax(m.instanceId, game.Wallet);
        if (levelMax.Levels >= 2 && GoldButton("Max ×" + levelMax.Levels, 240f))
        {
            BulkPurchase bought = game.LevelUpMinionMax(m.instanceId);
            Say(d.DisplayName + " +" + bought.Levels + " levels, now level " + bought.ToLevel + ". Paid " + Describe(bought.Cost) + ".", Mood.Good);
            Exit();
        }
        if (GoldButton("Level Up", 300f, level.CanDo))
        {
            MinionUpgradeCheck result = game.LevelUpMinion(m.instanceId);
            if (result.CanDo) Say(d.DisplayName + " is now level " + result.Level + ".", Mood.Good);
            else Say(Explain(result.Status), Mood.Bad);
            Exit();
        }
        GUILayout.EndHorizontal();
        if (levelMax.Levels >= 2) GUILayout.Label("Max buys " + levelMax.Levels + " levels for " + Describe(levelMax.Cost) + ".", s.small);

        // Breakthrough
        GUILayout.Space(10f);
        MinionUpgradeCheck breakthrough = minions.CheckBreakthrough(m.instanceId, game.Wallet);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (breakthrough.Status == MinionUpgradeStatus.NoMoreBreakthroughs)
        {
            GUILayout.Label("Every breakthrough done", s.body);
        }
        else
        {
            GUILayout.Label("Breakthrough " + (m.breakthroughs + 1) + ": " + Describe(breakthrough.Cost), s.body);
            if (breakthrough.Shortfall.Count > 0) Missing(game, breakthrough.Shortfall);
            GUILayout.Label(breakthrough.Status == MinionUpgradeStatus.BelowLevelCap
                ? "Reach level " + max + " first. Raises the cap to " + d.GetMaxLevel(m.breakthroughs + 1) + "."
                : "Raises the level cap to " + d.GetMaxLevel(m.breakthroughs + 1) + ".", s.small);
        }
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        if (GoldButton("Break", 300f, breakthrough.CanDo))
        {
            MinionUpgradeCheck result = game.BreakthroughMinion(m.instanceId);
            if (result.CanDo) Say(d.DisplayName + " broke through! Level cap " + result.MaxLevel + ".", Mood.Good);
            else Say(Explain(result.Status), Mood.Bad);
            Exit();
        }
        GUILayout.EndHorizontal();

        // Gear (upgraded at the Smith, SCRUM-9)
        GUILayout.Space(10f);
        GUILayout.Label("GEAR", s.panelHeading);
        foreach (EquipmentSlot slot in EquipmentRules.SlotsFor(d.Kind))
        {
            EquipmentSlotRules rules = gear.Get(slot);
            int gearLevel = EquipmentRules.GetLevel(m, slot);
            string effect = slot == EquipmentSlot.Tool ? "mining output" : slot == EquipmentSlot.Weapon ? "ATK" : "HP";
            GUILayout.Label("<b>" + rules.displayName + "</b> Lv " + gearLevel + " <color=#A59C8C>· +" +
                            Percent(rules.GetBonus(gearLevel)) + " " + effect + "</color>", s.body);
        }
        if (DarkButton("Upgrade gear at the Smith")) { tab = Tab.Smith; scroll = Vector2.zero; Exit(); }

        // Mine / team
        GUILayout.Space(10f);
        RosterService roster = game.Roster;
        bool assigned = roster.IsAssigned(m.instanceId);
        List<OwnedMinion> members = isMiner ? roster.GetMiners() : roster.GetTeam();
        bool full = members.Count >= (isMiner ? roster.MinerSlots : roster.AdventureSlots);
        if (!isMiner && roster.IsTeamBusy) GUILayout.Label("The team is on an Expedition. Stop it to change the team.", s.small);
        if (!assigned && full && (isMiner || !roster.IsTeamBusy))
        {
            // Full: swap in one tap instead of bench-then-add (SCRUM-34).
            GUILayout.Label(isMiner ? "All Mine slots are full. Swap with:" : "The team is full. Swap with:", s.small);
            foreach (OwnedMinion other in members)
            {
                MinionDefinition od = game.Database.FindMinion(other.definitionId);
                if (od == null) continue;
                string strength = isMiner
                    ? Format(MinionStatCalculator.GetMiningPower(od, other.level)) + " mining power"
                    : Format(game.Database.Combat.GetPower(MinionStatCalculator.GetHp(game.Database, other),
                        MinionStatCalculator.GetAtk(game.Database, other))) + " power";
                if (DarkButton("Swap with " + od.DisplayName + " · Lv " + other.level + " · " + strength))
                {
                    AssignResult swapped = isMiner ? game.SwapMiner(m.instanceId, other.instanceId)
                                                   : game.SwapIntoTeam(m.instanceId, other.instanceId);
                    if (swapped == AssignResult.Ok) Say(d.DisplayName + " is in; " + od.DisplayName + (isMiner ? " is resting." : " is on the bench."), Mood.Good);
                    else Say(Explain(swapped), Mood.Bad);
                    Exit();
                }
            }
            EndCard();
            return;
        }
        string assignLabel = isMiner ? (assigned ? "Take out of Mine" : "Put in Mine") : (assigned ? "Bench" : "Add to team");
        if (DarkButton(assignLabel))
        {
            AssignResult result;
            if (isMiner) result = assigned ? game.UnassignMiner(m.instanceId) : game.AssignMiner(m.instanceId);
            else result = assigned ? game.RemoveFromTeam(m.instanceId) : game.AssignToTeam(m.instanceId);
            if (result != AssignResult.Ok) Say(Explain(result), Mood.Bad);
            Exit();
        }
        EndCard();
    }

    static string Arrow(float now, float next) =>
        next > now ? " <color=#5FD88C>→ " + Format(next) + "</color>" : "";

    static string Percent(float fraction) =>
        Math.Round(fraction * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// Summoning (SCRUM-11; wireframes §4.6): pick the pool (follows the Minions tab: Miners or
    /// Adventure) and the shard, see the real rates, summon one. Short on shards: says where they come from.
    /// </summary>
    void DrawSummonCard(GameSession game)
    {
        MinionKind pool = showMiners ? MinionKind.Miner : MinionKind.Adventure;
        ShardType shard = summonAdvanced ? ShardType.Advanced : ShardType.Basic;

        BeginCard();
        GUILayout.BeginHorizontal();
        GUILayout.Label("SUMMON", s.title);
        GUILayout.FlexibleSpace();
        foreach (ResourceId id in new[] { ResourceId.BasicShard, ResourceId.AdvancedShard })
        {
            Rect icon = GUILayoutUtility.GetRect(44f, 44f, GUILayout.Width(44f), GUILayout.Height(44f));
            DrawIcon(new Rect(icon.x, icon.y + 6f, 40f, 40f), id);
            GUILayout.Label(Short(game.Wallet.Get(id)), s.chipInline);
            GUILayout.Space(12f);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Miner", showMiners ? s.segmentOn : s.segment)) { showMiners = true; selectedMinionId = 0; Exit(); }
        if (GUILayout.Button("Adventure", showMiners ? s.segment : s.segmentOn)) { showMiners = false; selectedMinionId = 0; Exit(); }
        GUILayout.Space(18f);
        if (GUILayout.Button("Basic", summonAdvanced ? s.segment : s.segmentOn)) { summonAdvanced = false; Exit(); }
        if (GUILayout.Button("Advanced", summonAdvanced ? s.segmentOn : s.segment)) { summonAdvanced = true; Exit(); }
        GUILayout.EndHorizontal();

        // Rates shown on the screen itself (store compliance, SCRUM-25): the real chances for this pool.
        Dictionary<Rarity, double> rates = game.Summoning.GetRates(pool, shard);
        GUILayout.Label("Common " + Pct(rates[Rarity.Common]) + " · <color=#5FD88C>Uncommon " + Pct(rates[Rarity.Uncommon]) +
                        "</color> · <color=#4FA3FF>Rare " + Pct(rates[Rarity.Rare]) + "</color>", s.body);
        GUILayout.Label("You own one of each Minion: duplicates become Minion Essence (" +
                        game.Database.Summoning.GetEssence(Rarity.Common) + " / " +
                        game.Database.Summoning.GetEssence(Rarity.Uncommon) + " / " +
                        game.Database.Summoning.GetEssence(Rarity.Rare) + " by rarity). No pity.", s.small);

        SummonResult check = game.Summoning.Check(pool, shard, game.Wallet);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (check.Status == SummonStatus.CantAfford)
        {
            GUILayout.Label("Need " + Describe(check.Shortfall) + " more", s.smallRed);
            GUILayout.Label(summonAdvanced ? "Advanced Shards: each boss's first win." : "Basic Shards: boss wins and Expedition first clears.", s.small);
        }
        else if (check.Status == SummonStatus.EmptyPool)
        {
            GUILayout.Label("This shard can't give a Minion from this pool.", s.smallRed);
        }
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        if (GoldButton("Summon x1 · " + check.Cost.amount, 380f, check.Ok))
        {
            SummonResult result = game.Summon(pool, shard);
            if (!result.Ok) Say("Can't summon: " + result.Status + ".", Mood.Bad);
            else if (result.IsDuplicate) Say(result.Definition.Rarity + " " + result.Definition.DisplayName + " again: +" + result.Essence + " Minion Essence.", Mood.Neutral);
            else
            {
                Say("NEW! " + result.Definition.DisplayName + " (" + result.Definition.Rarity + ") joined " +
                    (game.Roster.IsAssigned(result.Added.instanceId) ? (showMiners ? "the Mine." : "the team.") : "the bench."), Mood.Good);
                selectedMinionId = result.Added.instanceId;
            }
            Exit();
        }
        GUILayout.EndHorizontal();
        EndCard();
    }

    static string Pct(double fraction) =>
        (fraction * 100.0).ToString(fraction * 100.0 % 1.0 == 0.0 ? "0" : "0.#", CultureInfo.InvariantCulture) + "%";

    // ================================================================ Smith

    // ================================================================ Playtest fixes (SCRUM-32)

    /// <summary>Remembers a lost fight for the hint card; a win clears it.</summary>
    void RememberFight(bool won, string name, float power, float recommended)
    {
        if (won)
        {
            lossName = "";
            return;
        }
        lossName = name;
        lossPower = power;
        lossRecommended = recommended;
    }

    /// <summary>
    /// After a lost fight: how far the team is from the recommended power, and where to get stronger.
    /// Stays until the next win or until the player closes it.
    /// </summary>
    void DrawLossHint(GameSession game)
    {
        if (string.IsNullOrEmpty(lossName)) return;
        float now = game.Bosses.GetTeamPower();
        BeginCard();
        CardHeader("LOST TO " + lossName.ToUpperInvariant(), "Free retries", s.pillRed);
        if (now < lossRecommended)
        {
            GUILayout.Label("Your team has <b>" + Format(now) + "</b> power; " + lossName + " recommends <b>" + Format(lossRecommended) +
                            "</b>. You need about <color=#FF5A4E>" + Format(Math.Ceiling(lossRecommended - now)) + " more</color>.", s.body);
        }
        else
        {
            GUILayout.Label("Your team has <b>" + Format(now) + "</b> power, enough for the recommended " + Format(lossRecommended) +
                            ". Fights have some luck: try again, or bring a Support and a Tank.", s.body);
        }
        GUILayout.Label(CanAffordAnyTeamUpgrade(game)
            ? "<color=#5FD88C>You can afford upgrades right now.</color>"
            : "Level up your team and upgrade their gear. Collect the Mine first if you're short.", s.small);
        GUILayout.BeginHorizontal();
        if (GoldButton("Level up", 280f))
        {
            game.LogView("loss_hint Minions");
            tab = Tab.Minions; showMiners = false; scroll = Vector2.zero; Exit();
        }
        if (GoldButton("Upgrade gear", 300f))
        {
            game.LogView("loss_hint Smith");
            tab = Tab.Smith; showMiners = false; scroll = Vector2.zero; Exit();
        }
        GUILayout.FlexibleSpace();
        if (DarkButton("Close")) { lossName = ""; Exit(); }
        GUILayout.EndHorizontal();
        EndCard();
    }

    static bool CanAffordAnyTeamUpgrade(GameSession game)
    {
        foreach (OwnedMinion m in game.Roster.GetTeam())
        {
            if (game.Minions.CheckLevelUp(m.instanceId, game.Wallet).CanDo) return true;
            if (game.Smith.CheckUpgrade(m.instanceId, EquipmentSlot.Weapon, game.Wallet).CanDo) return true;
            if (game.Smith.CheckUpgrade(m.instanceId, EquipmentSlot.Armor, game.Wallet).CanDo) return true;
        }
        return false;
    }

    /// <summary>After a won first clear: offer to start automating it straight away.</summary>
    void DrawAutomatePrompt(GameSession game)
    {
        if (string.IsNullOrEmpty(automatePromptId)) return;
        ExpeditionDefinition e = game.Database.FindExpedition(automatePromptId);
        ExpeditionDefinition running = game.Expeditions.Active;
        if (e == null || running == e)
        {
            automatePromptId = "";
            return;
        }
        BeginCard();
        CardHeader(e.displayName.ToUpperInvariant() + " CLEARED", "Automate it", s.pillPurple);
        GUILayout.Label("Automate it now: it earns <b>" + DescribeRates(MiningMath.ToRates(e.automatedPerHour)) +
                        "</b> per hour, never fails and keeps going while the phone is locked.", s.body);
        GUILayout.Label(running != null
            ? "This stops " + running.displayName + " and collects its rewards first."
            : "Your Adventure team stays busy while it runs. Stop it any time to fight; the rewards are collected when you stop.", s.small);
        GUILayout.BeginHorizontal();
        if (GoldButton(running != null ? "Switch to this one" : "Start automation", 440f))
        {
            string prefix = "";
            if (running != null)
            {
                ExpeditionCollectResult stopped = game.StopExpedition();
                prefix = running.displayName + " stopped (+" + Describe(stopped.Collected) + "). ";
            }
            ExpeditionCheck started = game.StartExpedition(e.id);
            Say(started == ExpeditionCheck.Ok ? prefix + e.displayName + " is running. It keeps going while the app is closed."
                : prefix + Explain(started), started == ExpeditionCheck.Ok ? Mood.Good : Mood.Bad);
            automatePromptId = "";
            Exit();
        }
        GUILayout.FlexibleSpace();
        if (DarkButton("Not now")) { automatePromptId = ""; Exit(); }
        GUILayout.EndHorizontal();
        EndCard();
    }

    /// <summary>HQ: the Smith's rank and a shortcut to the Smith tab.</summary>
    void DrawSmithSummary(GameSession game)
    {
        SmithService smith = game.Smith;
        BeginCard();
        CardHeader("SMITH", "Rank " + smith.Rank + "/" + smith.MaxRank, s.pillGold);
        GUILayout.Label("Upgrade each Minion's gear with Gold and ores. Breakthroughs use rare Expedition materials.", s.body);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GoldButton("Go", 300f)) { tab = Tab.Smith; scroll = Vector2.zero; Exit(); }
        GUILayout.EndHorizontal();
        EndCard();
    }

    /// <summary>
    /// The Smith (SCRUM-9): learn ranks with boss materials, then upgrade and break through each
    /// Minion's own gear. Every row says what's missing and where to get it.
    /// </summary>
    void DrawSmithTab(GameSession game)
    {
        DrawSmithRankCard(game);

        MinionKind kind = showMiners ? MinionKind.Miner : MinionKind.Adventure;
        BeginCard();
        GUILayout.BeginHorizontal();
        GUILayout.Label(showMiners ? "MINER TOOLS" : "WEAPONS & ARMOR", s.title);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(showMiners ? "Adventure gear" : "Miner tools", s.pillButton))
        {
            showMiners = !showMiners;
            selectedMinionId = 0;
            Exit();
        }
        GUILayout.EndHorizontal();

        RosterService roster = game.Roster;
        var minions = new List<OwnedMinion>(showMiners ? roster.GetMiners() : roster.GetTeam());
        minions.AddRange(roster.GetUnassigned(kind));
        if (minions.Count == 0) GUILayout.Label("No Minions of this kind yet.", s.body);
        foreach (OwnedMinion m in minions)
        {
            MinionDefinition d = game.Database.FindMinion(m.definitionId);
            if (d == null) continue;
            GUILayout.Space(10f);
            GUILayout.Label("<b>" + d.DisplayName + "</b> <color=#A59C8C>· " + (roster.IsAssigned(m.instanceId) ? (showMiners ? "Mining" : "Team") : "Bench") + "</color>", s.body);
            foreach (EquipmentSlot slot in EquipmentRules.SlotsFor(d.Kind)) GearRow(game, m, d, slot);
        }
        EndCard();
    }

    void DrawSmithRankCard(GameSession game)
    {
        SmithService smith = game.Smith;
        BeginCard();
        CardHeader("SMITH", "Rank " + smith.Rank + "/" + smith.MaxRank, s.pillGold);
        SmithCheck check = smith.CheckRankUp(game.Wallet);
        if (check.Status == SmithStatus.NoMore)
        {
            GUILayout.Label("The Smith knows every technique. All gear breakthroughs are open.", s.body);
            EndCard();
            return;
        }

        SmithRank next = smith.NextRank;
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label("<b>" + next.displayName + "</b> <color=#A59C8C>· opens gear breakthrough " + (smith.Rank + 1) + "</color>", s.body);
        GUILayout.Label("Costs " + Describe(check.Cost) + " (once)", s.small);
        if (check.Status == SmithStatus.CantAfford) Missing(game, check.Shortfall);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        if (GoldButton("Learn", 300f, check.CanDo))
        {
            SmithCheck result = game.UpgradeSmith();
            if (result.CanDo) Say("The Smith learned " + next.displayName + "!", Mood.Good);
            else Say(Explain(result.Status), Mood.Bad);
            Exit();
        }
        GUILayout.EndHorizontal();
        EndCard();
    }

    void GearRow(GameSession game, OwnedMinion m, MinionDefinition d, EquipmentSlot slot)
    {
        SmithService smith = game.Smith;
        EquipmentRules gear = game.Database.Equipment;
        EquipmentSlotRules rules = gear.Get(slot);
        int level = EquipmentRules.GetLevel(m, slot);
        int max = gear.GetMaxLevel(m, slot);
        string effect = slot == EquipmentSlot.Tool ? "mining" : slot == EquipmentSlot.Weapon ? "ATK" : "HP";

        SmithCheck upgrade = smith.CheckUpgrade(m.instanceId, slot, game.Wallet);
        bool atCap = upgrade.Status == SmithStatus.AtLevelCap;
        SmithCheck check = atCap ? smith.CheckBreakthrough(m.instanceId, slot, game.Wallet) : upgrade;

        GUILayout.BeginHorizontal(s.row);
        GUILayout.BeginVertical();
        string bonus = "+" + Percent(rules.GetBonus(level)) +
                       (!atCap ? " <color=#5FD88C>→ +" + Percent(rules.GetBonus(level + 1)) + "</color>" : "");
        GUILayout.Label(rules.displayName + " Lv " + level + "/" + max + " <color=#A59C8C>·</color> " + bonus + " " + effect, s.body);
        if (atCap && check.Status == SmithStatus.NoMore)
        {
            GUILayout.Label("Fully upgraded.", s.small);
        }
        else
        {
            GUILayout.Label((atCap ? "Breakthrough: " : "Upgrade: ") + Describe(check.Cost), s.small);
            if (check.Status == SmithStatus.SmithRankTooLow)
            {
                SmithRank rank = gear.GetSmithRank(check.RequiredRank);
                GUILayout.Label("The Smith must learn " + (rank != null ? rank.displayName : "rank " + check.RequiredRank) + " first.", s.smallRed);
            }
            if (check.Shortfall.Count > 0) Missing(game, check.Shortfall);
        }
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        BulkPurchase gearMax = atCap ? BulkPurchase.None : smith.CheckUpgradeMax(m.instanceId, slot, game.Wallet);
        if (gearMax.Levels >= 2 && GoldButton("Max ×" + gearMax.Levels, 200f))
        {
            BulkPurchase bought = game.UpgradeGearMax(m.instanceId, slot);
            Say(d.DisplayName + "'s " + rules.displayName + " +" + bought.Levels + " levels, now level " + bought.ToLevel + ".", Mood.Good);
            Exit();
        }
        if (check.Status != SmithStatus.NoMore && SmallGoldButton(atCap ? "Break" : "Upgrade", check.CanDo))
        {
            SmithCheck result = atCap ? game.BreakthroughGear(m.instanceId, slot) : game.UpgradeGear(m.instanceId, slot);
            if (result.CanDo)
            {
                Say(atCap ? d.DisplayName + "'s " + rules.displayName + " broke through! Cap " + result.MaxLevel + "."
                          : d.DisplayName + "'s " + rules.displayName + " is now level " + result.Level + ".", Mood.Good);
            }
            else Say(Explain(result.Status), Mood.Bad);
            Exit();
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>"Need 50 Iron more", then where each missing resource comes from (SCRUM-9).</summary>
    void Missing(GameSession game, IReadOnlyList<ResourceAmount> shortfall)
    {
        if (shortfall == null || shortfall.Count == 0) return;
        GUILayout.Label("Need " + Describe(shortfall) + " more", s.smallRed);
        var hints = new List<string>();
        foreach (ResourceAmount a in shortfall) hints.Add(Name(a.id) + ": " + WhereToGet(game, a.id));
        GUILayout.Label(string.Join(" · ", hints), s.small);
    }

    static string WhereToGet(GameSession game, ResourceId id)
    {
        // Rare Expedition and boss materials: a hint that follows the player's progress (SCRUM-33).
        string rare = SourceHints.ForRareMaterial(game, id);
        if (rare != null) return rare;
        ResourceSource source = ResourceSources.Find(game.Database, id);
        switch (source.Kind)
        {
            case ResourceSourceKind.Mine:
                return game.Tiers.HighestUnlockedTier >= source.Tier ? "Mine" : "Mine from " + TierName(source.Tier);
            case ResourceSourceKind.AutomatedExpedition:
                return "automate " + source.DisplayName;
            case ResourceSourceKind.Boss:
                return "beat " + source.DisplayName;
            default:
                return "Summoning and first clears";
        }
    }

    // ================================================================ Boss

    void DrawBossSummary(GameSession game)
    {
        BossDefinition boss = game.Bosses.CurrentBoss;
        if (boss == null) return;
        float power = game.Bosses.GetTeamPower();
        BeginCard();
        CardHeader("BOSS · " + boss.displayName.ToUpperInvariant(), "Free · unlimited tries", s.pill);
        GUILayout.Label(TeamVsRecommended(power, boss.recommendedPower), s.body);
        GUILayout.BeginHorizontal();
        GUILayout.Label(game.Bosses.IsDefeated(boss.id) ? "Beaten · fight again for its material" : "Win: unique boss material + new Expeditions", s.small);
        GUILayout.FlexibleSpace();
        if (GoldButton("Go", 300f)) { tab = Tab.Boss; scroll = Vector2.zero; Exit(); }
        GUILayout.EndHorizontal();
        EndCard();
    }

    void DrawBossTab(GameSession game)
    {
        BossDefinition boss = game.Bosses.CurrentBoss;
        if (boss == null)
        {
            BeginCard();
            GUILayout.Label("No boss in the content.", s.body);
            EndCard();
            return;
        }

        DrawLossHint(game);

        // Art placeholder (SCRUM-13).
        Rect art = GUILayoutUtility.GetRect(100f, 380f, GUILayout.ExpandWidth(true), GUILayout.Height(380f));
        GUI.Box(art, GUIContent.none, s.artCard);
        GUI.Label(new Rect(art.x, art.yMax - 80f, art.width, 60f), "[ " + boss.displayName + " art ]", s.artLabel);
        GUILayout.Space(12f);

        float power = game.Bosses.GetTeamPower();
        bool busy = game.Roster.IsTeamBusy;
        BeginCard();
        CardHeader("BOSS · " + boss.displayName.ToUpperInvariant(), "Free · unlimited tries", s.pill);
        GUILayout.Label(TeamVsRecommended(power, boss.recommendedPower), s.body);
        GUILayout.Label("Win: unique boss material + new Expeditions", s.small);
        GUILayout.BeginHorizontal();
        BossCheck check = game.Bosses.CanFight(boss.id);
        GUILayout.Label(busy ? "Team on Expedition" : check == BossCheck.Ok ? "Ready" : Explain(check), s.small);
        GUILayout.FlexibleSpace();
        string label = busy ? "Stop Exp. & Fight" : game.Bosses.IsDefeated(boss.id) ? "Fight again" : "Fight";
        bool canPress = busy || check == BossCheck.Ok;
        if (GoldButton(label, 420f, canPress))
        {
            string prefix = "";
            if (busy)
            {
                ExpeditionCollectResult stopped = game.StopExpedition();
                prefix = "Expedition stopped (+" + Describe(stopped.Collected) + "). ";
            }
            float powerBefore = game.Bosses.GetTeamPower();
            BossFightResult result = game.FightBoss(boss.id);
            Say(prefix + DescribeFight(game, boss, result), result.Won ? Mood.Good : Mood.Bad);
            if (result.Check == BossCheck.Ok) RememberFight(result.Won, boss.displayName, powerBefore, boss.recommendedPower);
            Exit();
        }
        GUILayout.EndHorizontal();
        EndCard();

        DrawTeamSlotsCard(game, "YOUR TEAM", null);
        BeginCard();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Recommended", s.body);
        GUILayout.FlexibleSpace();
        GUILayout.Label(Format(boss.recommendedPower), s.bodyRight);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Your team", s.body);
        GUILayout.FlexibleSpace();
        GUILayout.Label(power < boss.recommendedPower ? "<color=#FF5A4E>" + Format(power) + " ▲</color>" : "<color=#5FD88C>" + Format(power) + "</color>", s.bodyRight);
        GUILayout.EndHorizontal();
        GUILayout.Label("Unlocks: " + DescribeUnlocks(game, boss), s.small);
        EndCard();

        DrawOtherBosses(game, boss);
    }

    void DrawOtherBosses(GameSession game, BossDefinition current)
    {
        bool any = false;
        foreach (BossDefinition b in game.Database.Bosses) if (b != null && b != current) any = true;
        if (!any) return;

        BeginCard();
        CardHeader("OTHER BOSSES", null, null);
        foreach (BossDefinition b in game.Database.Bosses)
        {
            if (b == null || b == current) continue;
            BossCheck check = game.Bosses.CanFight(b.id);
            bool beaten = game.Bosses.IsDefeated(b.id);
            GUILayout.BeginHorizontal(s.row);
            string state = beaten ? "<color=#5FD88C>beaten</color>" : check == BossCheck.TierLocked ? "reach " + TierName(b.tier) : "Rec. " + Format(b.recommendedPower);
            GUILayout.Label("<b>" + b.displayName + "</b> <color=#A59C8C>· Tier " + b.tier + " ·</color> " + state, s.body,
                GUILayout.ExpandWidth(true));
            if (SmallGoldButton(beaten ? "Fight again" : "Fight", check == BossCheck.Ok))
            {
                float powerBefore = game.Bosses.GetTeamPower();
                BossFightResult result = game.FightBoss(b.id);
                Say(DescribeFight(game, b, result), result.Won ? Mood.Good : Mood.Bad);
                if (result.Check == BossCheck.Ok) RememberFight(result.Won, b.displayName, powerBefore, b.recommendedPower);
                Exit();
            }
            GUILayout.EndHorizontal();
        }
        EndCard();
    }

    void DrawTeamSlotsCard(GameSession game, string title, string footer)
    {
        RosterService roster = game.Roster;
        BeginCard();
        CardHeader(title, roster.IsTeamBusy ? "Busy" : "Ready", roster.IsTeamBusy ? s.pillPurple : s.pillGreen);
        GUILayout.BeginHorizontal();
        List<OwnedMinion> team = roster.GetTeam();
        for (int i = 0; i < roster.AdventureSlots; i++)
        {
            Rect slot = GUILayoutUtility.GetRect(120f, 120f, GUILayout.Width(120f), GUILayout.Height(120f));
            if (i < team.Count)
            {
                MinionDefinition d = game.Database.FindMinion(team[i].definitionId);
                GUI.Box(slot, GUIContent.none, s.slotOn);
                DrawTinted(new Rect(slot.x + 10f, slot.y + 10f, slot.width - 20f, slot.height - 20f), s.roundedWhite,
                    (d != null ? ClassColor(d) : Muted) * 0.5f + new Color(0, 0, 0, 0.5f));
                GUI.Label(slot, roster.IsTeamBusy ? "BUSY" : d != null ? d.Role.ToString().Substring(0, 1) : "?",
                    roster.IsTeamBusy ? s.slotBusy : s.slotLetter);
            }
            else
            {
                GUI.Box(slot, GUIContent.none, s.slot);
                GUI.Label(slot, "+", s.slotLetter);
                if (GUI.Button(slot, GUIContent.none, GUIStyle.none)) { tab = Tab.Minions; showMiners = false; Exit(); }
            }
            GUILayout.Space(16f);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label("Power " + Format(game.Bosses.GetTeamPower()) + (footer != null ? " · " + footer : ""), s.small);
        EndCard();
    }

    // ================================================================ Overlays

    Rect GetOverlayRect(Rect topBar)
    {
        float w = Mathf.Min(680f, topBar.width - 2 * Side);
        float h = overlay == Overlay.Settings ? 1080f : 1000f;
        return new Rect(topBar.xMax - Side - w, topBar.yMax - 10f, w, h);
    }

    void CloseOverlayOnOutsideTap(Rect overlayRect)
    {
        Event e = Event.current;
        if (overlay == Overlay.None || e.type != EventType.MouseDown) return;
        if (overlayRect.Contains(e.mousePosition)) return;
        overlay = Overlay.None;
        resetArmed = false;
        e.Use();
    }

    void DrawResourcePanel(GameSession game, Rect r)
    {
        GUILayout.BeginArea(r, s.panel);
        PanelGroup(game, "Mine", ResourceId.Gold, ResourceId.Iron, ResourceId.Silver, ResourceId.Mithril);
        PanelGroup(game, "Shards", ResourceId.BasicShard, ResourceId.AdvancedShard);
        PanelGroup(game, "Expedition materials", ResourceId.SporeSilk, ResourceId.EmberCore, ResourceId.PrismDust);
        PanelGroup(game, "Boss materials", ResourceId.ForemanSigil, ResourceId.GolemHeart, ResourceId.WyrmScale);
        PanelGroup(game, "Other", ResourceId.MinionEssence);
        GUILayout.Space(8f);
        GUILayout.Label("Expedition materials pay for breakthroughs; boss materials teach the Smith.", s.small);
        GUILayout.EndArea();
    }

    void PanelGroup(GameSession game, string title, params ResourceId[] ids)
    {
        GUILayout.Label(title, s.panelHeading);
        foreach (ResourceId id in ids)
        {
            GUILayout.BeginHorizontal();
            Rect icon = GUILayoutUtility.GetRect(40f, 40f, GUILayout.Width(40f), GUILayout.Height(40f));
            DrawIcon(new Rect(icon.x, icon.y + 4f, 36f, 36f), id);
            GUILayout.Space(10f);
            GUILayout.Label(Name(id), s.panelText);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Format(game.Wallet.Get(id)), s.panelText);
            GUILayout.EndHorizontal();
            // Where each rare material comes from, right under it (SCRUM-33).
            string hint = SourceHints.ForRareMaterial(game, id);
            if (hint != null) GUILayout.Label("From: " + hint, s.small);
        }
    }

    void DrawSettingsPanel(GameSession game, Rect r)
    {
        GameSessionRunner runner = GameSessionRunner.Instance;
        GUILayout.BeginArea(r, s.panel);
        GUILayout.Label("Settings", s.title);
        if (ui != null && DarkButton("Sound")) { overlay = Overlay.None; ui.ShowSettings(); Exit(); }

        GUILayout.Space(16f);
        GUILayout.Label("Debug (playtest only)", s.panelHeading);
        GUILayout.Label("Clock skipped ahead: " + Hours(runner.Clock.Offset.TotalHours) +
                        (game.SavingAllowed ? "" : "\nSaving is OFF: the save on this device is from a newer build."), s.small);
        GUILayout.BeginHorizontal();
        if (DarkButton("+1 h")) { Skip(runner, 1); Exit(); }
        if (DarkButton("+8 h")) { Skip(runner, 8); Exit(); }
        if (DarkButton("+24 h")) { Skip(runner, 24); Exit(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(16f);
        GUILayout.Label("Playtest log (SCRUM-8): " + game.Playtest.Events.Count + " events on this device", s.small);
        GUILayout.BeginHorizontal();
        if (DarkButton("Copy log"))
        {
            GUIUtility.systemCopyBuffer = game.Playtest.ToCsv();
            playtestNote = "Copied " + game.Playtest.Events.Count + " events to the clipboard.";
            Exit();
        }
        if (DarkButton("Save log file"))
        {
            playtestNote = SavePlaytestLog(game);
            Exit();
        }
        GUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(playtestNote)) GUILayout.Label(playtestNote, s.small);

        // Web playtest (SCRUM-35): the log is sent to the website automatically.
        PlaytestUploader uploader = PlaytestUploader.Instance;
        if (uploader != null && uploader.Active)
        {
            string last = uploader.LastUploadUtc.HasValue ? uploader.LastUploadUtc.Value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) : "not yet";
            GUILayout.Label("Sent to the website automatically as <b>" + uploader.Code + "</b> · last sent " + last +
                            " · " + uploader.PendingCount + " waiting" +
                            (string.IsNullOrEmpty(uploader.LastError) ? "" : "\n<color=#FF5A4E>Last try failed: " + uploader.LastError + "</color>"), s.small);
            if (DarkButton("Send now")) { uploader.UploadNow(); Exit(); }
        }

        GUILayout.Space(16f);
        if (DarkButton(resetArmed ? "Tap again to DELETE the save and its playtest log" : "Reset game (copy the log first)"))
        {
            if (!resetArmed)
            {
                resetArmed = true;
                Exit();
                return;
            }
            resetArmed = false;
            overlay = Overlay.None;
            runner.DebugResetGame();
            Say("New game started.", Mood.Neutral);
            Exit();
        }
        GUILayout.EndArea();
    }

    /// <summary>Writes the playtest log as CSV next to the save and returns where it went.</summary>
    static string SavePlaytestLog(GameSession game)
    {
        try
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath,
                "playtest_log_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
            System.IO.File.WriteAllText(path, game.Playtest.ToCsv());
            return "Saved to " + path;
        }
        catch (Exception e)
        {
            return "Could not save the log: " + e.Message;
        }
    }

    void Skip(GameSessionRunner runner, int hours)
    {
        runner.DebugSkipTime(TimeSpan.FromHours(hours));
        Say("Skipped " + hours + " h. Check the Mine and the Expedition.", Mood.Neutral);
    }

    // ================================================================ Widgets

    /// <summary>
    /// Ends this GUI pass right after a tap changed the game. Without it, IMGUI would keep
    /// drawing a different number of controls than it laid out and log layout errors.
    /// </summary>
    static void Exit() => GUIUtility.ExitGUI();

    void Say(string text, Mood mood)
    {
        message = text ?? "";
        messageMood = mood;
    }

    void BeginCard()
    {
        GUILayout.BeginVertical(s.card);
    }

    void EndCard()
    {
        GUILayout.EndVertical();
        if (Event.current.type == EventType.Repaint)
        {
            // Remember the inner width so the Minion grid can size its columns.
            Rect last = GUILayoutUtility.GetLastRect();
            s.lastCardInnerWidth = last.width - s.card.padding.horizontal;
        }
        GUILayout.Space(14f);
    }

    void CardHeader(string title, string pill, GUIStyle pillStyle)
    {
        GUILayout.BeginHorizontal();
        Rect icon = GUILayoutUtility.GetRect(34f, 34f, GUILayout.Width(34f), GUILayout.Height(34f));
        DrawTinted(new Rect(icon.x, icon.y + 10f, 30f, 30f), s.diamond, Gold);
        GUILayout.Space(12f);
        GUILayout.Label(title, s.title);
        GUILayout.FlexibleSpace();
        if (!string.IsNullOrEmpty(pill)) GUILayout.Label(pill, pillStyle ?? s.pill, GUILayout.ExpandWidth(false));
        GUILayout.EndHorizontal();
        GUILayout.Space(8f);
    }

    bool GoldButton(string text, float width, bool enabled = true)
    {
        bool was = GUI.enabled;
        GUI.enabled = was && enabled;
        bool clicked = GUILayout.Button(text, s.goldButton, GUILayout.Width(width));
        GUI.enabled = was;
        return clicked;
    }

    bool SmallGoldButton(string text, bool enabled)
    {
        bool was = GUI.enabled;
        GUI.enabled = was && enabled;
        bool clicked = GUILayout.Button(text, s.goldButton, GUILayout.Width(280f));
        GUI.enabled = was;
        return clicked;
    }

    bool DarkButton(string text) => GUILayout.Button(text, s.darkButton);

    void ResourceLine(List<KeyValuePair<ResourceId, double>> items, string prefix)
    {
        GUILayout.BeginHorizontal();
        foreach (KeyValuePair<ResourceId, double> item in items)
        {
            Rect icon = GUILayoutUtility.GetRect(44f, 52f, GUILayout.Width(44f), GUILayout.Height(52f));
            DrawIcon(new Rect(icon.x, icon.y + 6f, 42f, 42f), item.Key);
            GUILayout.Label(prefix + Short(Math.Floor(item.Value)), s.chipInline);
            GUILayout.Space(18f);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    void DrawIcon(Rect r, ResourceId id)
    {
        IconInfo info = Icon(id);
        DrawTinted(r, info.Diamond ? s.diamond : s.circle, info.Color);
        if (!info.Diamond) GUI.Label(r, info.Letters, s.iconLetter);
    }

    static void DrawTinted(Rect r, Texture2D texture, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color old = GUI.color;
        GUI.color = GUI.enabled ? color : new Color(color.r, color.g, color.b, color.a * 0.5f);
        GUI.DrawTexture(r, texture);
        GUI.color = old;
    }

    // ================================================================ Text

    static string DescribeNextGoal(GameSession game)
    {
        TierUnlockCheck check = game.Tiers.CheckNext(game.Wallet);
        switch (check.Status)
        {
            case TierUnlockStatus.BossNotDefeated: return "Next: beat " + BossNames(game, check.MissingBosses);
            case TierUnlockStatus.ExpeditionNotCleared: return "Next: clear " + ExpeditionNames(game, check.MissingFirstClears);
            case TierUnlockStatus.CantAfford: return "Next: gather for " + TierName(check.Tier);
            case TierUnlockStatus.Ok: return "Next: unlock " + TierName(check.Tier);
            default: return "Deepest tier reached";
        }
    }

    static string TeamVsRecommended(float power, float recommended)
    {
        string team = power < recommended ? "<color=#FF5A4E>" + Format(power) + " ▲</color>" : "<color=#5FD88C>" + Format(power) + "</color>";
        return "Team " + team + " / Rec. " + Format(recommended);
    }

    static string DescribeUnlocks(GameSession game, BossDefinition boss)
    {
        var parts = new List<string>();
        foreach (ExpeditionDefinition e in game.Database.Expeditions)
        {
            if (e != null && e.unlockedByBoss == boss.id) parts.Add(e.displayName + " Expedition");
        }
        TierRequirement next = game.Database.FindTierRequirement(boss.tier + 1);
        if (next != null && next.requiredBosses != null && next.requiredBosses.Contains(boss.id)) parts.Add(TierName(boss.tier + 1));
        return parts.Count > 0 ? string.Join(" · ", parts) : "its boss material";
    }

    static string DescribeWelcome(GameSession session)
    {
        OfflineReport offline = session.Offline;
        if (offline == null || offline.HoursAway < 1.0 / 60.0) return "";
        string text = "Welcome back! You were away " + Hours(offline.HoursAway) + ". Mine: " +
                      Describe(offline.Mine) + " waiting.";
        // The Expedition's rare materials aren't counted up here (team decision 2026-11-24).
        ExpeditionDefinition running = session.Expeditions.Active;
        if (running != null) text += " " + running.displayName + " is still farming; stop it to collect.";
        return text + "  (tap to close)";
    }

    string DescribeFight(GameSession game, BossDefinition boss, BossFightResult result)
    {
        if (result.Check != BossCheck.Ok) return Explain(result.Check);
        if (!result.Won) return "Lost to " + boss.displayName + " after " + Format(result.Battle.DurationSeconds) + " s. " + result.DescribeLoss();

        string text = "Beat " + boss.displayName + " in " + Format(result.Battle.DurationSeconds) + " s! +" + Describe(result.Rewards) + ".";
        if (result.UnlockedExpeditions.Count > 0) text += " New Expedition: " + ExpeditionNames(game, result.UnlockedExpeditions) + ".";
        foreach (int tier in result.UnlockedTiers) text += " " + TierName(tier) + " unlocked!";
        return text;
    }

    static string DescribeFirstClear(ExpeditionDefinition e, FirstClearResult result)
    {
        if (result.Check != ExpeditionCheck.Ok) return Explain(result.Check);
        if (!result.Won) return "First clear of " + e.displayName + " failed. " + result.Battle.DescribeLoss("Enemy");
        string text = "Cleared " + e.displayName + "! +" + Describe(result.Rewards) + ". You can automate it now.";
        foreach (int tier in result.UnlockedTiers) text += " " + TierName(tier) + " unlocked!";
        return text;
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

    static string Explain(MinionUpgradeStatus status)
    {
        switch (status)
        {
            case MinionUpgradeStatus.AtLevelCap: return "At the level cap. A breakthrough raises it.";
            case MinionUpgradeStatus.BelowLevelCap: return "Reach the level cap before the breakthrough.";
            case MinionUpgradeStatus.NoMoreBreakthroughs: return "Every breakthrough is done.";
            case MinionUpgradeStatus.CantAfford: return "Not enough resources yet.";
            case MinionUpgradeStatus.NotFound: return "That Minion is gone.";
            default: return "Can't do that: " + status + ".";
        }
    }

    static string Explain(SmithStatus status)
    {
        switch (status)
        {
            case SmithStatus.AtLevelCap: return "At the level cap. A breakthrough raises it.";
            case SmithStatus.BelowLevelCap: return "Reach the level cap before the breakthrough.";
            case SmithStatus.SmithRankTooLow: return "The Smith must learn the next technique first (boss material).";
            case SmithStatus.NoMore: return "Nothing more to upgrade here.";
            case SmithStatus.CantAfford: return "Not enough resources yet.";
            case SmithStatus.WrongSlot: return "That Minion can't use this gear.";
            case SmithStatus.NotFound: return "That Minion is gone.";
            default: return "Can't do that: " + status + ".";
        }
    }

    static string Explain(AssignResult result)
    {
        switch (result)
        {
            case AssignResult.NoFreeSlot: return "All slots are full. Take someone out first.";
            case AssignResult.TeamBusy: return "The team is on an Expedition. Stop it to change the team.";
            case AssignResult.WrongKind: return "Miners only mine; Adventure Minions only fight.";
            default: return "Can't do that: " + result + ".";
        }
    }

    static string DescribeRates(List<MinedAmount> rates)
    {
        var parts = new List<string>();
        foreach (MinedAmount r in rates)
        {
            if (r.amount > 0) parts.Add(r.amount.ToString("0.#", CultureInfo.InvariantCulture) + " " + Name(r.id));
        }
        return parts.Count > 0 ? string.Join(" · ", parts) : "nothing";
    }

    static string Describe(IEnumerable<ResourceAmount> amounts)
    {
        var parts = new List<string>();
        foreach (ResourceAmount a in amounts) if (a.amount > 0) parts.Add(Format(a.amount) + " " + Name(a.id));
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    static string Describe(IEnumerable<MinedAmount> amounts)
    {
        var parts = new List<string>();
        foreach (MinedAmount a in amounts) if (a.amount >= 1.0) parts.Add(Format(Math.Floor(a.amount)) + " " + Name(a.id));
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    static string BossNames(GameSession game, IEnumerable<string> ids)
    {
        var names = new List<string>();
        foreach (string id in ids)
        {
            BossDefinition b = game.Database.FindBoss(id);
            names.Add(b != null ? b.displayName : id);
        }
        return string.Join(", ", names);
    }

    static string ExpeditionNames(GameSession game, IEnumerable<string> ids)
    {
        var names = new List<string>();
        foreach (string id in ids)
        {
            ExpeditionDefinition e = game.Database.FindExpedition(id);
            names.Add(e != null ? e.displayName : id);
        }
        return string.Join(", ", names);
    }

    /// <summary>Placeholder tier names (SCRUM-13), from the UI mock-up.</summary>
    static string TierName(int tier)
    {
        switch (tier)
        {
            case 1: return "Gold Shallows";
            case 2: return "Iron Seams";
            case 3: return "Silver Veins";
            case 4: return "Silver Depths";
            case 5: return "Mithril Deeps";
            case 6: return "Mithril Core";
            default: return "Tier " + tier;
        }
    }

    static string Format(double value) => Math.Floor(value).ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>1,234 → 1.2K, as in the mock-up's resource bar.</summary>
    static string Short(double value)
    {
        double v = Math.Floor(value);
        if (v >= 1_000_000) return (v / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        if (v >= 1_000) return (v / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        return v.ToString("0", CultureInfo.InvariantCulture);
    }

    static string Hours(double hours)
    {
        if (hours < 1.0) return Math.Floor(hours * 60.0).ToString(CultureInfo.InvariantCulture) + " min";
        return hours.ToString("0.#", CultureInfo.InvariantCulture) + " h";
    }

    /// <summary>First letter of the last word: "Common Tank" → "T".</summary>
    static string Initial(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        string[] words = name.Trim().Split(' ');
        return words[words.Length - 1].Substring(0, 1).ToUpperInvariant();
    }

    static KeyValuePair<ResourceId, double> Pair(ResourceId id, double amount) => new KeyValuePair<ResourceId, double>(id, amount);

    static string Name(ResourceId id)
    {
        switch (id)
        {
            case ResourceId.BasicShard: return "Basic Shards";
            case ResourceId.AdvancedShard: return "Advanced Shards";
            case ResourceId.MinionEssence: return "Minion Essence";
            case ResourceId.SporeSilk: return "Spore Silk";
            case ResourceId.EmberCore: return "Ember Core";
            case ResourceId.PrismDust: return "Prism Dust";
            case ResourceId.ForemanSigil: return "Foreman Sigil";
            case ResourceId.GolemHeart: return "Golem Heart";
            case ResourceId.WyrmScale: return "Wyrm Scale";
            default: return id.ToString();
        }
    }

    readonly struct IconInfo
    {
        public IconInfo(Color color, string letters, bool diamond)
        {
            Color = color;
            Letters = letters;
            Diamond = diamond;
        }

        public Color Color { get; }
        public string Letters { get; }
        public bool Diamond { get; }
    }

    static IconInfo Icon(ResourceId id)
    {
        switch (id)
        {
            case ResourceId.Gold: return new IconInfo(Gold, "G", false);
            case ResourceId.Iron: return new IconInfo(Hex("8C96A3"), "Fe", false);
            case ResourceId.Silver: return new IconInfo(Hex("C9D1DA"), "Ag", false);
            case ResourceId.Mithril: return new IconInfo(Hex("4FC3A1"), "Mi", false);
            case ResourceId.BasicShard: return new IconInfo(Blue, "", true);
            case ResourceId.AdvancedShard: return new IconInfo(Purple, "", true);
            case ResourceId.MinionEssence: return new IconInfo(Hex("E07BE0"), "E", false);
            case ResourceId.SporeSilk: return new IconInfo(Hex("9B7BD8"), "Ss", false);
            case ResourceId.EmberCore: return new IconInfo(Hex("FF7A3D"), "Ec", false);
            case ResourceId.PrismDust: return new IconInfo(Hex("7FE0F0"), "Pd", false);
            case ResourceId.ForemanSigil: return new IconInfo(Hex("D9A441"), "Fs", false);
            case ResourceId.GolemHeart: return new IconInfo(Hex("E05A5A"), "Gh", false);
            case ResourceId.WyrmScale: return new IconInfo(Hex("6FB7FF"), "Ws", false);
            default: return new IconInfo(Muted, "?", false);
        }
    }

    static Color ClassColor(MinionDefinition d)
    {
        if (d.Kind == MinionKind.Miner) return Gold;
        switch (d.Role)
        {
            case MinionRole.Tank: return Hex("4A78C2");
            case MinionRole.Fighter: return Hex("D0564A");
            case MinionRole.Archer: return Hex("5AA04A");
            case MinionRole.Mage: return Purple;
            case MinionRole.Support: return Hex("3FA89A");
            default: return Muted;
        }
    }

    static Color Hex(string rgb)
    {
        ColorUtility.TryParseHtmlString("#" + rgb, out Color c);
        return c;
    }

    // ================================================================ Touch scrolling

    void HandleTouchScroll()
    {
        Event e = Event.current;
        if (overlay != Overlay.None) return;
        if (e.type == EventType.MouseDown)
        {
            pressStart = e.mousePosition;
            dragging = false;
        }
        else if (e.type == EventType.MouseDrag)
        {
            if (!dragging && (e.mousePosition - pressStart).magnitude > DragThreshold)
            {
                dragging = true;
                GUIUtility.hotControl = 0; // a drag is a scroll, not a tap on the button under the finger
            }
            if (dragging)
            {
                scroll.y = Mathf.Max(0f, scroll.y - e.delta.y);
                e.Use();
            }
        }
    }

    // ================================================================ Styles

    sealed class Styles
    {
        public float lastCardInnerWidth = 900f;

        public readonly Texture2D circle = Shapes.Rounded(Color.white, Color.white, 32, 0, 68);
        public readonly Texture2D diamond = Shapes.Diamond(64);
        public readonly Texture2D roundedWhite = Shapes.Rounded(Color.white, Color.white, 16, 0);
        readonly Dictionary<Color, Texture2D> solids = new Dictionary<Color, Texture2D>();

        public readonly GUIStyle card, artCard, row, panel, title, header, body, bodyRight, small, smallRed;
        public readonly GUIStyle pill, pillGreen, pillPurple, pillRed, pillGold, pillButton;
        public readonly GUIStyle goldButton, darkButton, iconButton, segment, segmentOn;
        public readonly GUIStyle chip, chipInline, iconLetter, nav, navSelected;
        public readonly GUIStyle minionCard, minionCardOn, minionName, minionSub, portraitLetter;
        public readonly GUIStyle tagGreen, tagPurple, tagMuted, slot, slotOn, slotLetter, slotBusy;
        public readonly GUIStyle toast, toastGood, toastBad, panelHeading, panelText, artLabel;

        public Styles()
        {
            card = Box(CardFill, Bronze, 20, 3, 28);
            artCard = Box(Hex("221A2E"), Bronze, 20, 3, 0);
            row = new GUIStyle { margin = new RectOffset(0, 0, 6, 6), padding = new RectOffset(0, 0, 4, 4) };
            panel = Box(Hex("17131D"), Bronze, 20, 3, 32);

            title = Font(36, Text, FontStyle.Bold);
            header = Font(36, Text, FontStyle.Bold);
            header.alignment = TextAnchor.MiddleLeft;
            body = Font(32, Text, FontStyle.Normal);
            body.wordWrap = true;
            bodyRight = Font(32, Text, FontStyle.Bold);
            bodyRight.alignment = TextAnchor.MiddleRight;
            small = Font(26, Muted, FontStyle.Normal);
            small.wordWrap = true;
            smallRed = Font(26, Red, FontStyle.Normal);

            pill = Pill(Hex("1A1620"), Hex("4A4050"), Text);
            pillGreen = Pill(Hex("12301F"), Hex("2F8F55"), Green);
            pillPurple = Pill(Hex("2A1838"), Hex("8A5CC7"), Purple);
            pillRed = Pill(Hex("3A1414"), Hex("A33A30"), Red);
            pillGold = Pill(Hex("3A2808"), Hex("B98522"), Gold);
            pillButton = Pill(Hex("1A1620"), Hex("4A4050"), Text);
            pillButton.fixedHeight = 0;
            pillButton.padding = new RectOffset(28, 28, 18, 18);
            pillButton.hover.background = Shapes.Rounded(Hex("2A2430"), Hex("6B5E70"), 22, 2);
            pillButton.hover.textColor = Text;

            goldButton = Button(Gold, Hex("FFD77A"), GoldDark, 34);
            darkButton = Button(Hex("241E2A"), Bronze, Text, 30);
            darkButton.margin = new RectOffset(6, 6, 8, 8);
            iconButton = Button(Hex("1A1620"), Bronze, Text, 30);
            iconButton.fixedHeight = 0;
            segment = Button(Hex("1A1620"), Hex("4A4050"), Text, 28);
            segment.fixedHeight = 96;
            segmentOn = Button(Hex("3A2808"), Gold, Gold, 28);
            segmentOn.fixedHeight = 96;

            chip = Font(32, Text, FontStyle.Bold);
            chip.alignment = TextAnchor.MiddleLeft;
            chipInline = Font(32, Text, FontStyle.Bold);
            chipInline.alignment = TextAnchor.MiddleLeft;
            chipInline.fixedHeight = 52;
            iconLetter = Font(18, GoldDark, FontStyle.Bold);
            iconLetter.alignment = TextAnchor.MiddleCenter;
            nav = Font(26, Muted, FontStyle.Normal);
            nav.alignment = TextAnchor.MiddleCenter;
            navSelected = Font(26, Gold, FontStyle.Bold);
            navSelected.alignment = TextAnchor.MiddleCenter;

            minionCard = Box(Hex("15121A"), Hex("4A4050"), 16, 3, 0);
            minionCardOn = Box(Hex("15121A"), Green, 16, 3, 0);
            minionName = Font(30, Text, FontStyle.Bold);
            minionName.clipping = TextClipping.Clip;
            minionSub = Font(24, Muted, FontStyle.Normal);
            portraitLetter = Font(110, new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold);
            portraitLetter.alignment = TextAnchor.MiddleCenter;
            tagGreen = Font(26, Green, FontStyle.Bold);
            tagPurple = Font(26, Purple, FontStyle.Bold);
            tagMuted = Font(26, Muted, FontStyle.Normal);

            slot = Box(Hex("15121A"), Hex("4A4050"), 14, 3, 0);
            slotOn = Box(Hex("15121A"), Green, 14, 3, 0);
            slotLetter = Font(44, Text, FontStyle.Bold);
            slotLetter.alignment = TextAnchor.MiddleCenter;
            slotBusy = Font(24, Purple, FontStyle.Bold);
            slotBusy.alignment = TextAnchor.MiddleCenter;

            toast = Box(Hex("221C28"), Hex("6B5E70"), 18, 3, 24);
            toastGood = Box(Hex("13261B"), Hex("2F8F55"), 18, 3, 24);
            toastBad = Box(Hex("2C1414"), Hex("A33A30"), 18, 3, 24);
            foreach (GUIStyle t in new[] { toast, toastGood, toastBad })
            {
                t.fontSize = 30;
                t.wordWrap = true;
                t.richText = true;
                t.alignment = TextAnchor.MiddleLeft;
                t.normal.textColor = Text;
                t.hover = t.normal;
                t.active = t.normal;
            }

            panelHeading = Font(28, Muted, FontStyle.Normal);
            panelHeading.margin = new RectOffset(0, 0, 18, 6);
            panelText = Font(30, Text, FontStyle.Normal);
            panelText.alignment = TextAnchor.MiddleLeft;
            panelText.fixedHeight = 48;
            artLabel = Font(30, Text, FontStyle.Normal);
            artLabel.alignment = TextAnchor.MiddleCenter;
        }

        public Texture2D Solid(Color color)
        {
            if (!solids.TryGetValue(color, out Texture2D t))
            {
                t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                t.SetPixel(0, 0, color);
                t.Apply();
                solids[color] = t;
            }
            return t;
        }

        static GUIStyle Font(int size, Color color, FontStyle style)
        {
            var g = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, richText = true, wordWrap = false };
            g.normal.textColor = color;
            return g;
        }

        static GUIStyle Box(Color fill, Color border, int radius, int borderWidth, int padding)
        {
            var g = new GUIStyle
            {
                border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2),
                padding = new RectOffset(padding, padding, padding, padding),
                margin = new RectOffset(0, 0, 0, 0),
            };
            g.normal.background = Shapes.Rounded(fill, border, radius, borderWidth);
            return g;
        }

        static GUIStyle Pill(Color fill, Color border, Color textColor)
        {
            GUIStyle g = Box(fill, border, 22, 2, 0);
            g.padding = new RectOffset(22, 22, 8, 8);
            g.margin = new RectOffset(0, 0, 4, 4);
            g.fontSize = 24;
            g.alignment = TextAnchor.MiddleCenter;
            g.normal.textColor = textColor;
            g.wordWrap = false;
            g.richText = true;
            return g;
        }

        static GUIStyle Button(Color fill, Color border, Color textColor, int fontSize)
        {
            GUIStyle g = Box(fill, border, 18, 3, 0);
            g.padding = new RectOffset(24, 24, 10, 10);
            g.margin = new RectOffset(0, 0, 8, 8);
            g.fontSize = fontSize;
            g.fontStyle = FontStyle.Bold;
            g.alignment = TextAnchor.MiddleCenter;
            g.wordWrap = true;
            g.fixedHeight = TouchTarget;
            g.normal.textColor = textColor;
            g.hover.background = Shapes.Rounded(Color.Lerp(fill, Color.white, 0.12f), border, 18, 3);
            g.hover.textColor = textColor;
            g.active.background = Shapes.Rounded(Color.Lerp(fill, Color.black, 0.2f), border, 18, 3);
            g.active.textColor = textColor;
            return g;
        }
    }

    static class Shapes
    {
        /// <summary>A rounded rectangle for 9-slicing: <paramref name="fill"/> inside, a <paramref name="border"/> edge.</summary>
        public static Texture2D Rounded(Color fill, Color border, int radius, int borderWidth, int size = 0)
        {
            if (size <= 0) size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to the rounded rectangle edge (negative inside).
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float distance = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude +
                                     Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    float edge = borderWidth > 0 ? Mathf.Clamp01(distance + borderWidth + 0.5f) : 0f;
                    Color c = Color.Lerp(fill, border, edge);
                    c.a *= alpha;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        public static Texture2D Diamond(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Abs(x + 0.5f - half) + Mathf.Abs(y + 0.5f - half) - (half - 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance)));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
