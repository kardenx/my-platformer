#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// сетап сцени лр4
// Tools/Lab4/Setup Scene
public static class Lab4SceneSetup
{
    private const int InteractableLayer = 10;
    private const string CheckpointPrefabPath = "Assets/Prefabs/Checkpoint.prefab";

    // рівень 102, екран 20, шлях камери 82
    // ширина = 20 + (1 - f) * 82
    private const float CamMinX = 7f;
    private const float CamPath = 82f;
    private const float ViewWidth = 20f;

    [MenuItem("Tools/Lab4/Setup Scene")]
    public static void Setup()
    {
        var log = new List<string>();

        Transform env = Find("ENV"), gameplay = Find("GAMEPLAY"), systems = Find("SYSTEMS");
        Transform player = Find("Player");
        Transform leverPlatform = Find("LeverPlatform");
        if (player == null || env == null || gameplay == null)
        {
            EditorUtility.DisplayDialog("Lab4 Setup", "Не знайдено Player / ENV / GAMEPLAY у відкритій сцені.", "OK");
            return;
        }

        SetupTilemaps(log);
        SetupPlayer(player, log);
        SetupCheckpoints(gameplay, log);
        SetupLever(gameplay, leverPlatform, log);
        SetupParallax(env, log);
        Collider2D bounds = SetupLevelBounds(systems != null ? systems : env, log);
        SetupCinemachine(player, bounds, log);

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        Debug.Log("[Lab4 Setup]\n- " + string.Join("\n- ", log));
        EditorUtility.DisplayDialog("Lab4 Setup", "Готово, сцену збережено.\n\n" + string.Join("\n", log), "OK");
    }

    // тайлмапи
    private static void SetupTilemaps(List<string> log)
    {
        Transform hazards = Find("Hazards");
        if (hazards != null)
        {
            var col = Ensure<TilemapCollider2D>(hazards.gameObject);
            col.isTrigger = true;
            Ensure<Hazard>(hazards.gameObject);
            hazards.gameObject.layer = LayerMask.NameToLayer("Hazard");
            log.Add("Hazards: Tilemap Collider 2D (Is Trigger) + Hazard");
        }

        Transform decor = Find("Decor");
        if (decor != null && decor.TryGetComponent(out Tilemap decorMap))
        {
            decorMap.tileAnchor = new Vector3(0.5f, 0f, 0f);
            decorMap.color = Hex("#8FA3AC");                        // декор тихіший за геометрію
            SetSorting(decor.GetComponent<TilemapRenderer>(), "Environment", 5);
            foreach (var c in decor.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
            log.Add("Decor: Tile Anchor (0.5, 0), приглушений колір, без колайдера");
        }

        Transform background = Find("Background");
        if (background != null && background.TryGetComponent(out Tilemap bgMap))
        {
            bgMap.color = Hex("#4A6572");
            SetSorting(background.GetComponent<TilemapRenderer>(), "Background", 5);
        }

        Transform ground = Find("Ground");
        if (ground != null)
        {
            SetSorting(ground.GetComponent<TilemapRenderer>(), "Environment", 0);
            SetSorting(hazards != null ? hazards.GetComponent<TilemapRenderer>() : null, "Environment", 6);
        }
    }

    // гравець
    private static void SetupPlayer(Transform player, List<string> log)
    {
        Ensure<PlayerRespawn>(player.gameObject);
        var impulse = Ensure<CinemachineImpulseSource>(player.gameObject);

        Transform point = player.Find("InteractPoint");
        if (point == null)
        {
            point = new GameObject("InteractPoint").transform;
            point.SetParent(player, false);
        }
        point.localPosition = new Vector3(0f, 1.6f, 0f);   // груди

        var pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            var so = new SerializedObject(pc);
            so.FindProperty("interactPoint").objectReferenceValue = point;
            so.FindProperty("interactableLayer").FindPropertyRelative("m_Bits").intValue = 1 << InteractableLayer;
            so.FindProperty("impulseSource").objectReferenceValue = impulse;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        log.Add("Player: PlayerRespawn, Impulse Source, InteractPoint (0; 1,6), шар взаємодії = Interactable");
    }

    // чекпоінти
    private static void SetupCheckpoints(Transform parent, List<string> log)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath);
        if (prefab == null)
        {
            var go = new GameObject("Checkpoint");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Level/checkpoint.png");
            SetSorting(sr, "Environment", 4);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1f, 3f);
            box.offset = new Vector2(0f, 1.5f);
            go.AddComponent<Checkpoint>();
            prefab = PrefabUtility.SaveAsPrefabAsset(go, CheckpointPrefabPath);
            Object.DestroyImmediate(go);
            log.Add("Префаб Assets/Prefabs/Checkpoint.prefab");
        }

        PlaceCheckpoint(prefab, parent, "Checkpoint_1", new Vector3(27.5f, 1f, 0f));
        PlaceCheckpoint(prefab, parent, "Checkpoint_2", new Vector3(53.5f, 1f, 0f));
        log.Add("Checkpoint_1 (27,5; 1) перед бітом 2, Checkpoint_2 (53,5; 1) перед бітом 3");
    }

    private static void PlaceCheckpoint(GameObject prefab, Transform parent, string name, Vector3 pos)
    {
        Transform existing = Find(name);
        GameObject go = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;
    }

    // важіль
    private static void SetupLever(Transform parent, Transform leverPlatform, List<string> log)
    {
        Transform t = Find("Lever");
        GameObject go = t != null ? t.gameObject : new GameObject("Lever");
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(57.5f, 1f, 0f);
        go.layer = InteractableLayer;

        var sr = Ensure<SpriteRenderer>(go);
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Level/lever.png");
        SetSorting(sr, "Environment", 4);

        var box = Ensure<BoxCollider2D>(go);
        box.isTrigger = true;
        box.size = new Vector2(1.2f, 2f);
        box.offset = new Vector2(0f, 1f);

        var lever = Ensure<Lever>(go);
        var so = new SerializedObject(lever);
        if (leverPlatform != null) so.FindProperty("target").objectReferenceValue = leverPlatform.gameObject;
        so.FindProperty("handle").objectReferenceValue = sr;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (leverPlatform != null) leverPlatform.gameObject.SetActive(false);   // на старті вимкнена
        log.Add("Lever (57,5; 1), шар Interactable, ціль LeverPlatform (вимкнена на старті)");
    }

    // паралакс
    private static void SetupParallax(Transform env, List<string> log)
    {
        Transform root = Find("Parallax");
        if (root == null) root = new GameObject("Parallax").transform;
        root.SetParent(null, true);
        root.position = Vector3.zero;

        MakeLayer(root, "BG_Far", "Assets/Art/Sprites/Background/background.png", 0.2f, 0, "#5F8797", 4f);
        MakeLayer(root, "BG_Near", "Assets/Art/Tilesets/mountains.png", 0.5f, 1, "#6FA3B8", 2.5f);

        // старі фони не треба
        foreach (var old in new[] { "Mountains", "Graveyard" })
        {
            Transform o = Find(old);
            if (o != null && o.parent == env) Object.DestroyImmediate(o.gameObject);
        }
        log.Add($"Parallax: BG_Far f=0,2 ширина {LayerWidth(0.2f):0}, BG_Near f=0,5 ширина {LayerWidth(0.5f):0}; старі Mountains/Graveyard прибрано");
    }

    private static float LayerWidth(float f) => Mathf.Ceil(ViewWidth + (1f - f) * CamPath + 2f);

    private static void MakeLayer(Transform root, string name, string spritePath, float factor, int order, string tint, float y)
    {
        Transform t = root.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name);
        go.transform.SetParent(root, false);

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        var sr = Ensure<SpriteRenderer>(go);
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(LayerWidth(factor), sprite != null ? sprite.bounds.size.y : 10f);
        sr.color = Hex(tint);
        SetSorting(sr, "Background", order);

        float centerX = CamMinX + 0.5f * (1f - factor) * CamPath;
        go.transform.position = new Vector3(centerX, y, 0f);

        var layer = Ensure<ParallaxLayer>(go);
        var so = new SerializedObject(layer);
        so.FindProperty("parallaxFactor").floatValue = factor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // межі рівня
    private static Collider2D SetupLevelBounds(Transform parent, List<string> log)
    {
        Transform t = Find("LevelBounds");
        GameObject go = t != null ? t.gameObject : new GameObject("LevelBounds");
        go.transform.SetParent(parent, false);
        go.transform.position = Vector3.zero;

        var box = Ensure<BoxCollider2D>(go);
        box.isTrigger = true;   // тригер
        box.size = new Vector2(102f, 17f);
        box.offset = new Vector2(48f, 4.5f);
        log.Add("LevelBounds: Box Collider 2D (Is Trigger) 102 × 17, Offset (48; 4,5)");
        return box;
    }

    // cinemachine
    private static void SetupCinemachine(Transform player, Collider2D bounds, List<string> log)
    {
        Camera main = Camera.main;
        if (main != null)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(main.gameObject);
            Ensure<CinemachineBrain>(main.gameObject);
        }

        Transform t = Find("CinemachineCamera");
        GameObject go = t != null ? t.gameObject : new GameObject("CinemachineCamera");
        go.transform.SetParent(null, true);
        go.transform.position = new Vector3(player.position.x, player.position.y, -10f);

        var cam = Ensure<CinemachineCamera>(go);
        cam.Target.TrackingTarget = player;
        var lens = cam.Lens;
        lens.OrthographicSize = 5.5f;
        cam.Lens = lens;

        var composer = Ensure<CinemachinePositionComposer>(go);
        var comp = composer.Composition;
        comp.DeadZone.Enabled = true;
        comp.DeadZone.Size = new Vector2(0.15f, 0.15f);
        composer.Composition = comp;
        composer.Lookahead = new LookaheadSettings { Enabled = true, Time = 0.3f, Smoothing = 4f, IgnoreY = false };
        composer.Damping = new Vector3(0.7f, 0.7f, 0f);
        composer.CameraDistance = 10f;

        var confiner = Ensure<CinemachineConfiner2D>(go);
        confiner.BoundingShape2D = bounds;
        confiner.InvalidateBoundingShapeCache();

        Ensure<CinemachineImpulseListener>(go);
        Ensure<CinemachinePixelPerfect>(go);
        log.Add("CinemachineCamera: Position Composer (Dead Zone 0,15 × 0,15, Lookahead 0,3 / 4, Damping 0,7), Confiner 2D, Impulse Listener, Pixel Perfect; Brain на Main Camera");
    }

    private static T Ensure<T>(GameObject go) where T : Component =>
        go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    private static void SetSorting(Renderer r, string layer, int order)
    {
        if (r == null) return;
        r.sortingLayerName = layer;
        r.sortingOrder = order;
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

    // з вимкненими теж
    private static Transform Find(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }
}
#endif
