#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// сетап лр6
// Tools/Lab6/Setup UI, Coins, Scenes
public static class Lab6Setup
{
    private const string UiArt = "Assets/Art/UI";
    private const string FontTtf = "Assets/Art/Fonts/PixelifySans.ttf";
    private const string FontAssetPath = "Assets/Art/Fonts/PixelifySans SDF.asset";
    private const string EventsDir = "Assets/Data/Events";
    private const string Prefabs = "Assets/Prefabs";
    private const string Level01 = "Assets/Scenes/Level01.unity";
    private const string Level02 = "Assets/Scenes/Level02.unity";
    private const string MainMenu = "Assets/Scenes/MainMenu.unity";

    private static readonly Color Env = Hex("#1F3B4D");
    private static readonly Color PlayerCol = Hex("#E8F1F2");
    private static readonly Color UiCol = Hex("#5CE1E6");

    private static int L(string n) => LayerMask.NameToLayer(n);

    [MenuItem("Tools/Lab6/Setup UI, Coins, Scenes")]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new List<string>();

        PickupLayer(log);
        ImportUiSprites();
        var font = BuildFont(log);
        var ev = BuildEvents(log);
        var fx = BuildPickupFx(log);
        var coin = BuildCoin(ev["CoinsChanged"], fx, log);
        var exit = BuildExit(log);
        var ui = BuildGameplayUI(font, ev, log);

        SetupLevel01(ui, coin, exit, ev, log);
        SetupLevel02(coin, exit, log);
        BuildMainMenu(font, log);
        BuildSettings(log);

        EditorSceneManager.OpenScene(Level01);
        AssetDatabase.SaveAssets();
        Debug.Log("[Lab6 Setup]\n- " + string.Join("\n- ", log));
        EditorUtility.DisplayDialog("Lab6 Setup", "Готово.\n\n" + string.Join("\n", log), "OK");
    }

    // шар pickup бачить тільки гравця
    private static void PickupLayer(List<string> log)
    {
        if (L("Pickup") < 0)
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = "Pickup"; break; }
            }
            tm.ApplyModifiedPropertiesWithoutUndo();
        }
        int pickup = L("Pickup"), player = L("Player");
        for (int i = 0; i < 32; i++) Physics2D.IgnoreLayerCollision(pickup, i, i != player);
        log.Add("Шар Pickup: у матриці зіткнень лише з Player");
    }

    private static void ImportUiSprites()
    {
        foreach (var path in Directory.GetFiles(UiArt, "*.png").Select(p => p.Replace('\\', '/')))
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            bool world = path.EndsWith("coin.png") || path.EndsWith("exit-gate.png");
            imp.spritePixelsPerUnit = world ? 16 : 100;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = path.EndsWith("exit-gate.png") ? (int)SpriteAlignment.BottomCenter : (int)SpriteAlignment.Center;
            imp.SetTextureSettings(s);
            imp.spriteBorder = path.Contains("ui-button") || path.Contains("ui-panel") || path.Contains("ui-bar-bg")
                ? new Vector4(4, 4, 4, 4) : Vector4.zero;
            imp.SaveAndReimport();
        }
    }

    private static Sprite Ui(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArt}/{n}.png");

    private static TMP_FontAsset BuildFont(List<string> log)
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fa != null) return fa;
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(FontTtf);
        fa = TMP_FontAsset.CreateFontAsset(ttf, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        AssetDatabase.CreateAsset(fa, FontAssetPath);
        fa.atlasTextures[0].name = "PixelifySans Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = "PixelifySans Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        AssetDatabase.SaveAssets();
        log.Add("Шрифт Pixelify Sans → TMP Font Asset (динамічний, з кирилицею)");
        return fa;
    }

    private static Dictionary<string, IntEventSO> BuildEvents(List<string> log)
    {
        Directory.CreateDirectory(EventsDir);
        var d = new Dictionary<string, IntEventSO>();
        foreach (var n in new[] { "PlayerHPChanged", "PlayerMaxHPChanged", "CoinsChanged" })
        {
            string p = $"{EventsDir}/{n}.asset";
            var e = AssetDatabase.LoadAssetAtPath<IntEventSO>(p);
            if (e == null) { e = ScriptableObject.CreateInstance<IntEventSO>(); AssetDatabase.CreateAsset(e, p); }
            d[n] = e;
        }
        log.Add("Канали подій: PlayerHPChanged, PlayerMaxHPChanged, CoinsChanged");
        return d;
    }

    private static ParticleSystem BuildPickupFx(List<string> log)
    {
        var go = new GameObject("CoinPickupFX");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.6f; main.loop = false;
        main.startLifetime = 0.4f; main.startSpeed = 3f;
        main.startSize = 0.12f; main.gravityModifier = 0.5f;
        main.startColor = UiCol;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.playOnAwake = true;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.15f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        r.sortingLayerName = "Foreground"; r.sortingOrder = 10;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Prefabs}/CoinPickupFX.prefab");
        Object.DestroyImmediate(go);
        log.Add("Префаб CoinPickupFX: сплеск 12 частинок, Stop Action = Destroy");
        return prefab.GetComponent<ParticleSystem>();
    }

    private static GameObject BuildCoin(IntEventSO coinsChanged, ParticleSystem fx, List<string> log)
    {
        var go = new GameObject("Coin") { layer = L("Pickup") };
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Ui("coin");
        sr.sortingLayerName = "Environment"; sr.sortingOrder = 7;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true; col.radius = 0.4f;
        var c = go.AddComponent<Coin>();
        var so = new SerializedObject(c);
        so.FindProperty("coinsChanged").objectReferenceValue = coinsChanged;
        so.FindProperty("pickupEffect").objectReferenceValue = fx;
        so.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Prefabs}/Coin.prefab");
        Object.DestroyImmediate(go);
        log.Add("Префаб Coin: шар Pickup, Circle Collider 2D (Is Trigger, r 0,4)");
        return prefab;
    }

    private static GameObject BuildExit(List<string> log)
    {
        var go = new GameObject("LevelExit");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Ui("exit-gate");
        sr.sortingLayerName = "Environment"; sr.sortingOrder = 4;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true; box.size = new Vector2(1f, 2f); box.offset = new Vector2(0f, 1f);
        go.AddComponent<LevelExit>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Prefabs}/LevelExit.prefab");
        Object.DestroyImmediate(go);
        log.Add("Префаб LevelExit: арка, тригер, скрипт LevelExit");
        return prefab;
    }

    // ---------- ui рівня ----------
    private static GameObject BuildGameplayUI(TMP_FontAsset font, Dictionary<string, IntEventSO> ev, List<string> log)
    {
        var root = new GameObject("GameplayUI");
        var canvas = MakeCanvas(root.transform);
        MakeEventSystem(root.transform);

        // hud на весь екран
        var hud = MakeRect("HUD", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

        // смуга здоровʼя - лівий верхній кут
        var hp = MakeRect("HPBar", hud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(320, 28), new Vector2(0, 1));
        var hpBg = hp.gameObject.AddComponent<Image>();
        hpBg.sprite = Ui("ui-bar-bg"); hpBg.type = Image.Type.Sliced; hpBg.pixelsPerUnitMultiplier = 0.25f;
        var fillRt = MakeRect("Fill", hp, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0, 0.5f));
        fillRt.offsetMin = new Vector2(5, 5); fillRt.offsetMax = new Vector2(-5, -5);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.sprite = Ui("ui-bar-fill"); fill.color = UiCol;
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left; fill.fillAmount = 1f;
        fill.raycastTarget = false;
        var bar = hp.gameObject.AddComponent<HealthBar>();
        var bs = new SerializedObject(bar);
        bs.FindProperty("healthChanged").objectReferenceValue = ev["PlayerHPChanged"];
        bs.FindProperty("maxHealthChanged").objectReferenceValue = ev["PlayerMaxHPChanged"];
        bs.FindProperty("fill").objectReferenceValue = fill;
        bs.ApplyModifiedPropertiesWithoutUndo();

        // монети - правий верхній кут
        var icon = MakeRect("CoinIcon", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -20), new Vector2(40, 40), new Vector2(1, 1));
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.sprite = Ui("coin"); iconImg.preserveAspect = true; iconImg.raycastTarget = false;
        var lbl = MakeRect("CoinLabel", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -16), new Vector2(240, 48), new Vector2(1, 1));
        var tmp = MakeText(lbl, font, "0", 36, UiCol, TextAlignmentOptions.Right);
        var cc = lbl.gameObject.AddComponent<CoinCounter>();
        var cs = new SerializedObject(cc);
        cs.FindProperty("coinsChanged").objectReferenceValue = ev["CoinsChanged"];
        cs.FindProperty("label").objectReferenceValue = tmp;
        cs.ApplyModifiedPropertiesWithoutUndo();

        // пауза: попап з оверлеєм
        var popup = MakeRect("PausePopup", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        var overlay = MakeRect("Overlay", popup, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        var ov = overlay.gameObject.AddComponent<Image>();
        ov.color = new Color(0f, 0f, 0f, 180f / 255f); ov.raycastTarget = true;
        var panel = MakeRect("Panel", popup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 440), new Vector2(0.5f, 0.5f));
        var pimg = panel.gameObject.AddComponent<Image>();
        pimg.sprite = Ui("ui-panel"); pimg.type = Image.Type.Sliced; pimg.pixelsPerUnitMultiplier = 0.25f;
        var title = MakeRect("Title", panel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(480, 70), new Vector2(0.5f, 1));
        MakeText(title, font, "ПАУЗА", 56, PlayerCol, TextAlignmentOptions.Center);

        var pauseGo = new GameObject("PAUSE");
        pauseGo.transform.SetParent(root.transform, false);
        var pc = pauseGo.AddComponent<PauseController>();

        var b1 = MakeButton(panel, font, "Продовжити", new Vector2(0, -150), pc.Resume);
        MakeButton(panel, font, "Рестарт", new Vector2(0, -240), pc.Restart);
        MakeButton(panel, font, "У меню", new Vector2(0, -330), pc.ToMenu);

        var ps = new SerializedObject(pc);
        ps.FindProperty("pausePopup").objectReferenceValue = popup.gameObject;
        ps.FindProperty("firstButton").objectReferenceValue = b1.gameObject;
        ps.ApplyModifiedPropertiesWithoutUndo();
        popup.gameObject.SetActive(false);

        // старт рівня шле числа в hud
        var boot = new GameObject("LEVEL_BOOTSTRAP");
        boot.transform.SetParent(root.transform, false);
        var lb = boot.AddComponent<LevelBootstrap>();
        var ls = new SerializedObject(lb);
        ls.FindProperty("coinsChanged").objectReferenceValue = ev["CoinsChanged"];
        ls.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/GameplayUI.prefab");
        Object.DestroyImmediate(root);
        log.Add("Префаб GameplayUI: Canvas (1920×1080, Match 0,5), HUD, PausePopup (Overlay + Panel), EventSystem, LEVEL_BOOTSTRAP, PAUSE");
        return prefab;
    }

    private static RectTransform MakeCanvas(Transform parent)
    {
        var go = new GameObject("Canvas", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = L("UI");
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return (RectTransform)go.transform;
    }

    private static void MakeEventSystem(Transform parent)
    {
        var es = new GameObject("EventSystem");
        es.transform.SetParent(parent, false);
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = L("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    private static TMP_Text MakeText(RectTransform rt, TMP_FontAsset font, string text, float size, Color col, TextAlignmentOptions align)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.color = col;
        t.alignment = align; t.raycastTarget = false;
        return t;
    }

    private static Button MakeButton(RectTransform parent, TMP_FontAsset font, string label, Vector2 pos, UnityAction onClick)
    {
        var rt = MakeRect("Btn_" + label, parent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(400, 72), new Vector2(0.5f, 1));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Ui("ui-button"); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 0.25f;
        var b = rt.gameObject.AddComponent<Button>();
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Hex("#BFF4F6");
        colors.selectedColor = Hex("#BFF4F6");
        colors.pressedColor = Hex("#7FB2BB");
        colors.disabledColor = new Color(0.45f, 0.5f, 0.55f, 0.6f);
        b.colors = colors;
        var txt = MakeRect("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        MakeText(txt, font, label, 36, PlayerCol, TextAlignmentOptions.Center);
        UnityEventTools.AddPersistentListener(b.onClick, onClick);
        return b;
    }

    // ---------- рівні ----------
    private static void SetupLevel01(GameObject ui, GameObject coin, GameObject exit, Dictionary<string, IntEventSO> ev, List<string> log)
    {
        var scene = EditorSceneManager.OpenScene(Level01);
        EnsureUi(ui);
        WirePlayer(ev);

        var parent = EnsureRoot("PICKUPS");
        Clear(parent);
        var spots = new[] { (6f, 1.7f), (14.5f, 3.2f), (21f, 1.7f), (32.5f, 3.2f), (37f, 0.7f), (44f, 1.7f), (66f, 2.2f), (76f, 1.7f), (85f, 1.7f) };
        foreach (var (x, y) in spots) Place(coin, parent, new Vector3(x, y, 0));
        PlaceOnce(exit, "LevelExit", new Vector3(93.5f, 1f, 0));

        EditorSceneManager.SaveScene(scene);
        log.Add($"Level01: GameplayUI, {spots.Length} монет, вихід на (93,5; 1), канали на PlayerHealth");
    }

    private static void SetupLevel02(GameObject coin, GameObject exit, List<string> log)
    {
        bool fresh = !File.Exists(Level02);
        if (fresh) AssetDatabase.CopyAsset(Level01, Level02);
        var scene = EditorSceneManager.OpenScene(Level02);

        if (fresh)
        {
            // земля порожня - малюєш сам
            foreach (var n in new[] { "Ground", "Hazards", "Decor", "Background" })
            {
                var t = Find(n);
                if (t != null && t.TryGetComponent(out Tilemap tm)) tm.ClearAllTiles();
            }
            foreach (var n in new[] { "Enemy_Walker", "Enemy_Shooter", "Enemy_Rusher", "Enemy_Tank", "Lever", "LeverPlatform", "Checkpoint_1", "Checkpoint_2" })
            {
                var t = Find(n); if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            var player = Find("Player");
            if (player != null) player.position = new Vector3(2f, 1f, 0f);

            var bounds = Find("LevelBounds");
            if (bounds != null && bounds.TryGetComponent(out BoxCollider2D box))
            {
                box.size = new Vector2(56f, 17f);   // x -3..53
                box.offset = new Vector2(25f, 4.5f);
            }

            var gameplay = Find("GAMEPLAY");
            var cp = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Checkpoint.prefab");
            if (cp != null) { var c = (GameObject)PrefabUtility.InstantiatePrefab(cp, gameplay); c.name = "Checkpoint_1"; c.transform.position = new Vector3(27.5f, 1f, 0); }

            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Enemy.prefab");
            if (enemy != null)
            {
                Enemy(enemy, gameplay, "Enemy_Rusher", "Assets/Data/Enemies/Enemy_Rusher.asset", 38f);
                Enemy(enemy, gameplay, "Enemy_Shooter", "Assets/Data/Enemies/Enemy_Shooter.asset", 45f);
            }
        }

        var parent = EnsureRoot("PICKUPS");
        Clear(parent);
        var spots = new[] { (5f, 1.7f), (11f, 2.7f), (14f, 3.7f), (17f, 4.7f), (21.5f, 5.5f), (33f, 5.7f), (35f, 5.7f), (41f, 1.7f) };
        foreach (var (x, y) in spots) Place(coin, parent, new Vector3(x, y, 0));
        PlaceOnce(exit, "LevelExit", new Vector3(47.5f, 1f, 0));

        EditorSceneManager.SaveScene(scene);
        log.Add(fresh
            ? "Level02: копія Level01 з порожньою землею (малювати за схемою), чекпоінт, Rusher і Shooter, 8 монет, вихід"
            : "Level02: монети й вихід оновлено (землю не чіпав)");
    }

    private static void Enemy(GameObject prefab, Transform parent, string name, string statsPath, float x)
    {
        var e = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        e.name = name; e.transform.position = new Vector3(x, 1f, 0);
        var so = new SerializedObject(e.GetComponent<EnemyController>());
        so.FindProperty("stats").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(statsPath);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureUi(GameObject ui)
    {
        if (Find("GameplayUI") == null) PrefabUtility.InstantiatePrefab(ui);
    }

    private static void WirePlayer(Dictionary<string, IntEventSO> ev)
    {
        var p = Find("Player");
        if (p == null || !p.TryGetComponent(out PlayerHealth ph)) return;
        var so = new SerializedObject(ph);
        so.FindProperty("healthChanged").objectReferenceValue = ev["PlayerHPChanged"];
        so.FindProperty("maxHealthChanged").objectReferenceValue = ev["PlayerMaxHPChanged"];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform EnsureRoot(string name)
    {
        var t = Find(name);
        if (t != null) return t;
        var go = new GameObject(name);
        var gameplay = Find("GAMEPLAY");
        if (gameplay != null) go.transform.SetParent(gameplay, false);
        return go.transform;
    }

    private static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    private static void Place(GameObject prefab, Transform parent, Vector3 pos)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = pos;
    }

    private static void PlaceOnce(GameObject prefab, string name, Vector3 pos)
    {
        var t = Find(name);
        var go = t != null ? t.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, Find("GAMEPLAY"));
        go.name = name; go.transform.position = pos;
    }

    // ---------- головне меню ----------
    private static void BuildMainMenu(TMP_FontAsset font, List<string> log)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Env;
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.AddComponent<AudioListener>();

        var root = new GameObject("MenuUI");
        var canvas = MakeCanvas(root.transform);
        MakeEventSystem(root.transform);

        // фон - той самий цвинтар, приглушений
        var bg = MakeRect("Background", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Background/background.png");
        bgImg.color = Hex("#2E4A5A"); bgImg.preserveAspect = false; bgImg.raycastTarget = false;
        var shade = MakeRect("Shade", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        var sh = shade.gameObject.AddComponent<Image>();
        sh.color = new Color(Env.r, Env.g, Env.b, 0.55f); sh.raycastTarget = false;

        var title = MakeRect("Title", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1400, 160), new Vector2(0.5f, 0.5f));
        MakeText(title, font, "НІЧНИЙ ЛІС", 128, PlayerCol, TextAlignmentOptions.Center);
        var sub = MakeRect("Subtitle", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1200, 60), new Vector2(0.5f, 0.5f));
        MakeText(sub, font, "2D-платформер", 40, UiCol, TextAlignmentOptions.Center);

        var block = MakeRect("Buttons", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(460, 320), new Vector2(0.5f, 0.5f));

        var menuGo = new GameObject("Menu");
        var menu = menuGo.AddComponent<MainMenuUI>();
        var bNew = MakeButton(block, font, "Нова гра", new Vector2(0, 0), menu.NewGame);
        var bCont = MakeButton(block, font, "Продовжити", new Vector2(0, -100), menu.Continue);
        MakeButton(block, font, "Вийти", new Vector2(0, -200), menu.Quit);

        var so = new SerializedObject(menu);
        so.FindProperty("continueButton").objectReferenceValue = bCont;
        so.FindProperty("newGameButton").objectReferenceValue = bNew;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, MainMenu);
        log.Add("MainMenu: фон, назва «НІЧНИЙ ЛІС» шрифтом Pixelify Sans, кнопки Нова гра / Продовжити / Вийти");
    }

    private static void BuildSettings(List<string> log)
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenu, true),
            new EditorBuildSettingsScene(Level01, true),
            new EditorBuildSettingsScene(Level02, true),
        };
        PlayerSettings.companyName = "Karpovych";
        PlayerSettings.productName = "MyPlatformer";
        log.Add("Scene List: 0 MainMenu, 1 Level01, 2 Level02");
    }

    private static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.white;

    private static Transform Find(string name)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }
}
#endif
