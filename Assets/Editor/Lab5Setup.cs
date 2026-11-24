#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

// сетап ворогів лр5
// Tools/Lab5/Setup Enemies
public static class Lab5Setup
{
    private const string ArtDir   = "Assets/Art/Enemies";
    private const string AnimDir  = "Assets/Animations/Enemy";
    private const string DataDir  = "Assets/Data/Enemies";
    private const string EnemyPrefabPath      = "Assets/Prefabs/Enemy.prefab";
    private const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";
    private const string TelegraphSfxPath     = "Assets/Audio/Enemy/enemy-telegraph.ogg";
    private const string MixerPath            = "Assets/Audio/GameMixer.mixer";

    private static int L(string name) => LayerMask.NameToLayer(name);

    [MenuItem("Tools/Lab5/Setup Enemies")]
    public static void Setup()
    {
        var log = new List<string>();
        if (L("Enemy") < 0 || L("Player") < 0)
        {
            EditorUtility.DisplayDialog("Lab5", "Немає фізичних шарів Enemy / Player у Tags and Layers.", "OK");
            return;
        }

        ImportSprites(log);
        var controller = BuildAnimator(log);
        var stats = CreateStats(log);
        var projectile = BuildProjectile(log);
        var enemy = BuildEnemyPrefab(controller, stats["Enemy_Walker"], projectile, log);
        SolidGround(log);
        SetupPlayer(log);
        PlaceEnemies(enemy, stats, log);

        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Lab5 Setup]\n- " + string.Join("\n- ", log));
        EditorUtility.DisplayDialog("Lab5 Setup", "Готово, сцену збережено.\n\n" + string.Join("\n", log), "OK");
    }

    // імпорт як у героя
    private static void ImportSprites(List<string> log)
    {
        foreach (var path in Directory.GetFiles(ArtDir, "*.png").Select(p => p.Replace('\\', '/')))
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;

            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = path.EndsWith("projectile.png")
                ? (int)SpriteAlignment.Center
                : (int)SpriteAlignment.BottomCenter;
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }
        log.Add("Спрайти ворога: PPU 16, Point, без стиснення, Pivot Bottom");
    }

    private static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

    // кліпи + аніматор
    private static AnimatorController BuildAnimator(List<string> log)
    {
        Directory.CreateDirectory(AnimDir);
        var idle  = Clip("Enemy_Idle",      8,  true,  "enemy-idle-1", "enemy-idle-1", "enemy-idle-2", "enemy-idle-2");
        var run   = Clip("Enemy_Run",       10, true,  Enumerable.Range(1, 8).Select(i => $"enemy-run-{i}").ToArray());
        var tele  = Clip("Enemy_Telegraph", 8,  false, "enemy-telegraph-1", "enemy-telegraph-2", "enemy-telegraph-1", "enemy-telegraph-2");
        var hurt  = Clip("Enemy_Hurt",      10, false, "enemy-hurt-1", "enemy-hurt-2", "enemy-hurt-1");
        var die   = Clip("Enemy_Die",       10, false, Enumerable.Range(1, 5).Select(i => $"enemy-die-{i}").ToArray());

        string path = $"{AnimDir}/Enemy.controller";
        // не видаляти ассет, інакше вороги в сцені гублять посилання
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ac == null) ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (var p in ac.parameters) ac.RemoveParameter(p);
        var oldSm = ac.layers[0].stateMachine;
        foreach (var t in oldSm.anyStateTransitions) oldSm.RemoveAnyStateTransition(t);
        foreach (var st in oldSm.states) oldSm.RemoveState(st.state);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Telegraph", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;
        var sIdle = sm.AddState("Enemy_Idle", new Vector3(300, 0));
        var sRun  = sm.AddState("Enemy_Run", new Vector3(560, 0));
        var sTele = sm.AddState("Enemy_Telegraph", new Vector3(300, 140));
        var sHurt = sm.AddState("Enemy_Hurt", new Vector3(560, 140));
        var sDie  = sm.AddState("Enemy_Die", new Vector3(820, 140));
        sIdle.motion = idle; sRun.motion = run; sTele.motion = tele; sHurt.motion = hurt; sDie.motion = die;
        sm.defaultState = sIdle;
        sm.anyStatePosition = new Vector3(560, 280);

        // ходьба
        var t1 = sIdle.AddTransition(sRun);  Setup(t1, false, 0f, 0.05f); t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        var t2 = sRun.AddTransition(sIdle);  Setup(t2, false, 0f, 0.05f); t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // any state
        foreach (var (state, trigger) in new[] { (sTele, "Telegraph"), (sHurt, "Hurt"), (sDie, "Die") })
        {
            var t = sm.AddAnyStateTransition(state);
            Setup(t, false, 0f, 0f);
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        // назад в idle після кліпу
        var b1 = sTele.AddTransition(sIdle); Setup(b1, true, 1f, 0f);
        var b2 = sHurt.AddTransition(sIdle); Setup(b2, true, 1f, 0f);
        // з die нікуди

        AssetDatabase.SaveAssets();
        log.Add("Аніматор: 5 кліпів, параметри Speed/Telegraph/Hurt/Die, переходи з Any State");
        return ac;
    }

    private static void Setup(AnimatorStateTransition t, bool exitTime, float exit, float duration)
    {
        t.hasExitTime = exitTime;
        t.exitTime = exit;
        t.hasFixedDuration = true;
        t.duration = duration;
    }

    private static AnimationClip Clip(string name, float fps, bool loop, params string[] frames)
    {
        string path = $"{AnimDir}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = fps;

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = S(frames[i]) };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = S(frames[^1]) };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // ассети ворогів
    private static Dictionary<string, EnemyStatsSO> CreateStats(List<string> log)
    {
        Directory.CreateDirectory(DataDir);
        var sfx = AssetDatabase.LoadAssetAtPath<AudioClip>(TelegraphSfxPath);
        var result = new Dictionary<string, EnemyStatsSO>();

        result["Enemy_Walker"] = Stats("Enemy_Walker", sfx, s => { });

        // танк
        result["Enemy_Tank"] = Stats("Enemy_Tank", sfx, s =>
        {
            s.patrolSpeed = 0.8f; s.chaseSpeed = 1.8f; s.chaseRange = 5f;
            s.attackRange = 1.5f; s.attackRadius = 0.8f;
            s.maxHealth = 90; s.damage = 20; s.telegraphTime = 0.8f; s.recoveryTime = 0.7f;
            s.tint = Hex("#6FA3B8"); s.sizeScale = 1.35f;
        });

        // рашер
        result["Enemy_Rusher"] = Stats("Enemy_Rusher", sfx, s =>
        {
            s.patrolSpeed = 2.5f; s.chaseSpeed = 6f; s.chaseRange = 9f;
            s.maxHealth = 12; s.damage = 8; s.telegraphTime = 0.2f; s.recoveryTime = 0.15f;
            s.tint = Hex("#5CE1E6"); s.sizeScale = 0.85f;
        });

        // стрілець
        result["Enemy_Shooter"] = Stats("Enemy_Shooter", sfx, s =>
        {
            s.patrolSpeed = 1f; s.chaseSpeed = 1.2f; s.chaseRange = 8f;
            s.attackRange = 6f; s.stopDistance = 5f;
            s.maxHealth = 20; s.damage = 8; s.telegraphTime = 0.5f; s.recoveryTime = 0.9f;
            s.tint = Hex("#B084E8"); s.sizeScale = 1f; s.ranged = true;
        });

        log.Add("Ассети: Enemy_Walker, Enemy_Tank, Enemy_Rusher, Enemy_Shooter у " + DataDir);
        return result;
    }

    private static EnemyStatsSO Stats(string name, AudioClip sfx, System.Action<EnemyStatsSO> configure)
    {
        string path = $"{DataDir}/{name}.asset";
        var s = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(path);
        if (s != null) return s;   // не перетирати свої правки
        s = ScriptableObject.CreateInstance<EnemyStatsSO>();
        s.telegraphSfx = sfx;
        configure(s);
        AssetDatabase.CreateAsset(s, path);
        return s;
    }

    // снаряд
    private static Projectile BuildProjectile(List<string> log)
    {
        var go = new GameObject("Projectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = S("projectile");
        sr.sortingLayerName = "Enemies";
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.25f;
        var p = go.AddComponent<Projectile>();
        var so = new SerializedObject(p);
        so.FindProperty("targetLayers").FindPropertyRelative("m_Bits").intValue = (1 << L("Player")) | (1 << L("Ground"));
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
        Object.DestroyImmediate(go);
        log.Add("Префаб Projectile: Gravity 0, Circle Collider 2D (Is Trigger), Target Layers = Player + Ground");
        return prefab.GetComponent<Projectile>();
    }

    // префаб ворога
    private static GameObject BuildEnemyPrefab(AnimatorController ac, EnemyStatsSO walker, Projectile projectile, List<string> log)
    {
        var root = new GameObject("Enemy") { layer = L("Enemy") };

        var visual = Child(root, "Visual", Vector3.zero);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = S("enemy-idle-1");
        sr.sortingLayerName = "Enemies";
        var anim = visual.AddComponent<Animator>();
        anim.runtimeAnimatorController = ac;
        anim.applyRootMotion = false;

        var ground = Child(root, "GroundCheck", new Vector3(0f, 0f));
        var edge   = Child(root, "EdgeCheck",   new Vector3(0.7f, -0.25f));
        var wall   = Child(root, "WallCheck",   new Vector3(0.75f, 1.3f));
        var attack = Child(root, "AttackPoint", new Vector3(0.9f, 1.3f));

        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var capsule = root.AddComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Vertical;
        capsule.size = new Vector2(1f, 2.6f);
        capsule.offset = new Vector2(0f, 1.3f);
        capsule.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Settings/PlayerNoFriction.physicsMaterial2D");
        // крізь гравця і ворогів
        capsule.excludeLayers = (1 << L("Player")) | (1 << L("Enemy"));

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = false;
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer != null)
            audio.outputAudioMixerGroup = mixer.FindMatchingGroups("Master/SFX").FirstOrDefault()
                                          ?? mixer.FindMatchingGroups("SFX").FirstOrDefault();

        var health = root.AddComponent<EnemyHealth>();
        var ctrl = root.AddComponent<EnemyController>();

        var so = new SerializedObject(ctrl);
        so.FindProperty("stats").objectReferenceValue = walker;
        so.FindProperty("groundCheck").objectReferenceValue = ground.transform;
        so.FindProperty("edgeCheck").objectReferenceValue = edge.transform;
        so.FindProperty("wallCheck").objectReferenceValue = wall.transform;
        so.FindProperty("attackPoint").objectReferenceValue = attack.transform;
        so.FindProperty("groundLayer").FindPropertyRelative("m_Bits").intValue = 1 << L("Ground");
        so.FindProperty("damageableLayers").FindPropertyRelative("m_Bits").intValue = 1 << L("Player");
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("projectilePrefab").objectReferenceValue = projectile;
        so.ApplyModifiedPropertiesWithoutUndo();

        var hs = new SerializedObject(health);
        hs.FindProperty("controller").objectReferenceValue = ctrl;
        hs.FindProperty("hitbox").objectReferenceValue = capsule;
        hs.FindProperty("invulnerableTime").floatValue = 0.6f;
        hs.FindProperty("knockbackForce").floatValue = 8f;
        hs.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        Object.DestroyImmediate(root);
        log.Add("Префаб Enemy: Visual + 4 датчики, Rigidbody 2D, Capsule (Exclude Layers: Player, Enemy), Audio Source → SFX, EnemyHealth, EnemyController");
        return prefab;
    }

    private static GameObject Child(GameObject parent, string name, Vector3 local)
    {
        var go = new GameObject(name) { layer = parent.layer };
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = local;
        return go;
    }

    // outlines - пуста всередині, датчики під ногами нічого не бачать
    private static void SolidGround(List<string> log)
    {
        var ground = Find("Ground");
        if (ground != null && ground.TryGetComponent(out CompositeCollider2D comp))
        {
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
            comp.GenerateGeometry();
            log.Add("Ground: Composite Collider 2D → Geometry Type = Polygons");
        }
    }

    // гравець
    private static void SetupPlayer(List<string> log)
    {
        var player = Find("Player");
        if (player == null) { log.Add("УВАГА: Player не знайдено"); return; }

        player.tag = "Player";
        player.layer = L("Player");

        var respawn = player.GetComponent<PlayerRespawn>();
        // ?? тут не працює
        if (!player.TryGetComponent(out PlayerHealth ph)) ph = player.AddComponent<PlayerHealth>();
        var hs = new SerializedObject(ph);
        hs.FindProperty("maxHealth").intValue = 30;
        hs.FindProperty("invulnerableTime").floatValue = 0.8f;
        hs.FindProperty("knockbackForce").floatValue = 8f;
        hs.FindProperty("respawn").objectReferenceValue = respawn;
        hs.FindProperty("controller").objectReferenceValue = player.GetComponent<PlayerController>();
        hs.ApplyModifiedPropertiesWithoutUndo();

        var stompPoint = player.transform.Find("StompPoint");
        if (stompPoint == null)
        {
            stompPoint = new GameObject("StompPoint").transform;
            stompPoint.SetParent(player.transform, false);
        }
        stompPoint.localPosition = new Vector3(0f, -0.05f, 0f);

        if (!player.TryGetComponent(out PlayerStomp stomp)) stomp = player.AddComponent<PlayerStomp>();
        var ss = new SerializedObject(stomp);
        ss.FindProperty("stompPoint").objectReferenceValue = stompPoint;
        ss.FindProperty("radius").floatValue = 0.35f;
        ss.FindProperty("enemyLayer").FindPropertyRelative("m_Bits").intValue = 1 << L("Enemy");
        ss.ApplyModifiedPropertiesWithoutUndo();

        log.Add("Player: тег Player, шар Player, PlayerHealth (30 HP, 0,8 с невразливості), PlayerStomp + StompPoint");
    }

    // вороги на рівні
    private static void PlaceEnemies(GameObject prefab, Dictionary<string, EnemyStatsSO> stats, List<string> log)
    {
        var parent = Find("GAMEPLAY");
        var places = new (string name, string stats, float x)[]
        {
            ("Enemy_Walker",  "Enemy_Walker",  45f),
            ("Enemy_Shooter", "Enemy_Shooter", 78f),   // за ямою
            ("Enemy_Rusher",  "Enemy_Rusher",  84f),   // є куди відступати
            ("Enemy_Tank",    "Enemy_Tank",    91f),   // біля фінішу
        };

        foreach (var (name, statsName, x) in places)
        {
            var existing = Find(name);
            var go = existing != null ? existing : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent != null ? parent.transform : null);
            go.name = name;
            go.transform.position = new Vector3(x, 1f, 0f);
            var so = new SerializedObject(go.GetComponent<EnemyController>());
            so.FindProperty("stats").objectReferenceValue = stats[statsName];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        log.Add("На рівні: Walker (45), Shooter (78), Rusher (84), Tank (91) - відрізняється лише поле Stats");
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

    private static GameObject Find(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
}
#endif
