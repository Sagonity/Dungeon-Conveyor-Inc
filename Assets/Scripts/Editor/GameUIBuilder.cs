using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Menu item that builds the Main Menu / Game HUD / Settings UI in the open scene
/// and wires every reference. Run it once, tweak the look, then save the scene.
/// </summary>
public static class GameUIBuilder
{
    const string MenuPath = "Tools/Dungeon Conveyor/Build Game UI";
    const string PrefabFolder = "Assets/Prefabs";
    const string MinionPrefabPath = PrefabFolder + "/Minion.prefab";

    static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.09f, 0.97f);
    static readonly Color ButtonColor = new Color(0.42f, 0.27f, 0.14f, 1f);
    static readonly Color AccentColor = new Color(0.80f, 0.53f, 0.16f, 1f);
    static readonly Color TitleColor = new Color(1f, 0.82f, 0.36f, 1f);
    static readonly Color TextColor = new Color(0.95f, 0.93f, 0.88f, 1f);
    static readonly Color BarColor = new Color(0f, 0f, 0f, 0.45f);

    static DefaultControls.Resources resources;
    static int uiLayer;

    [MenuItem(MenuPath, true)]
    static bool ValidateBuild() => !EditorApplication.isPlaying;

    [MenuItem(MenuPath)]
    static void Build()
    {
        UIManager existing = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Build Game UI",
                "The scene already has a UIManager on '" + existing.name + "'. Replace it?",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 0;

        resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        };

        EnsureEventSystem();
        GameController controller = EnsureGameController();

        // Root canvas
        GameObject canvasGO = new GameObject("Game UI",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = uiLayer;
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f; // works in both portrait and landscape

        // Game HUD first so it sits behind the full-screen menus in draw order.
        GameHUDView hud = BuildGameHUD(canvasGO.transform, controller);
        MainMenuView mainMenu = BuildMainMenu(canvasGO.transform);
        SettingsView settings = BuildSettings(canvasGO.transform);

        UIManager manager = canvasGO.AddComponent<UIManager>();
        SetRef(manager, "mainMenuView", mainMenu);
        SetRef(manager, "gameView", hud);
        SetRef(manager, "settingsView", settings);

        // Inspector-visible wiring: UI state -> game pause/resume.
        UnityEventTools.AddPersistentListener(manager.OnEnterGame, new UnityAction(controller.ResumeGameplay));
        UnityEventTools.AddPersistentListener(manager.OnExitGame, new UnityAction(controller.PauseGameplay));
        EditorUtility.SetDirty(manager);

        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Game UI");
        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Selection.activeGameObject = canvasGO;

        Debug.Log("Game UI built. Save the scene (Ctrl/Cmd+S) to keep it.", canvasGO);
    }

    // ---------------------------------------------------------------- Views

    static MainMenuView BuildMainMenu(Transform parent)
    {
        MainMenuView view = CreateView<MainMenuView>("MainMenuView", parent, true);
        RectTransform column = CreateColumn("Content", view.transform, 48f);

        TextMeshProUGUI title = CreateText("Title", column, Application.productName, 120f,
            TitleColor, TextAlignmentOptions.Center, FontStyles.Bold);
        title.enableAutoSizing = true;
        title.fontSizeMin = 60f;
        title.fontSizeMax = 130f;
        SetPreferredSize(title.gameObject, 950f, 360f);

        Spacer(column, 60f);
        Button play = CreateButton("PlayButton", column, "Play", 640f, 160f, AccentColor);
        Button settings = CreateButton("SettingsButton", column, "Settings", 640f, 160f, ButtonColor);

        SetRef(view, "titleText", title);
        SetRef(view, "playButton", play);
        SetRef(view, "settingsButton", settings);
        return view;
    }

    static GameHUDView BuildGameHUD(Transform parent, GameController controller)
    {
        // No background: the board must stay visible behind the HUD.
        GameHUDView view = CreateView<GameHUDView>("GameHUDView", parent, false);

        // Top bar: menu button left, counter right.
        RectTransform bar = CreateRect("TopBar", view.transform);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(0f, 170f);
        Image barImage = bar.gameObject.AddComponent<Image>();
        barImage.color = BarColor;
        barImage.raycastTarget = false;

        Button menuButton = CreateButton("MainMenuButton", bar, "Menu", 260f, 120f, ButtonColor);
        RectTransform menuRect = (RectTransform)menuButton.transform;
        menuRect.anchorMin = menuRect.anchorMax = new Vector2(0f, 0.5f);
        menuRect.pivot = new Vector2(0f, 0.5f);
        menuRect.anchoredPosition = new Vector2(30f, 0f);

        TextMeshProUGUI counter = CreateText("SummonCounter", bar, "Minions summoned: 0", 48f,
            TextColor, TextAlignmentOptions.Right, FontStyles.Bold);
        RectTransform counterRect = counter.rectTransform;
        counterRect.anchorMin = Vector2.zero;
        counterRect.anchorMax = Vector2.one;
        counterRect.offsetMin = new Vector2(320f, 0f);
        counterRect.offsetMax = new Vector2(-40f, 0f);

        // Log above the summon button.
        TextMeshProUGUI log = CreateText("SummonLog", view.transform, "", 40f,
            TextColor, TextAlignmentOptions.BottomLeft, FontStyles.Normal);
        RectTransform logRect = log.rectTransform;
        logRect.anchorMin = new Vector2(0f, 0f);
        logRect.anchorMax = new Vector2(1f, 0f);
        logRect.pivot = new Vector2(0.5f, 0f);
        logRect.anchoredPosition = new Vector2(0f, 260f);
        logRect.sizeDelta = new Vector2(-80f, 240f);

        Button summon = CreateButton("SummonButton", view.transform, "Summon Minion", 640f, 170f, AccentColor);
        RectTransform summonRect = (RectTransform)summon.transform;
        summonRect.anchorMin = summonRect.anchorMax = new Vector2(0.5f, 0f);
        summonRect.pivot = new Vector2(0.5f, 0f);
        summonRect.anchoredPosition = new Vector2(0f, 60f);

        SetRef(view, "gameController", controller);
        SetRef(view, "summonButton", summon);
        SetRef(view, "mainMenuButton", menuButton);
        SetRef(view, "counterText", counter);
        SetRef(view, "logText", log);
        return view;
    }

    static SettingsView BuildSettings(Transform parent)
    {
        SettingsView view = CreateView<SettingsView>("SettingsView", parent, true);
        RectTransform column = CreateColumn("Content", view.transform, 40f);

        TextMeshProUGUI header = CreateText("Header", column, "Settings", 96f,
            TitleColor, TextAlignmentOptions.Center, FontStyles.Bold);
        SetPreferredSize(header.gameObject, 800f, 140f);

        // "Master Volume ........ 100%"
        RectTransform row = CreateRect("VolumeRow", column);
        HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        SetPreferredSize(row.gameObject, 700f, 80f);

        TextMeshProUGUI volumeLabel = CreateText("Label", row, "Master Volume", 52f,
            TextColor, TextAlignmentOptions.Left, FontStyles.Normal);
        volumeLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        TextMeshProUGUI volumeValue = CreateText("Value", row, "100%", 52f,
            TitleColor, TextAlignmentOptions.Right, FontStyles.Bold);
        SetPreferredSize(volumeValue.gameObject, 220f, 80f);

        Slider slider = CreateSlider("VolumeSlider", column);
        Toggle mute = CreateToggle("MuteToggle", column, "Mute Audio");

        Spacer(column, 40f);
        Button back = CreateButton("BackButton", column, "Back", 640f, 160f, ButtonColor);

        SetRef(view, "volumeSlider", slider);
        SetRef(view, "volumeValueText", volumeValue);
        SetRef(view, "muteToggle", mute);
        SetRef(view, "backButton", back);
        return view;
    }

    // ---------------------------------------------------------------- Scene setup

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
    }

    static GameController EnsureGameController()
    {
        GameController controller = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            GameObject go = new GameObject("GameController");
            Undo.RegisterCreatedObjectUndo(go, "Create GameController");
            controller = Undo.AddComponent<GameController>(go);
        }

        SerializedObject so = new SerializedObject(controller);
        SerializedProperty prefab = so.FindProperty("minionPrefab");
        if (prefab.objectReferenceValue == null) prefab.objectReferenceValue = EnsureMinionPrefab();
        SerializedProperty grid = so.FindProperty("gridManager");
        if (grid.objectReferenceValue == null) grid.objectReferenceValue = Object.FindFirstObjectByType<GridManager>();
        so.ApplyModifiedProperties();
        return controller;
    }

    /// <summary>Uses Assets/Prefabs/Minion.prefab, or creates it from a minion already in the scene.</summary>
    static GameObject EnsureMinionPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(MinionPrefabPath);
        if (existing != null) return existing;

        MinionHealth template = Object.FindFirstObjectByType<MinionHealth>(FindObjectsInactive.Include);
        if (template == null)
        {
            Debug.LogWarning("GameUIBuilder: no minion in the scene to make a prefab from. " +
                             "Assign 'Minion Prefab' on GameController by hand.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template.gameObject, MinionPrefabPath, out bool success);
        if (!success)
        {
            Debug.LogWarning("GameUIBuilder: could not save minion prefab to " + MinionPrefabPath);
            return null;
        }
        Debug.Log("GameUIBuilder: created " + MinionPrefabPath + " from '" + template.name + "'.", prefab);
        return prefab;
    }

    // ---------------------------------------------------------------- Widgets

    static T CreateView<T>(string name, Transform parent, bool withBackground) where T : UIView
    {
        RectTransform rect = CreateRect(name, parent);
        Stretch(rect);
        CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
        if (withBackground)
        {
            // Full-screen panel also blocks clicks from reaching anything behind it.
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = PanelColor;
        }
        T view = rect.gameObject.AddComponent<T>();
        SetRef(view, "canvasGroup", group);
        return view;
    }

    static RectTransform CreateColumn(string name, Transform parent, float spacing)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rect;
    }

    static Button CreateButton(string name, Transform parent, string label, float width, float height, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.sizeDelta = new Vector2(width, height);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = resources.standard;
        image.type = Image.Type.Sliced;
        image.color = color;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        SetPreferredSize(rect.gameObject, width, height);

        TextMeshProUGUI text = CreateText("Label", rect, label, 56f, TextColor,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    static Slider CreateSlider(string name, Transform parent)
    {
        GameObject go = DefaultControls.CreateSlider(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        SetLayerRecursively(go);
        SetPreferredSize(go, 700f, 50f);

        Transform fill = go.transform.Find("Fill Area/Fill");
        if (fill != null) fill.GetComponent<Image>().color = AccentColor;

        RectTransform handleArea = go.transform.Find("Handle Slide Area") as RectTransform;
        if (handleArea != null)
        {
            handleArea.offsetMin = new Vector2(28f, 0f);
            handleArea.offsetMax = new Vector2(-28f, 0f);
        }
        RectTransform handle = go.transform.Find("Handle Slide Area/Handle") as RectTransform;
        if (handle != null) handle.sizeDelta = new Vector2(56f, 30f);

        Slider slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    static Toggle CreateToggle(string name, Transform parent, string label)
    {
        GameObject go = DefaultControls.CreateToggle(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        SetLayerRecursively(go);
        SetPreferredSize(go, 700f, 80f);

        RectTransform background = go.transform.Find("Background") as RectTransform;
        if (background != null)
        {
            background.anchorMin = background.anchorMax = new Vector2(0f, 0.5f);
            background.pivot = new Vector2(0f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(70f, 70f);

            RectTransform check = background.Find("Checkmark") as RectTransform;
            if (check != null)
            {
                check.anchorMin = Vector2.zero;
                check.anchorMax = Vector2.one;
                check.offsetMin = new Vector2(8f, 8f);
                check.offsetMax = new Vector2(-8f, -8f);
            }
        }

        // Swap the legacy Text label for TextMeshPro to match the rest of the project.
        RectTransform labelRect = go.transform.Find("Label") as RectTransform;
        if (labelRect != null)
        {
            Text legacy = labelRect.GetComponent<Text>();
            if (legacy != null) Object.DestroyImmediate(legacy);

            TextMeshProUGUI text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 52f;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(95f, 0f);
            labelRect.offsetMax = Vector2.zero;
        }

        Toggle toggle = go.GetComponent<Toggle>();
        toggle.isOn = false;
        return toggle;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string content, float size,
        Color color, TextAlignmentOptions alignment, FontStyles style)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        return text;
    }

    static void Spacer(Transform parent, float height)
    {
        RectTransform rect = CreateRect("Spacer", parent);
        SetPreferredSize(rect.gameObject, 0f, height);
    }

    // ---------------------------------------------------------------- Helpers

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = uiLayer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void SetPreferredSize(GameObject go, float width, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
        ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);
    }

    static void SetLayerRecursively(GameObject go)
    {
        go.layer = uiLayer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject);
    }

    /// <summary>Assigns a private [SerializeField] reference so it shows up in the Inspector.</summary>
    static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError("GameUIBuilder: field '" + field + "' not found on " + target.GetType().Name);
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
