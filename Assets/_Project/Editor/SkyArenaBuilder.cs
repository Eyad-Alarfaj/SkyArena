using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using SkyArena.AI;
using SkyArena.Combat;
using SkyArena.Core;
using SkyArena.Flight;
using SkyArena.Inputs;
using SkyArena.Networking;
using SkyArena.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyArena.EditorTools
{
    /// <summary>
    /// Generates the entire playable prototype from code: placeholder
    /// materials, the networked plane and missile prefabs, the arena, and the
    /// touch HUD.
    ///
    /// Everything is rebuilt deterministically, so the project can be wiped
    /// back to just these scripts and regenerated with one menu click. It also
    /// runs itself automatically the first time the scripts compile in a
    /// project where the arena scene does not exist yet, which is what makes
    /// "open the project and press Play" work with no manual setup.
    /// </summary>
    public static class SkyArenaBuilder
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string ArtFolder = ProjectRoot + "/Art";
        private const string ResourcesFolder = ProjectRoot + "/Resources";
        private const string ScenesFolder = ProjectRoot + "/Scenes";

        private const string ArenaScenePath = ScenesFolder + "/Arena.unity";
        private const string PlayerPrefabPath = ResourcesFolder + "/PlayerPlane.prefab";
        private const string MissilePrefabPath = ResourcesFolder + "/Missile.prefab";
        private const string BotPrefabPath = ResourcesFolder + "/EnemyBot.prefab";
        private const string ExplosionPrefabPath = ArtFolder + "/ExplosionFx.prefab";

        private const string LegacyPlayerPrefab = "Assets/Resources/PlayerJet.prefab";

        /// <summary>Placeholder materials shared by every generated object.</summary>
        private class ArenaMaterials
        {
            public Material Ground;
            public Material Ridge;
            public Material Block;
            public Material PlaneLocal;
            public Material PlaneRemote;
            public Material Bot;
            public Material Missile;
            public Material Tracer;
            public Material Explosion;
        }

        // ------------------------------------------------------------- Entry

        [MenuItem("SkyArena/Build Everything (Prefabs + Arena)", false, 0)]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SkyArena] Stop Play mode before rebuilding the arena.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(ArtFolder);
            EnsureFolder(ResourcesFolder);
            EnsureFolder(ScenesFolder);

            ArenaMaterials materials = CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject explosionPrefab = BuildExplosionPrefab(materials);
            BuildMissilePrefab(materials, explosionPrefab);
            BuildPlayerPrefab(materials);
            BuildBotPrefab(materials);

            ConfigureLighting();
            ConfigureCameraAndLight();
            BuildEnvironment(materials);
            BuildSpawnPoints();

            HudReferences hud = SkyArenaUiFactory.Build();
            WireSceneSystems(hud);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ArenaScenePath);

            RemoveLegacyAssets();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ArenaScenePath, true) };
            ConfigurePlayerSettings();
            PhotonEditor.UpdateRpcList();

            Debug.Log("[SkyArena] Build complete. Arena scene is open — press Play to fly.");
            Validate();
        }

        [MenuItem("SkyArena/Open Arena Scene", false, 20)]
        public static void OpenArena()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaScenePath) == null)
            {
                BuildAll();
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// Builds the prototype automatically the first time these scripts
        /// compile in a project that has no arena scene yet.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void HookAutoBuild()
        {
            EditorApplication.delayCall += TryAutoBuild;

            // A recompile triggered by pressing Play reloads the domain while
            // isPlayingOrWillChangePlaymode is already true, so the check below
            // refuses to run and would otherwise never get another chance.
            // Retrying on the way back to edit mode covers that path.
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryAutoBuild;
        }

        /// <summary>
        /// Builds the prototype when anything the current scripts expect is
        /// absent. This is also the upgrade path: adding a component to a
        /// generated prefab makes the old asset stale, and regenerating beats
        /// leaving a plane that silently ignores the stick.
        /// </summary>
        private static void TryAutoBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool sceneMissing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaScenePath) == null;
            bool botMissing = AssetDatabase.LoadAssetAtPath<GameObject>(BotPrefabPath) == null;
            if (!sceneMissing && !botMissing) return;

            Debug.Log("[SkyArena] Generated assets are missing or out of date - rebuilding now.");
            BuildAll();
        }

        // --------------------------------------------------------- Validation

        [MenuItem("SkyArena/Validate Setup", false, 21)]
        public static void Validate()
        {
            List<string> problems = new List<string>();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                problems.Add($"Missing player prefab at {PlayerPrefabPath}");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(MissilePrefabPath) == null)
                problems.Add($"Missing missile prefab at {MissilePrefabPath}");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(BotPrefabPath) == null)
                problems.Add($"Missing bot prefab at {BotPrefabPath}");

            if (Object.FindAnyObjectByType<BotSpawner>() == null) problems.Add("Scene has no BotSpawner");
            if (Object.FindAnyObjectByType<GameManager>() == null) problems.Add("Scene has no GameManager");
            if (Object.FindAnyObjectByType<PhotonLauncher>() == null) problems.Add("Scene has no PhotonLauncher");
            if (Object.FindAnyObjectByType<MobileInputController>() == null) problems.Add("Scene has no MobileInputController");
            if (Object.FindAnyObjectByType<CameraFollow>() == null) problems.Add("Scene has no chase camera");
            if (Object.FindAnyObjectByType<HUDController>() == null) problems.Add("Scene has no HUD");
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                problems.Add("Scene has no EventSystem — touch input will not work");

            string appId = PhotonNetwork.PhotonServerSettings != null
                ? PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime
                : null;

            if (string.IsNullOrEmpty(appId))
            {
                Debug.LogWarning("[SkyArena] No Photon App ID set. The game will still run in offline solo mode.");
            }

            if (problems.Count == 0)
            {
                Debug.Log("[SkyArena] Validation passed — everything is wired up.");
                return;
            }

            Debug.LogError("[SkyArena] Validation found problems:\n - " + string.Join("\n - ", problems));
        }

        // ---------------------------------------------------------- Materials

        private static ArenaMaterials CreateMaterials()
        {
            return new ArenaMaterials
            {
                Ground = MakeMaterial("Mat_Ground", new Color(0.24f, 0.29f, 0.22f), false, 0f, 0.05f),
                Ridge = MakeMaterial("Mat_Ridge", new Color(0.33f, 0.31f, 0.29f), false, 0f, 0.05f),
                Block = MakeMaterial("Mat_Block", new Color(0.44f, 0.44f, 0.47f), false, 0.1f, 0.2f),
                PlaneLocal = MakeMaterial("Mat_PlaneLocal", new Color(0.16f, 0.47f, 0.95f), false, 0.35f, 0.6f),
                PlaneRemote = MakeMaterial("Mat_PlaneRemote", new Color(0.88f, 0.18f, 0.18f), false, 0.35f, 0.6f),
                Bot = MakeMaterial("Mat_Bot", new Color(0.95f, 0.45f, 0.05f), false, 0.3f, 0.5f),
                Missile = MakeMaterial("Mat_Missile", new Color(1f, 0.55f, 0.12f), true, 0f, 0f),
                Tracer = MakeMaterial("Mat_Tracer", new Color(1f, 0.92f, 0.4f), true, 0f, 0f),
                Explosion = MakeMaterial("Mat_Explosion", new Color(1f, 0.6f, 0.15f), true, 0f, 0f)
            };
        }

        private static Material MakeMaterial(string name, Color color, bool unlit, float metallic, float smoothness)
        {
            string path = $"{ArtFolder}/{name}.mat";
            Shader shader = FindShader(unlit);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            // URP uses _BaseColor, the built-in pipeline uses _Color; set whichever exists.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);

            if (!unlit)
            {
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader FindShader(bool unlit)
        {
            Shader shader = Shader.Find(unlit
                ? "Universal Render Pipeline/Unlit"
                : "Universal Render Pipeline/Lit");

            if (shader == null) shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return shader;
        }

        // ------------------------------------------------------------ Prefabs

        private static GameObject BuildExplosionPrefab(ArenaMaterials materials)
        {
            GameObject source = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            source.name = "ExplosionFx";
            Object.DestroyImmediate(source.GetComponent<Collider>());
            source.GetComponent<Renderer>().sharedMaterial = materials.Explosion;
            source.AddComponent<ExplosionFx>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, ExplosionPrefabPath);
            Object.DestroyImmediate(source);
            return prefab;
        }

        private static void BuildMissilePrefab(ArenaMaterials materials, GameObject explosionPrefab)
        {
            GameObject root = new GameObject("Missile");
            Transform t = root.transform;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;

            // The PhotonView must exist before anything that declares
            // [RequireComponent(typeof(PhotonView))], otherwise Unity adds a
            // second one automatically and the object ends up with two views.
            PhotonView view = AddPhotonView(root);

            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shell.name = "Shell";
            Object.DestroyImmediate(shell.GetComponent<Collider>());
            shell.transform.SetParent(t, false);
            shell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shell.transform.localScale = new Vector3(0.6f, 1.6f, 0.6f);
            shell.GetComponent<Renderer>().sharedMaterial = materials.Missile;

            AddBox("Fins", t, new Vector3(0f, 0f, -1.3f), new Vector3(1.9f, 0.15f, 0.7f), materials.Missile);

            NetworkTransformSync sync = root.AddComponent<NetworkTransformSync>();
            MissileController controller = root.AddComponent<MissileController>();
            WireField(controller, "explosionPrefab", explosionPrefab);

            Observe(view, sync);

            PrefabUtility.SaveAsPrefabAsset(root, MissilePrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildPlayerPrefab(ArenaMaterials materials) => BuildAircraft(materials, false);

        private static void BuildBotPrefab(ArenaMaterials materials) => BuildAircraft(materials, true);

        /// <summary>
        /// Builds the player plane and the bot from one recipe.
        ///
        /// They are deliberately the same airframe with the same weapons and
        /// the same physics; the only differences are who is flying (HumanPilot
        /// vs AiPilot), the paint, and the fact that a bot must NOT claim the
        /// chase camera.
        /// </summary>
        private static void BuildAircraft(ArenaMaterials materials, bool isBot)
        {
            string prefabName = isBot ? "EnemyBot" : "PlayerPlane";
            string prefabPath = isBot ? BotPrefabPath : PlayerPrefabPath;
            Material paint = isBot ? materials.Bot : materials.PlaneLocal;

            GameObject root = new GameObject(prefabName);
            Transform t = root.transform;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider hull = root.AddComponent<BoxCollider>();
            hull.center = Vector3.zero;
            hull.size = new Vector3(13f, 3f, 12f);

            // Added first so that [RequireComponent(typeof(PhotonView))] on the
            // gameplay components finds an existing view instead of adding a
            // second one.
            PhotonView view = AddPhotonView(root);

            // Low-poly placeholder airframe, nose pointing down +Z.
            Renderer[] renderers =
            {
                AddBox("Fuselage", t, new Vector3(0f, 0f, 0f), new Vector3(2.2f, 1.5f, 11f), paint),
                AddBox("Nose", t, new Vector3(0f, 0f, 6.2f), new Vector3(1.2f, 0.9f, 2.6f), paint),
                AddBox("Wings", t, new Vector3(0f, 0f, 0.4f), new Vector3(13f, 0.4f, 2.8f), paint),
                AddBox("Tailplane", t, new Vector3(0f, 0f, -4.8f), new Vector3(5.4f, 0.35f, 1.7f), paint),
                AddBox("Fin", t, new Vector3(0f, 1.3f, -4.8f), new Vector3(0.35f, 2.4f, 1.9f), paint)
            };

            Transform muzzle = CreateAnchor("Muzzle", t, new Vector3(0f, 0f, 8f));
            Transform launchPoint = CreateAnchor("LaunchPoint", t, new Vector3(0f, -2.2f, 4.5f));

            LineRenderer tracer = root.AddComponent<LineRenderer>();
            tracer.sharedMaterial = materials.Tracer;
            tracer.positionCount = 2;
            tracer.useWorldSpace = true;
            tracer.startWidth = 0.45f;
            tracer.endWidth = 0.12f;
            tracer.numCapVertices = 0;
            tracer.alignment = LineAlignment.View;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracer.enabled = false;

            NetworkTransformSync sync = root.AddComponent<NetworkTransformSync>();
            HealthSystem health = root.AddComponent<HealthSystem>();
            root.AddComponent<FlightController>();
            root.AddComponent<LockOnSystem>();
            WeaponSystem weapon = root.AddComponent<WeaponSystem>();
            MissileLauncher launcher = root.AddComponent<MissileLauncher>();
            Targetable targetable = root.AddComponent<Targetable>();

            WireArray(health, "visualRenderers", renderers);
            WireArray(health, "hitColliders", new Object[] { hull });

            WireField(weapon, "muzzle", muzzle);
            WireField(weapon, "tracer", tracer);
            WireField(launcher, "launchPoint", launchPoint);

            if (isBot)
            {
                // The bot is owned by the master client, so photonView.IsMine is
                // true there. PlayerAvatar would therefore steal the host's
                // camera; BotAvatar only repaints.
                root.AddComponent<AiPilot>();
                BotAvatar botAvatar = root.AddComponent<BotAvatar>();

                WireArray(botAvatar, "bodyRenderers", renderers);
                WireField(botAvatar, "botMaterial", materials.Bot);
                WireBool(targetable, "isBot", true);
            }
            else
            {
                root.AddComponent<HumanPilot>();
                PlayerAvatar avatar = root.AddComponent<PlayerAvatar>();

                WireArray(avatar, "bodyRenderers", renderers);
                WireField(avatar, "localPlayerMaterial", materials.PlaneLocal);
                WireField(avatar, "remotePlayerMaterial", materials.PlaneRemote);
            }

            Observe(view, sync);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        private static PhotonView AddPhotonView(GameObject target)
        {
            PhotonView view = target.AddComponent<PhotonView>();
            view.Synchronization = ViewSynchronization.UnreliableOnChange;
            view.OwnershipTransfer = OwnershipOption.Fixed;
            view.ObservedComponents = new List<Component>();
            return view;
        }

        /// <summary>Registers the component whose state Photon should replicate.</summary>
        private static void Observe(PhotonView view, Component observed)
        {
            view.ObservedComponents = new List<Component> { observed };
            EditorUtility.SetDirty(view);
        }

        // -------------------------------------------------------- Environment

        private static void ConfigureLighting()
        {
            // Flat ambient means the arena is lit correctly without waiting for
            // a lightmap bake, which keeps the generated scene instantly usable.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.52f, 0.62f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.72f, 0.85f);
            RenderSettings.fogDensity = 0.00035f;
        }

        private static void ConfigureCameraAndLight()
        {
            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.gameObject.name = "Chase Camera";
                camera.tag = "MainCamera";
                camera.fieldOfView = 65f;
                camera.nearClipPlane = 0.5f;
                camera.farClipPlane = 5000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.transform.position = new Vector3(0f, 260f, -420f);
                camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

                if (camera.GetComponent<CameraFollow>() == null) camera.gameObject.AddComponent<CameraFollow>();
                if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            }

            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.96f, 0.87f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }
        }

        private static void BuildEnvironment(ArenaMaterials materials)
        {
            GameObject arena = new GameObject("Arena");
            Transform arenaRoot = arena.transform;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(arenaRoot, false);
            ground.transform.localScale = new Vector3(400f, 1f, 400f); // 4 km square
            ground.GetComponent<Renderer>().sharedMaterial = materials.Ground;

            // Fixed seed so every rebuild produces the identical arena.
            Random.InitState(20260820);

            Transform ridges = new GameObject("Boundary Ridges").transform;
            ridges.SetParent(arenaRoot, false);
            for (int i = 0; i < 64; i++)
            {
                float angle = (i / 64f) * Mathf.PI * 2f + Random.Range(-0.04f, 0.04f);
                float radius = Random.Range(1500f, 1850f);
                float height = Random.Range(140f, 420f);
                float width = Random.Range(120f, 300f);

                AddEnvironmentBox(ridges, materials.Ridge,
                    new Vector3(Mathf.Cos(angle) * radius, height * 0.5f, Mathf.Sin(angle) * radius),
                    new Vector3(width, height, width),
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }

            Transform blocks = new GameObject("Ground Clutter").transform;
            blocks.SetParent(arenaRoot, false);
            for (int i = 0; i < 70; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Mathf.Lerp(120f, 1150f, Mathf.Sqrt(Random.value));
                float height = Random.Range(18f, 65f);
                float width = Random.Range(28f, 85f);

                AddEnvironmentBox(blocks, materials.Block,
                    new Vector3(Mathf.Cos(angle) * radius, height * 0.5f, Mathf.Sin(angle) * radius),
                    new Vector3(width, height, width),
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
        }

        private static void BuildSpawnPoints()
        {
            GameObject root = new GameObject("Spawn Points");

            const int count = 8;
            const float radius = 700f;
            const float altitude = 200f;

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, altitude, Mathf.Sin(angle) * radius);

                GameObject point = new GameObject($"Spawn {i + 1}");
                point.transform.SetParent(root.transform, false);
                point.transform.position = position;
                point.transform.rotation = Quaternion.LookRotation(
                    new Vector3(-position.x, 0f, -position.z).normalized, Vector3.up);

                point.AddComponent<SpawnPoint>();
            }
        }

        // ------------------------------------------------------------ Systems

        private static void WireSceneSystems(HudReferences hud)
        {
            GameObject systems = new GameObject("Game Systems");

            systems.AddComponent<GameManager>();

            MobileInputController input = systems.AddComponent<MobileInputController>();
            WireField(input, "flightJoystick", hud.Joystick);
            WireField(input, "throttleUpButton", hud.ThrottleUp);
            WireField(input, "throttleDownButton", hud.ThrottleDown);
            WireField(input, "gunButton", hud.Gun);
            WireField(input, "missileButton", hud.Missile);

            PhotonLauncher launcher = systems.AddComponent<PhotonLauncher>();
            WireField(launcher, "statusText", hud.StatusText);

            // Bots are spawned by the master client only; see BotSpawner.
            systems.AddComponent<BotSpawner>();

            GameObject canvasObject = hud.Canvas.gameObject;

            HUDController hudController = canvasObject.AddComponent<HUDController>();
            WireField(hudController, "healthFill", hud.HealthFill);
            WireField(hudController, "healthText", hud.HealthText);
            WireField(hudController, "speedText", hud.SpeedText);
            WireField(hudController, "altitudeText", hud.AltitudeText);
            WireField(hudController, "throttleFill", hud.ThrottleFill);
            WireField(hudController, "missileCooldownFill", hud.MissileCooldown);
            WireField(hudController, "weaponStatusText", hud.WeaponStatusText);
            WireField(hudController, "deathOverlay", hud.DeathOverlay);

            LockOnIndicatorUI lockOnUi = canvasObject.AddComponent<LockOnIndicatorUI>();
            WireField(lockOnUi, "canvasRect", hud.CanvasRect);
            WireField(lockOnUi, "reticle", hud.Reticle);
            WireField(lockOnUi, "lockProgressRing", hud.LockProgressRing);
            WireArray(lockOnUi, "reticleGraphics", hud.ReticleGraphics);

            RadarController radar = canvasObject.AddComponent<RadarController>();
            WireField(radar, "radarPanel", hud.RadarPanel);
            WireField(radar, "blipTemplate", hud.BlipTemplate);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Lets a build and the Editor run side by side while testing multiplayer.
            PlayerSettings.runInBackground = true;
        }

        private static void RemoveLegacyAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyPlayerPrefab) != null)
            {
                AssetDatabase.DeleteAsset(LegacyPlayerPrefab);
            }
        }

        // ------------------------------------------------------------ Helpers

        private static Renderer AddBox(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());

            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;

            Renderer renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static void AddEnvironmentBox(
            Transform parent, Material material, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Feature";
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localRotation = rotation;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            box.isStatic = true;
        }

        private static Transform CreateAnchor(string name, Transform parent, Vector3 localPosition)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            return anchor.transform;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Assigns a [SerializeField] private reference. Reported loudly rather
        /// than failing silently, because a mistyped field name here would show
        /// up much later as an unexplained null at runtime.
        /// </summary>
        public static void WireField(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyArena] {target.GetType().Name} has no serialized field '{fieldName}'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireBool(Object target, string fieldName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyArena] {target.GetType().Name} has no serialized field '{fieldName}'.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireColor(Object target, string fieldName, Color value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyArena] {target.GetType().Name} has no serialized field '{fieldName}'.");
                return;
            }

            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireArray(Object target, string fieldName, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null || !property.isArray)
            {
                Debug.LogError($"[SkyArena] {target.GetType().Name} has no serialized array '{fieldName}'.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
