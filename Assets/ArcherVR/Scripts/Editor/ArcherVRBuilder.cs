using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ArcherVR.EditorTools
{
    /// <summary>
    /// One-click builder for the Archer VR story-map scenes.
    ///   Archer VR > Build Everything   (prefabs + scenes + build settings)
    /// Re-run it any time: after importing new enemy packs, or after moving the tower in
    /// "Group Project.unity" (the team's layout scene, which every story scene copies).
    /// </summary>
    public static class ArcherVRBuilder
    {
        const string Root = "Assets/ArcherVR";
        const string PrefabDir = Root + "/Prefabs";
        const string MatDir = Root + "/Materials";
        const string AnimDir = Root + "/Animations";
        const string SceneDir = Root + "/Scenes";
        const string BaseScene = "Assets/Group Project.unity";
        const string RigPrefab = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        // Which Asset Store packs fill which Figma enemy role (first match wins).
        static readonly string[] BasicSearch = { "skeleton" };
        static readonly string[] AdvancedSearch = { "crusader", "warrior", "assassin" };
        static readonly string[] BossSearch = { "orc", "giant", "drakonit" };

        // Skyboxes from Fantasy Skybox FREE, picked per story beat.
        const string SkyExposition = "Assets/Fantasy Skybox FREE/Panoramics/FS002/FS002_Day.mat";
        const string SkyBattle = "Assets/Fantasy Skybox FREE/Panoramics/FS002/FS002_Sunset.mat";
        const string SkyResolution = "Assets/Fantasy Skybox FREE/Panoramics/FS003/FS003_Sunrise.mat";
        const string GrassTex = "Assets/Fantasy Skybox FREE/Scenes/Textures (Terrain)/Texture_Grass_Diffuse.png";

        static readonly string[] TowerPrefixes = { "Tower_", "Floor_", "Top_" };

        // ------------------------------------------------------------------ menu

        [MenuItem("Archer VR/Build Everything", priority = 0)]
        public static void BuildEverything()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildPrefabs();
            BuildScenes();
            EditorUtility.DisplayDialog("Archer VR", "Built prefabs and scenes S1_Exposition, S2_Battle, S4_Resolution.\n\nOpen Assets/ArcherVR/Scenes/S1_Exposition and press Play.", "OK");
        }

        [MenuItem("Archer VR/1. Build Prefabs", priority = 20)]
        public static void BuildPrefabs()
        {
            EnsureFolders();
            ConvertBuiltinMaterialsToURP();
            BuildHitMarker();
            var arrow = BuildArrow();
            BuildBow(arrow);
            BuildEnemy(EnemyTier.Basic, BasicSearch);
            BuildEnemy(EnemyTier.Advanced, AdvancedSearch);
            BuildEnemy(EnemyTier.Boss, BossSearch);
            AssetDatabase.SaveAssets();
            Debug.Log("[Archer VR] Prefabs built in " + PrefabDir);
        }

        [MenuItem("Archer VR/2. Build Scenes", priority = 21)]
        public static void BuildScenes()
        {
            EnsureFolders();
            var s1 = BuildScene(GameFlow.SceneExposition, Beat.Exposition);
            var s2 = BuildScene(GameFlow.SceneBattle, Beat.Battle);
            var s4 = BuildScene(GameFlow.SceneResolution, Beat.Resolution);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(s1, true),
                new EditorBuildSettingsScene(s2, true),
                new EditorBuildSettingsScene(s4, true),
            };
            EditorSceneManager.OpenScene(s1);
            Debug.Log("[Archer VR] Scenes built and added to Build Settings.");
        }

        /// <summary>
        /// Static objects get merged into one combined mesh at Play, which can't move, so the
        /// tower would stay put while only its colliders sank. Clear every Static flag under it.
        /// </summary>
        static int MakeMovable(Transform root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                n++;
            }
            return n;
        }

        /// <summary>
        /// Fixes the tower in the existing story scenes WITHOUT rebuilding them, so any
        /// changes the team has made to S1/S2/S4 are kept.
        /// </summary>
        [MenuItem("Archer VR/Fix Tower Collapse In Existing Scenes", priority = 30)]
        public static void FixTowerCollapseInScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = new List<string>();
            foreach (var name in new[] { GameFlow.SceneExposition, GameFlow.SceneBattle, GameFlow.SceneResolution })
            {
                var path = $"{SceneDir}/{name}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var tower = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Tower");
                if (tower == null) { report.Add($"{name}: no Tower object"); continue; }
                int n = MakeMovable(tower.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add($"{name}: cleared Static on {n} objects");
            }
            var battle = $"{SceneDir}/{GameFlow.SceneBattle}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(battle) != null) EditorSceneManager.OpenScene(battle);
            Debug.Log("[Archer VR] Tower collapse fix: " + string.Join(" | ", report));
        }

        [MenuItem("Archer VR/Convert Built-in Materials to URP", priority = 40)]
        public static void ConvertBuiltinMaterialsToURP()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") || path.StartsWith(Root)) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null) continue;
                var s = m.shader.name;
                if (s != "Standard" && s != "Standard (Specular setup)" && !s.StartsWith("Legacy Shaders/")) continue;

                var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                var col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                var nrm = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                var met = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
                var emi = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
                bool cutout = m.HasProperty("_Mode") && Mathf.Approximately(m.GetFloat("_Mode"), 1f);

                m.shader = lit;
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", col);
                if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
                if (met != null) { m.SetTexture("_MetallicGlossMap", met); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                if (emi != null) { m.SetTexture("_EmissionMap", emi); m.EnableKeyword("_EMISSION"); }
                if (cutout) { m.SetFloat("_AlphaClip", 1f); m.EnableKeyword("_ALPHATEST_ON"); }
                EditorUtility.SetDirty(m);
                n++;
            }
            if (n > 0) Debug.Log($"[Archer VR] Converted {n} built-in materials to URP Lit.");
        }

        // ------------------------------------------------------------------ helpers

        static void EnsureFolders()
        {
            foreach (var p in new[] { Root, PrefabDir, MatDir, AnimDir, SceneDir })
            {
                if (AssetDatabase.IsValidFolder(p)) continue;
                var parent = Path.GetDirectoryName(p).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(p));
            }
        }

        static Material Mat(string name, Color color, bool unlit = false, Texture tex = null, Vector2? tiling = null)
        {
            var path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static T LoadPrefab<T>(string name) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
            return go != null ? go.GetComponent<T>() : null;
        }

        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ------------------------------------------------------------------ small prefabs

        static GameObject BuildHitMarker()
        {
            var go = new GameObject("HitMarker");
            Prim(PrimitiveType.Sphere, "Glow", go.transform, Vector3.zero, Vector3.one * 0.6f, Mat("HitMarker", new Color(1f, 0.25f, 0.1f), unlit: true));
            return SavePrefab(go, "HitMarker");
        }

        static Arrow BuildArrow()
        {
            var go = new GameObject("Arrow");
            var shaft = Mat("ArrowShaft", new Color(0.45f, 0.3f, 0.18f));
            var tip = Mat("ArrowTip", new Color(0.6f, 0.6f, 0.65f));
            var fletch = Mat("ArrowFletch", new Color(0.85f, 0.85f, 0.8f));
            var s = Prim(PrimitiveType.Cylinder, "Shaft", go.transform, new Vector3(0, 0, -0.3f), new Vector3(0.015f, 0.35f, 0.015f), shaft);
            s.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var t = Prim(PrimitiveType.Cube, "Tip", go.transform, new Vector3(0, 0, 0.06f), new Vector3(0.03f, 0.03f, 0.08f), tip);
            t.transform.localRotation = Quaternion.Euler(0, 0, 45);
            Prim(PrimitiveType.Cube, "Fletch", go.transform, new Vector3(0, 0, -0.6f), new Vector3(0.002f, 0.06f, 0.1f), fletch);

            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 2; col.radius = 0.03f; col.height = 0.75f; col.center = new Vector3(0, 0, -0.27f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            go.AddComponent<Arrow>();
            return SavePrefab(go, "Arrow").GetComponent<Arrow>();
        }

        static void BuildBow(Arrow arrow)
        {
            var go = new GameObject("Bow");
            var wood = Mat("BowWood", new Color(0.35f, 0.22f, 0.12f));
            var str = Mat("BowString", new Color(0.9f, 0.9f, 0.85f), unlit: true);

            // Look for a bow mesh in the medieval weapons pack; otherwise build a simple one.
            GameObject model = null;
            var bowGuid = AssetDatabase.FindAssets("bow t:Model").Concat(AssetDatabase.FindAssets("bow t:Prefab"))
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => !p.StartsWith(Root) && !p.Contains("Samples/") && Path.GetFileNameWithoutExtension(p).ToLower().Contains("bow") && !Path.GetFileNameWithoutExtension(p).ToLower().Contains("elbow") && !Path.GetFileNameWithoutExtension(p).ToLower().Contains("crossbow"));
            if (bowGuid != null)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(bowGuid);
                model = (GameObject)Object.Instantiate(src);
                model.name = "Model";
                foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (var r in model.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(r);
                model.transform.SetParent(go.transform, false);
                var b = RendererBounds(model);
                float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (longest > 0.01f) model.transform.localScale *= 1.2f / longest;
                model.transform.localPosition -= RendererBounds(model).center - go.transform.position;
            }
            else
            {
                model = new GameObject("Model");
                model.transform.SetParent(go.transform, false);
                Prim(PrimitiveType.Cylinder, "Grip", model.transform, Vector3.zero, new Vector3(0.04f, 0.08f, 0.04f), wood);
                var up = Prim(PrimitiveType.Cylinder, "UpperLimb", model.transform, new Vector3(0, 0.33f, -0.06f), new Vector3(0.025f, 0.28f, 0.025f), wood);
                up.transform.localRotation = Quaternion.Euler(-20, 0, 0);
                var lo = Prim(PrimitiveType.Cylinder, "LowerLimb", model.transform, new Vector3(0, -0.33f, -0.06f), new Vector3(0.025f, 0.28f, 0.025f), wood);
                lo.transform.localRotation = Quaternion.Euler(20, 0, 0);
                Prim(PrimitiveType.Cylinder, "String", model.transform, new Vector3(0, 0, -0.2f), new Vector3(0.004f, 0.58f, 0.004f), str);
            }

            var grip = new GameObject("Grip").transform; grip.SetParent(go.transform, false);
            var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(go.transform, false);
            muzzle.localPosition = new Vector3(0, 0, 0.15f);

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.08f, 1.1f, 0.25f);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = true;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.attachTransform = grip;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;

            var line = new GameObject("AimArc").AddComponent<LineRenderer>();
            line.transform.SetParent(go.transform, false);
            line.widthMultiplier = 0.01f;
            line.sharedMaterial = Mat("AimArc", new Color(1f, 0.9f, 0.4f), unlit: true);
            line.useWorldSpace = true;

            var bow = go.AddComponent<Bow>();
            bow.arrowPrefab = arrow;
            bow.muzzle = muzzle;
            bow.aimLine = line;
            SavePrefab(go, "Bow");
        }

        // ------------------------------------------------------------------ enemies

        struct TierStats { public float hp, speed, dmg, interval, height, radius; public Color placeholder; }

        static TierStats Stats(EnemyTier t)
        {
            switch (t)
            {
                case EnemyTier.Advanced: return new TierStats { hp = 90, speed = 1.7f, dmg = 10, interval = 2f, height = 3.2f, radius = 0.8f, placeholder = new Color(0.8f, 0.5f, 0.1f) };
                case EnemyTier.Boss: return new TierStats { hp = 600, speed = 1.1f, dmg = 30, interval = 2.5f, height = 8f, radius = 1.8f, placeholder = new Color(0.5f, 0.1f, 0.5f) };
                default: return new TierStats { hp = 30, speed = 2.2f, dmg = 4, interval = 1.5f, height = 2.7f, radius = 0.6f, placeholder = new Color(0.75f, 0.75f, 0.7f) };
            }
        }

        static string FindModel(string[] terms)
        {
            foreach (var term in terms)
            {
                var candidates = AssetDatabase.FindAssets(term + " t:Prefab").Concat(AssetDatabase.FindAssets(term + " t:Model"))
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => !p.StartsWith(Root) && !p.Contains("/Samples/") && p.ToLower().Contains(term))
                    .Distinct()
                    .ToList();
                foreach (var p in candidates.OrderBy(p => p.EndsWith(".prefab") ? 0 : 1).ThenBy(p => p.Length))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go != null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return p;
                }
            }
            return null;
        }

        static void BuildEnemy(EnemyTier tier, string[] search)
        {
            var st = Stats(tier);
            var name = "Enemy_" + tier;
            var go = new GameObject(name);

            var modelPath = FindModel(search);
            GameObject model;
            if (modelPath != null)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                model = (GameObject)Object.Instantiate(src);
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var j in model.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
                foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
                foreach (var a in model.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(a);
                foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                model.transform.SetParent(go.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                var b = RendererBounds(model);
                if (b.size.y > 0.01f) model.transform.localScale *= st.height / b.size.y;
                b = RendererBounds(model);
                model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
                SetupAnimator(model, modelPath, tier);
                Debug.Log($"[Archer VR] {name} uses {modelPath}");
            }
            else
            {
                model = new GameObject("Model");
                model.transform.SetParent(go.transform, false);
                var mat = Mat("Placeholder_" + tier, st.placeholder);
                Prim(PrimitiveType.Capsule, "Body", model.transform, new Vector3(0, st.height / 2f, 0), new Vector3(st.height * 0.35f, st.height / 2f, st.height * 0.35f), mat);
                Prim(PrimitiveType.Cube, "Weapon", model.transform, new Vector3(st.height * 0.25f, st.height * 0.55f, st.height * 0.2f), new Vector3(0.08f, 0.08f, 0.6f) * st.height / 1.8f, Mat("Placeholder_Weapon", new Color(0.3f, 0.3f, 0.3f)));
                Debug.LogWarning($"[Archer VR] No model found for {tier} ({string.Join(", ", search)}). Using a placeholder capsule. Import the pack and run Archer VR > Build Everything again.");
            }
            model.name = "Model";

            var agent = go.AddComponent<NavMeshAgent>();
            agent.speed = st.speed;
            agent.radius = st.radius;
            agent.height = st.height;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.3f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var cap = go.AddComponent<CapsuleCollider>();
            cap.height = st.height;
            cap.radius = st.radius * 1.1f;
            cap.center = new Vector3(0, st.height / 2f, 0);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var src2 = go.AddComponent<AudioSource>();
            src2.spatialBlend = 1f;
            src2.playOnAwake = false;

            var health = go.AddComponent<Health>();
            health.maxHealth = st.hp;

            var ec = go.AddComponent<EnemyController>();
            ec.tier = tier;
            ec.moveSpeed = st.speed;
            ec.attackDamage = st.dmg;
            ec.attackInterval = st.interval;
            ec.model = model.transform;

            SavePrefab(go, name);
        }

        static readonly Dictionary<string, string[]> ClipKeywords = new Dictionary<string, string[]>
        {
            { "Walk",   new[] { "walk", "run", "move", "forward", "march" } },
            { "Attack", new[] { "attack", "hit", "punch", "slash", "swing", "strike", "smash" } },
            { "Die",    new[] { "death", "die", "dead", "dying" } },
            { "Idle",   new[] { "idle", "stand", "wait" } },
        };

        static void SetupAnimator(GameObject model, string modelPath, EnemyTier tier)
        {
            var anim = model.GetComponentInChildren<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();

            // Look for clips anywhere in the pack's top-level folder.
            var parts = modelPath.Split('/');
            var packRoot = parts.Length > 2 ? parts[0] + "/" + parts[1] : "Assets";
            var clips = new List<AnimationClip>();
            foreach (var g in AssetDatabase.FindAssets("t:AnimationClip", new[] { packRoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__") && !clips.Contains(c)) clips.Add(c);
            }
            if (clips.Count == 0)
            {
                Debug.LogWarning($"[Archer VR] {tier}: no animation clips found in {packRoot}. Enemy will use simple procedural motion.");
                return;
            }

            var picks = new Dictionary<string, AnimationClip>();
            foreach (var kv in ClipKeywords)
            {
                AnimationClip best = null;
                foreach (var kw in kv.Value)
                {
                    best = clips.Where(c => c.name.ToLower().Contains(kw)).OrderBy(c => c.name.Length).FirstOrDefault();
                    if (best != null) break;
                }
                if (best != null) picks[kv.Key] = best;
            }
            if (!picks.ContainsKey("Walk"))
            {
                Debug.LogWarning($"[Archer VR] {tier}: found {clips.Count} clips but none look like walking ({string.Join(", ", clips.Select(c => c.name).Take(12))}).");
                return;
            }

            var ctrlPath = $"{AnimDir}/Enemy_{tier}.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var key in new[] { "Walk", "Idle", "Attack", "Die" })
            {
                if (!picks.TryGetValue(key, out var clip)) continue;
                var s = sm.AddState(key);
                s.motion = clip;
                if (key == "Walk") sm.defaultState = s;
            }
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            Debug.Log($"[Archer VR] {tier} animations: " + string.Join(", ", picks.Select(kv => kv.Key + "=" + kv.Value.name)));
        }

        // ------------------------------------------------------------------ scenes

        enum Beat { Exposition, Battle, Resolution }

        class Layout
        {
            public XROrigin rig;
            public GameObject ground;
            public Transform tower;
            public Bounds towerBounds;
            public Vector3 center;      // tower centre at ground level
            public float radius;        // tower footprint radius
            public float groundY;
            public Vector3 front;       // horizontal direction the player faces out from
            public Transform platform;
            public Vector3 topStand;    // where the player stands on top
        }

        static string BuildScene(string sceneName, Beat beat)
        {
            var path = $"{SceneDir}/{sceneName}.unity";
            AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset(BaseScene, path))
                throw new System.Exception("Could not copy " + BaseScene);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var L = PrepareLayout();
            SetSky(beat);
            AddNavMesh(L.center);
            EnsureEventSystem();
            var flow = new GameObject("GameFlow").AddComponent<GameFlow>();

            switch (beat)
            {
                case Beat.Exposition: BuildExposition(L, flow); break;
                case Beat.Battle: BuildBattle(L, flow); break;
                case Beat.Resolution: BuildResolution(L, flow); break;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return path;
        }

        static Layout PrepareLayout()
        {
            var L = new Layout();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            L.rig = roots.Select(r => r.GetComponentInChildren<XROrigin>()).FirstOrDefault(x => x != null);
            if (L.rig == null)
            {
                var rp = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
                L.rig = ((GameObject)PrefabUtility.InstantiatePrefab(rp)).GetComponent<XROrigin>();
            }

            L.ground = roots.FirstOrDefault(r => r.name == "Ground");
            var pieces = roots.Where(r => TowerPrefixes.Any(p => r.name.StartsWith(p))).ToList();
            if (pieces.Count == 0) throw new System.Exception("No Tower_/Floor_ objects found in " + BaseScene);

            // The ground goes at the foot of the lowest Tower_ piece; stray pieces far below are left alone.
            var towerOnly = pieces.Where(p => p.name.StartsWith("Tower_")).ToList();
            if (towerOnly.Count == 0) towerOnly = pieces;
            float baseY = towerOnly.Min(p => RendererBounds(p).min.y);
            pieces = pieces.Where(p => RendererBounds(p).min.y >= baseY - 0.5f).ToList();

            var tb = RendererBounds(pieces[0]);
            foreach (var p in pieces) tb.Encapsulate(RendererBounds(p));
            L.towerBounds = tb;
            L.radius = Mathf.Max(tb.extents.x, tb.extents.z);
            L.groundY = baseY;
            L.center = new Vector3(tb.center.x, L.groundY, tb.center.z);

            var tower = new GameObject("Tower");
            tower.transform.position = L.center;
            foreach (var p in pieces)
            {
                p.transform.SetParent(tower.transform, true);
                foreach (var mf in p.GetComponentsInChildren<MeshFilter>())
                    if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>();
            }
            MakeMovable(tower.transform);
            L.tower = tower.transform;

            // Face out toward the arch if the team placed one, otherwise +Z.
            var entrance = roots.FirstOrDefault(r => r.name.StartsWith("Entrance"));
            var f = entrance != null ? entrance.transform.position - L.center : Vector3.forward;
            f.y = 0f;
            L.front = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;

            // Ground: grass, big enough for the battlefield.
            if (L.ground == null)
            {
                L.ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                L.ground.name = "Ground";
                L.ground.transform.position = L.center;
            }
            L.ground.transform.position = L.center;
            L.ground.transform.localScale = new Vector3(30f, 1f, 30f);
            var grass = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTex);
            L.ground.GetComponent<Renderer>().sharedMaterial = Mat("Ground_Grass", grass != null ? Color.white : new Color(0.3f, 0.45f, 0.2f), tex: grass, tiling: new Vector2(60, 60));

            // Battle platform on the top of the tower; it is part of the tower so it sinks too.
            float topY = tb.max.y;
            var plat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plat.name = "BattlePlatform";
            plat.transform.SetParent(tower.transform, true);
            float platR = Mathf.Max(1.5f, L.radius * 0.8f);
            plat.transform.position = new Vector3(L.center.x, topY + 0.05f, L.center.z);
            plat.transform.localScale = new Vector3(platR * 2f, 0.05f, platR * 2f);
            // A flattened cylinder's CapsuleCollider becomes a sphere the player spawns inside
            // and falls through. Use a solid slab instead: top flush with the visual, 30 cm thick.
            Object.DestroyImmediate(plat.GetComponent<Collider>());
            var slab = plat.AddComponent<BoxCollider>();
            slab.center = new Vector3(0f, -2f, 0f);   // local units (y scale 0.05)
            slab.size = new Vector3(1f, 6f, 1f);      // spans -0.25 m .. +0.05 m
            var stone = AssetDatabase.FindAssets("t:Material", new[] { "Assets/StoneKeep" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(m => m != null && m.name.ToLower().Contains("stone"));
            if (stone != null) plat.GetComponent<Renderer>().sharedMaterial = stone;
            L.platform = plat.transform;
            L.topStand = new Vector3(L.center.x, topY + 0.1f, L.center.z);
            return L;
        }

        static void SetSky(Beat beat)
        {
            var path = beat == Beat.Exposition ? SkyExposition : beat == Beat.Battle ? SkyBattle : SkyResolution;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 260f;
            RenderSettings.fogColor = beat == Beat.Battle ? new Color(0.55f, 0.42f, 0.38f) : new Color(0.7f, 0.75f, 0.8f);
        }

        static void AddNavMesh(Vector3 center)
        {
            var go = new GameObject("NavMesh");
            var s = go.AddComponent<NavMeshSurface>();
            // Bake only the battlefield around the tower to keep load times short.
            s.collectObjects = CollectObjects.Volume;
            s.center = center + Vector3.up * 20f;
            s.size = new Vector3(230f, 80f, 230f);
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            s.layerMask = ~(1 << 2); // skip Ignore Raycast (the XR rig)
            go.AddComponent<RuntimeNavMesh>();
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<XRUIInputModule>();
        }

        static void PlaceRig(Layout L, Vector3 pos, Vector3 lookDir)
        {
            lookDir.y = 0f;
            L.rig.transform.position = pos;
            L.rig.transform.rotation = Quaternion.LookRotation(lookDir.sqrMagnitude > 0.01f ? lookDir : Vector3.forward);
        }

        // Stand point lives under the tower (so it sinks with collapses); the safety net
        // returns the player there if they ever fall off or through the top.
        static void AddSafetyNet(Layout L)
        {
            var stand = new GameObject("PlayerStandPoint").transform;
            stand.SetParent(L.tower, true);
            stand.position = L.topStand;
            var net = new GameObject("PlayerSafetyNet").AddComponent<PlayerSafetyNet>();
            net.playerRig = L.rig.transform;
            net.standPoint = stand;
        }

        static void SetLocomotion(XROrigin rig, bool move, bool teleport)
        {
            foreach (var mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var n = mb.GetType().Name;
                if (n.Contains("MoveProvider")) mb.enabled = move;
                if (n == "TeleportationProvider") mb.enabled = teleport;
            }
        }

        static Transform SpawnBowRack(Vector3 pos, Quaternion rot, Transform parent)
        {
            var rack = new GameObject("BowRack").transform;
            rack.SetParent(parent, true);
            rack.SetPositionAndRotation(pos, rot);
            var wood = Mat("BowWood", new Color(0.35f, 0.22f, 0.12f));
            Prim(PrimitiveType.Cube, "Post", rack, new Vector3(0, 0.45f, 0), new Vector3(0.1f, 0.9f, 0.1f), wood, keepCollider: true);
            Prim(PrimitiveType.Cube, "Peg", rack, new Vector3(0, 0.95f, 0.08f), new Vector3(0.3f, 0.04f, 0.04f), wood);

            var bowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Bow.prefab");
            if (bowPrefab != null)
            {
                var bow = (GameObject)PrefabUtility.InstantiatePrefab(bowPrefab);
                bow.transform.SetParent(rack, false);
                bow.transform.localPosition = new Vector3(0, 1.1f, 0.15f);
                // The rack faces the player, so turn the bow round to point out at the battlefield.
                bow.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                bow.transform.SetParent(parent, true);
            }
            return rack;
        }

        static Canvas WorldCanvas(string name, Vector3 pos, Vector3 faceDir, Vector2 size, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, true);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * 0.002f;
            rt.position = pos;
            faceDir.y = 0f;
            rt.rotation = Quaternion.LookRotation(faceDir);
            var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(go.transform, false);
            bg.color = new Color(0.08f, 0.06f, 0.05f, 0.75f);
            Stretch(bg.rectTransform);
            return c;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static Text AddText(Transform parent, string text, int size, Vector2 anchorMin, Vector2 anchorMax)
        {
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = UIFont;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.95f, 0.9f, 0.8f);
            t.text = text;
            t.rectTransform.anchorMin = anchorMin;
            t.rectTransform.anchorMax = anchorMax;
            t.rectTransform.offsetMin = new Vector2(20, 10);
            t.rectTransform.offsetMax = new Vector2(-20, -10);
            return t;
        }

        static void AddButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, UnityAction action)
        {
            var go = new GameObject(label + " Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.45f, 0.28f, 0.12f, 1f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(15, 10); rt.offsetMax = new Vector2(-15, -10);
            var btn = go.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(btn.onClick, action);
            AddText(go.transform, label, 44, Vector2.zero, Vector2.one);
        }

        static GameObject Label3D(string text, Vector3 pos, Vector3 faceDir, Transform parent, float charSize = 0.08f)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            faceDir.y = 0f;
            go.transform.rotation = Quaternion.LookRotation(faceDir);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = UIFont;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.9f, 0.6f);
            go.GetComponent<MeshRenderer>().sharedMaterial = UIFont.material;
            return go;
        }

        // S1 — Exposition: start on the ground facing the tower, the army visible beyond it.
        // Walk (left stick) to the glowing circle at the tower door to climb to the top (S2).
        static void BuildExposition(Layout L, GameFlow flow)
        {
            var f = L.front;
            var start = L.center + f * (L.radius + 9f);
            PlaceRig(L, start, -f);
            SetLocomotion(L.rig, move: true, teleport: true);

            var ta = L.ground.AddComponent<TeleportationArea>();
            ta.interactionLayers = InteractionLayerMask.GetMask("Teleport");

            var env = new GameObject("S1 Exposition").transform;

            // "Climb the tower": a glowing anchor at the tower door that takes you to the top (S2).
            var doorPos = L.center + f * (L.radius + 1.5f);
            var anchor = new GameObject("Climb Tower Anchor");
            anchor.transform.SetParent(env, true);
            anchor.transform.position = doorPos;
            anchor.transform.rotation = Quaternion.LookRotation(f);
            var box = anchor.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 0.1f, 2f);
            var tan = anchor.AddComponent<TeleportationAnchor>();
            tan.interactionLayers = InteractionLayerMask.GetMask("Teleport");
            tan.teleportAnchorTransform = anchor.transform;
            Prim(PrimitiveType.Cylinder, "Glow", anchor.transform, new Vector3(0, 0.03f, 0), new Vector3(2f, 0.02f, 2f), Mat("AnchorGlow", new Color(1f, 0.8f, 0.3f), unlit: true));
            var zone = anchor.AddComponent<SceneLoadZone>();
            zone.sceneName = GameFlow.SceneBattle;
            zone.radius = 1.2f;
            Label3D("Walk onto the glowing circle\nto climb the tower", doorPos + Vector3.up * 2.6f, -f, env);

            // The army: beyond the tower and off to one side so it is visible from the start,
            // marching in and halting (not attacking yet).
            var basic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Enemy_Basic.prefab");
            var adv = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Enemy_Advanced.prefab");
            var army = new GameObject("Approaching Army").transform;
            army.SetParent(env, true);
            var a = Quaternion.Euler(0f, 35f, 0f) * -f;          // direction from tower to army
            var aRight = Vector3.Cross(Vector3.up, a);
            int rows = 4, cols = 8;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var prefab = (r == 0 && (c == 1 || c == 6) && adv != null) ? adv : basic;
                    if (prefab == null) continue;
                    var offset = aRight * ((c - (cols - 1) / 2f) * 3.5f) + a * (r * 3.5f);
                    var p = L.center + a * 95f + offset;
                    var e = (GameObject)PrefabUtility.InstantiatePrefab(prefab, army);
                    e.transform.position = p;
                    e.transform.rotation = Quaternion.LookRotation(-a);
                    var ec = e.GetComponent<EnemyController>();
                    ec.marchOnStart = true;
                    ec.canAttack = false;
                    ec.moveSpeed = 1f;
                    ec.haltPoint = L.center + a * 55f + offset;
                }
        }

        // S2 + S3 — Rising Action, Climax (boss), Falling Action (stragglers), all on the tower top.
        static void BuildBattle(Layout L, GameFlow flow)
        {
            var f = L.front;
            PlaceRig(L, L.topStand, f);
            SetLocomotion(L.rig, move: false, teleport: false);
            L.rig.transform.SetParent(null, true);
            AddSafetyNet(L);

            var env = new GameObject("S2-S3 Battle").transform;

            var th = L.tower.gameObject.AddComponent<TowerHealth>();
            th.maxHealth = 1200f;
            th.towerVisualRoot = L.tower;
            th.playerRig = L.rig.transform;
            th.collapseStepHeight = Mathf.Clamp(L.towerBounds.size.y * 0.15f, 1.5f, 4f);
            th.hitMarkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/HitMarker.prefab");
            th.rubblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StoneKeep/Prefabs/Rubble.prefab");

            var right = Vector3.Cross(Vector3.up, f);
            // Bow rack right in front of the player at waist height, bow pointing at the battlefield.
            SpawnBowRack(L.topStand + f * 0.55f, Quaternion.LookRotation(-f), L.tower);

            // Spawn points in a wide ring; weighted to the front so the action is where the player looks first.
            var spawns = new List<Transform>();
            var spRoot = new GameObject("Spawn Points").transform;
            spRoot.SetParent(env, true);
            for (int i = 0; i < 10; i++)
            {
                float ang = (i < 6 ? (i - 2.5f) * 22f : 180f + (i - 7.5f) * 35f);
                var dir = Quaternion.Euler(0, ang, 0) * f;
                var t = new GameObject("Spawn " + (i + 1)).transform;
                t.SetParent(spRoot, true);
                t.position = L.center + dir * 70f;
                spawns.Add(t);
            }

            var basic = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Enemy_Basic.prefab");
            var adv = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Enemy_Advanced.prefab");
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Enemy_Boss.prefab");
            WaveEntry E(GameObject p, int n) => new WaveEntry { enemyPrefab = p, count = n };

            var music = new GameObject("Music").AddComponent<AudioSource>();
            music.transform.SetParent(env, true);
            music.playOnAwake = false;

            var wm = new GameObject("WaveManager").AddComponent<WaveManager>();
            wm.transform.SetParent(env, true);
            wm.tower = th;
            wm.spawnPoints = spawns.ToArray();
            wm.attackRingRadius = L.radius + 2.5f;
            wm.musicSource = music;
            wm.waves = new[]
            {
                new Wave { name = "Wave 1 - Scouts",        entries = new[] { E(basic, 10) },                         spawnInterval = 1.0f },
                new Wave { name = "Wave 2 - The horde",     entries = new[] { E(basic, 16), E(adv, 2) },              spawnInterval = 0.8f },
                new Wave { name = "Wave 3 - Heavy infantry",entries = new[] { E(basic, 18), E(adv, 5) },              spawnInterval = 0.7f },
                new Wave { name = "Wave 4 - Full assault",  entries = new[] { E(basic, 24), E(adv, 8) },              spawnInterval = 0.55f },
                new Wave { name = "Wave 5 - BOSS",          entries = new[] { E(boss, 1), E(basic, 20), E(adv, 6) },  spawnInterval = 0.6f, isBossWave = true },
                new Wave { name = "Stragglers",             entries = new[] { E(basic, 8) },                          spawnInterval = 1.2f },
            };

            // Status board just left of where the player faces.
            var hudPos = L.topStand + f * 1.6f - right * 0.9f + Vector3.up * 1.55f;
            var hud = WorldCanvas("Battle HUD", hudPos, f, new Vector2(780, 320), env);
            hud.transform.SetParent(L.tower, true);
            var hudText = AddText(hud.transform, "Enemies approaching...", 52, Vector2.zero, Vector2.one);
            var bh = hud.gameObject.AddComponent<BattleHUD>();
            bh.waves = wm; bh.tower = th; bh.label = hudText;

            // Defeat panel (story map S3-ALT): start over or quit.
            var defeat = WorldCanvas("Defeat Panel", L.topStand + f * 2f + Vector3.up * 1.5f, f, new Vector2(900, 500), env);
            defeat.transform.SetParent(L.tower, true);
            AddText(defeat.transform, "The tower has fallen.", 70, new Vector2(0, 0.55f), new Vector2(1, 1));
            AddButton(defeat.transform, "Try Again", new Vector2(0.03f, 0.1f), new Vector2(0.35f, 0.45f), flow.RestartBattle);
            AddButton(defeat.transform, "Start Over", new Vector2(0.35f, 0.1f), new Vector2(0.65f, 0.45f), flow.StartOver);
            AddButton(defeat.transform, "Quit", new Vector2(0.65f, 0.1f), new Vector2(0.97f, 0.45f), flow.Quit);
            flow.defeatPanel = defeat.gameObject;
        }

        // S4 — Resolution: calm dawn, the battlefield is empty, celebrate and replay.
        static void BuildResolution(Layout L, GameFlow flow)
        {
            var f = L.front;
            PlaceRig(L, L.topStand, f);
            SetLocomotion(L.rig, move: false, teleport: false);
            AddSafetyNet(L);

            var env = new GameObject("S4 Resolution").transform;
            var panel = WorldCanvas("Victory Panel", L.topStand + f * 2f + Vector3.up * 1.5f, f, new Vector2(900, 520), env);
            AddText(panel.transform, "Victory!\nThe kingdom is safe.", 66, new Vector2(0, 0.5f), new Vector2(1, 1));
            AddButton(panel.transform, "Play Again", new Vector2(0.05f, 0.1f), new Vector2(0.5f, 0.42f), flow.StartOver);
            AddButton(panel.transform, "Quit", new Vector2(0.5f, 0.1f), new Vector2(0.95f, 0.42f), flow.Quit);

            // Rubble left over from the siege.
            var rubble = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StoneKeep/Prefabs/Rubble.prefab");
            if (rubble != null)
                for (int i = 0; i < 6; i++)
                {
                    var r = (GameObject)PrefabUtility.InstantiatePrefab(rubble, env);
                    r.transform.position = L.center + Quaternion.Euler(0, i * 60f + 15f, 0) * f * (L.radius + 3f + (i % 3));
                    r.transform.rotation = Quaternion.Euler(0, i * 47f, 0);
                }
        }
    }
}
