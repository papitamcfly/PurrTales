#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PurrTales.EditorTools
{
    /// <summary>
    /// Generador de la escena base de Purr-tales. Se ejecuta solo al abrir el proyecto por primera vez
    /// (PurrTalesAutoSetup) y se puede regenerar desde "Purr-tales / Reconstruir Escena de Prueba".
    /// Crea las Layers (Ground, Wall, PortalSurface, Hazard), genera sprites/materiales,
    /// arma los prefabs PortalAzul y PortalNaranja, crea al Player con todas sus referencias
    /// y construye un nivel de prueba (plataformas, chimenea de wall jump, superficies de portal,
    /// trampas, checkpoints y meta). La escena se guarda en Assets/_Juego/Scenes/PurrTales_Prueba.unity.
    /// </summary>
    public static class PurrTalesSceneBuilder
    {
        const string Root = "Assets/_Juego";
        const string GeneratedDir = Root + "/Generated";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/PurrTales_Prueba.unity";
        const string InputAssetPath = "Assets/InputSystem_Actions.inputactions";

        // Paleta (medieval / grutas arcanas)
        static readonly Color GroundColor = new Color(0.42f, 0.34f, 0.48f);
        static readonly Color WallColor = new Color(0.24f, 0.28f, 0.44f);
        static readonly Color PortalSurfaceColor = new Color(0.86f, 0.93f, 1f);
        static readonly Color HazardColor = new Color(0.95f, 0.25f, 0.32f);
        static readonly Color BackgroundColor = new Color(0.07f, 0.05f, 0.12f);
        static readonly Color BlueColor = new Color(0.2f, 0.6f, 1f);
        static readonly Color OrangeColor = new Color(1f, 0.55f, 0.1f);

        const float PortalWidth = 1.6f;
        const float PortalTriggerDepth = 0.6f;

        class Shared
        {
            public Sprite block, tile, spike, circle, cat;
            public Material spriteMat, particleMat;
            public PhysicsMaterial2D frictionless;
            public int ground, wall, portalSurface, hazard;
            public LayerMask GroundMask { get { return 1 << ground; } }
            public LayerMask WallMask { get { return 1 << wall; } }
            public LayerMask PortalMask { get { return 1 << portalSurface; } }
            public LayerMask HazardMask { get { return 1 << hazard; } }
            public LayerMask SolidMask { get { return (1 << ground) | (1 << wall) | (1 << portalSurface) | (1 << hazard); } }
            public LayerMask StandableMask { get { return (1 << ground) | (1 << wall) | (1 << portalSurface); } }
        }

        public const string GameScenePath = ScenePath;

        /// <summary>Regenera manualmente la escena de prueba (pide confirmación si ya existe).</summary>
        [MenuItem("Purr-tales/Reconstruir Escena de Prueba", false, 0)]
        public static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Purr-tales", "Sal del modo Play antes de reconstruir la escena.", "OK");
                return;
            }
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Purr-tales",
                    "Esto vuelve a generar " + ScenePath + " desde cero y se pierden los cambios hechos a mano en esa escena.\n\n¿Continuar?",
                    "Reconstruir", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(true);
        }

        [MenuItem("Purr-tales/Abrir Escena de Prueba", false, 1)]
        public static void OpenGameScene()
        {
            if (!File.Exists(ScenePath)) { Build(true); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>
        /// Genera layers, assets, prefabs y la escena de prueba, y la deja abierta.
        /// Lo llama PurrTalesAutoSetup automáticamente la primera vez que se abre el proyecto.
        /// </summary>
        public static bool Build(bool interactive)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Purr-tales", "Creando carpetas y layers...", 0.1f);
                EnsureFolders();
                Dictionary<string, int> layers = EnsureLayers();

                var s = new Shared();
                s.ground = layers[GameLayers.Ground];
                s.wall = layers[GameLayers.Wall];
                s.portalSurface = layers[GameLayers.PortalSurface];
                s.hazard = layers[GameLayers.Hazard];

                EditorUtility.DisplayProgressBar("Purr-tales", "Generando sprites y materiales...", 0.3f);
                CreateSharedAssets(s);

                EditorUtility.DisplayProgressBar("Purr-tales", "Creando prefabs de portales...", 0.5f);
                Portal bluePrefab = CreatePortalPrefab("PortalAzul", PortalColor.Blue, BlueColor, s);
                Portal orangePrefab = CreatePortalPrefab("PortalNaranja", PortalColor.Orange, OrangeColor, s);

                EditorUtility.DisplayProgressBar("Purr-tales", "Construyendo nivel de prueba...", 0.7f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject player = BuildScene(s, bluePrefab, orangePrefab);

                EditorUtility.DisplayProgressBar("Purr-tales", "Guardando escena...", 0.9f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuildSettings(ScenePath);
                AssetDatabase.SaveAssets();

                Selection.activeGameObject = player;
                EditorGUIUtility.PingObject(player);
                Debug.Log("[PurrTales] Escena del juego lista en " + ScenePath + ". Pulsa Play para probar.");
                if (interactive)
                {
                    EditorUtility.DisplayDialog("Purr-tales",
                        "Escena regenerada:\n" + ScenePath + "\n\nPulsa Play para probar.", "OK");
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PurrTales] No se pudo generar la escena: " + e);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Purr-tales/Reconstruir Escena de Prueba", true)]
        static bool ValidateRebuild()
        {
            return !EditorApplication.isPlaying;
        }

        // =====================================================================
        // Carpetas y Layers
        // =====================================================================

        static void EnsureFolders()
        {
            EnsureFolder("Assets", "_Juego");
            EnsureFolder(Root, "Generated");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Scenes");
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        static Dictionary<string, int> EnsureLayers()
        {
            var result = new Dictionary<string, int>();
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                throw new System.Exception("No se pudo abrir ProjectSettings/TagManager.asset");

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            foreach (string layerName in GameLayers.All)
            {
                int found = -1;
                for (int i = 0; i < layersProp.arraySize; i++)
                {
                    if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName) { found = i; break; }
                }
                if (found < 0)
                {
                    for (int i = 8; i < layersProp.arraySize; i++)
                    {
                        SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                        if (string.IsNullOrEmpty(sp.stringValue))
                        {
                            sp.stringValue = layerName;
                            found = i;
                            break;
                        }
                    }
                }
                if (found < 0)
                    throw new System.Exception("No hay espacios libres para crear la layer '" + layerName + "'.");
                result[layerName] = found;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return result;
        }

        // =====================================================================
        // Assets generados (sprites, materiales, física)
        // =====================================================================

        static void CreateSharedAssets(Shared s)
        {
            s.block = CreateSpriteAsset("Bloque", MakeBlockTexture(), 8f, new Vector4(2, 2, 2, 2));
            s.tile = CreateSpriteAsset("BloquePortal", MakePortalTileTexture(), 8f, Vector4.zero);
            s.spike = CreateSpriteAsset("Pincho", MakeSpikeTexture(), 16f, Vector4.zero);
            s.circle = CreateSpriteAsset("Circulo", MakeCircleTexture(32), 32f, Vector4.zero);
            s.cat = CreateSpriteAsset("Gato_Placeholder", MakeCatTexture(), 16f, Vector4.zero);

            Shader spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (spriteShader == null) spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null) spriteShader = Shader.Find("Universal Render Pipeline/Unlit");

            s.spriteMat = LoadOrCreateMaterial("PurrTales_SpriteUnlit", spriteShader, null);
            s.particleMat = LoadOrCreateMaterial("PurrTales_Particula", spriteShader, s.circle.texture);

            string pmPath = GeneratedDir + "/PurrTales_SinFriccion.physicsMaterial2D";
            s.frictionless = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(pmPath);
            if (s.frictionless == null)
            {
                s.frictionless = new PhysicsMaterial2D("PurrTales_SinFriccion");
                AssetDatabase.CreateAsset(s.frictionless, pmPath);
            }
            s.frictionless.friction = 0f;
            s.frictionless.bounciness = 0f;
            EditorUtility.SetDirty(s.frictionless);
            AssetDatabase.SaveAssets();
        }

        static Material LoadOrCreateMaterial(string name, Shader shader, Texture texture)
        {
            string path = GeneratedDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null)
            {
                mat.shader = shader;
            }
            if (texture != null) mat.mainTexture = texture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Sprite CreateSpriteAsset(string fileName, Texture2D tex, float ppu, Vector4 border)
        {
            string path = GeneratedDir + "/" + fileName + ".png";
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.spriteBorder = border;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // necesario para Sliced / Tiled
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D NewTexture(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            return t;
        }

        static Texture2D MakeBlockTexture()
        {
            Texture2D t = NewTexture(8, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    bool edge = x == 0 || y == 0 || x == 7 || y == 7;
                    bool highlight = !edge && y == 6;
                    float v = edge ? 0.55f : (highlight ? 1f : 0.86f);
                    t.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            t.Apply();
            return t;
        }

        static Texture2D MakePortalTileTexture()
        {
            // Baldosa clara con runa central: "superficie de color válido" para portales.
            Texture2D t = NewTexture(8, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    bool edge = x == 0 || y == 0;
                    bool rune = (x == 3 || x == 4) && (y == 3 || y == 4);
                    float v = edge ? 0.72f : (rune ? 0.8f : 1f);
                    t.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            t.Apply();
            return t;
        }

        static Texture2D MakeSpikeTexture()
        {
            Texture2D t = NewTexture(8, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    float halfWidth = (7.5f - y) * 0.5f;
                    bool inside = Mathf.Abs(x + 0.5f - 4f) <= halfWidth;
                    float v = 0.7f + 0.3f * (y / 7f);
                    t.SetPixel(x, y, inside ? new Color(v, v, v, 1f) : Color.clear);
                }
            t.Apply();
            return t;
        }

        static Texture2D MakeCircleTexture(int size)
        {
            Texture2D t = NewTexture(size, size);
            t.filterMode = FilterMode.Bilinear;
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01((1f - d) * 4f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        static Texture2D MakeCatTexture()
        {
            // Gato pixel-art 16x16 mirando a la derecha (placeholder del protagonista).
            string[] rows =
            {
                "................",
                "................",
                "..........K...K.",
                ".........KOK.KOK",
                ".........KOOKOOK",
                ".........KOOOOOK",
                ".........KOEOOEK",
                ".K.......KOOOPOK",
                "KOK.....KKOWWWK.",
                "KOK..KKKOOOWWK..",
                ".KOKKOOOOOOOOK..",
                "..KOOOOOOOOOOK..",
                "..KOOOOOOOOOOK..",
                "..KOWOOOOOOWOK..",
                "..KOOKKOOKKOOK..",
                "..KKK..KK..KKK..",
            };
            var palette = new Dictionary<char, Color32>
            {
                { '.', new Color32(0, 0, 0, 0) },
                { 'K', new Color32(40, 24, 30, 255) },
                { 'O', new Color32(240, 150, 60, 255) },
                { 'W', new Color32(255, 240, 225, 255) },
                { 'E', new Color32(60, 200, 120, 255) },
                { 'P', new Color32(255, 120, 150, 255) },
            };
            Texture2D t = NewTexture(16, 16);
            for (int row = 0; row < 16; row++)
                for (int x = 0; x < 16; x++)
                    t.SetPixel(x, 15 - row, palette[rows[row][x]]);
            t.Apply();
            return t;
        }

        // =====================================================================
        // Prefabs de portales
        // =====================================================================

        static Portal CreatePortalPrefab(string name, PortalColor color, Color tint, Shared s)
        {
            var go = new GameObject(name);
            go.layer = 2; // Ignore Raycast: no bloquea el disparo ni la detección de suelo.

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(PortalWidth * 0.9f, PortalTriggerDepth);
            box.offset = new Vector2(0f, PortalTriggerDepth * 0.5f);

            var portal = go.AddComponent<Portal>();
            portal.portalColor = color;
            portal.width = PortalWidth;

            // Óvalo exterior
            var outer = new GameObject("Visual");
            outer.transform.SetParent(go.transform, false);
            outer.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            outer.transform.localScale = new Vector3(PortalWidth, 0.42f, 1f);
            var sr = outer.AddComponent<SpriteRenderer>();
            sr.sprite = s.circle;
            sr.sharedMaterial = s.spriteMat;
            sr.color = tint;
            sr.sortingOrder = 5;

            // Núcleo brillante
            var inner = new GameObject("Nucleo");
            inner.transform.SetParent(go.transform, false);
            inner.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            inner.transform.localScale = new Vector3(PortalWidth * 0.75f, 0.2f, 1f);
            var srInner = inner.AddComponent<SpriteRenderer>();
            srInner.sprite = s.circle;
            srInner.sharedMaterial = s.spriteMat;
            srInner.color = Color.Lerp(tint, Color.white, 0.6f);
            srInner.sortingOrder = 6;

            // Partículas arcanas
            var fx = new GameObject("Particulas");
            fx.transform.SetParent(go.transform, false);
            var ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = Color.Lerp(tint, Color.white, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 60;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(PortalWidth * 0.9f, 0.05f, 0.05f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var psr = fx.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = s.particleMat;
            psr.sortingOrder = 7;

            portal.visual = sr;
            portal.particles = ps;

            string path = PrefabDir + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Portal>();
        }

        // =====================================================================
        // Escena
        // =====================================================================

        static GameObject BuildScene(Shared s, Portal bluePrefab, Portal orangePrefab)
        {
            // ---------- Sistemas ----------
            var systems = new GameObject("GameSystems");
            var portalManager = systems.AddComponent<PortalManager>();
            portalManager.bluePortalPrefab = bluePrefab;
            portalManager.orangePortalPrefab = orangePrefab;
            portalManager.portalSurfaceMask = s.PortalMask;
            portalManager.surfaceProbeMask = s.SolidMask;
            portalManager.portalWidth = PortalWidth;
            portalManager.minExitSpeed = 12f;
            portalManager.teleportCooldown = 0.15f;
            portalManager.exitBrakeLockTime = 0.15f;

            var checkpointManager = systems.AddComponent<CheckpointManager>();
            var hud = systems.AddComponent<GameHUD>();

            // ---------- Nivel ----------
            var level = new GameObject("Nivel").transform;
            var checkpointsRoot = new GameObject("Checkpoints").transform;
            checkpointsRoot.SetParent(level);

            Checkpoint firstCheckpoint = BuildLevel(s, level, checkpointsRoot);
            LevelGoal goal = CreateGoal(s, level, new Vector2(89.5f, 6.5f));
            checkpointManager.initialCheckpoint = firstCheckpoint;
            checkpointManager.defaultSpawnPoint = firstCheckpoint.transform;

            // ---------- Player ----------
            GameObject player = CreatePlayer(s, firstCheckpoint.RespawnPosition, portalManager);

            // ---------- Cámara ----------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(player.transform.position.x, player.transform.position.y + 2.5f, -10f);
            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = player.transform;
            follow.targetBody = player.GetComponent<Rigidbody2D>();
            follow.offset = new Vector2(0f, 2.5f);

            player.GetComponent<PortalGun>().aimCamera = cam;

            // ---------- HUD ----------
            hud.player = player.GetComponent<PlayerDeathHandler>();
            hud.dash = player.GetComponent<PlayerDash>();
            hud.portalManager = portalManager;
            hud.goal = goal;

            return player;
        }

        static Checkpoint BuildLevel(Shared s, Transform level, Transform checkpoints)
        {
            Transform z1 = Group(level, "Zona1_Grutas");
            Transform z2 = Group(level, "Zona2_Vacio");
            Transform z3 = Group(level, "Zona3_Chimenea_WallJump");
            Transform z4 = Group(level, "Zona4_Dash");
            Transform z5 = Group(level, "Zona5_Sala_Portales");

            // ---------- Zona 1: introducción (correr, saltar pinchos) ----------
            Block(s, z1, "Suelo_Inicio", -8f, -4f, 14f, 0f, s.ground);
            Block(s, z1, "Pared_Limite", -9f, -4f, -8f, 12f, s.wall);
            Spikes(s, z1, "Pinchos_1", 6f, 8.5f, 0f);
            Block(s, z1, "Plataforma_1", 9.5f, 1.8f, 12f, 2.3f, s.ground);
            Checkpoint cp0 = CreateCheckpoint(s, checkpoints, "Checkpoint_0_Inicio", new Vector2(-4f, 0f));

            // ---------- Zona 2: foso al vacío + pinchos ----------
            Block(s, z2, "Suelo_2", 18f, -4f, 34.5f, 0f, s.ground);
            Block(s, z2, "Plataforma_2", 22f, 2.4f, 25f, 2.9f, s.ground);
            Spikes(s, z2, "Pinchos_2", 26f, 28f, 0f);
            CreateCheckpoint(s, checkpoints, "Checkpoint_1", new Vector2(19.5f, 0f));

            // ---------- Zona 3: chimenea de wall jump (capa Wall) ----------
            Block(s, z3, "Pared_Chimenea_Izq", 30f, 2.6f, 31f, 15f, s.wall);
            Block(s, z3, "Pared_Chimenea_Der", 34.5f, -4f, 35.5f, 12f, s.wall);
            Block(s, z3, "Plataforma_Alta", 35.5f, 11.5f, 46f, 12f, s.ground);
            CreateCheckpoint(s, checkpoints, "Checkpoint_2_Alto", new Vector2(37.5f, 12f));

            // ---------- Zona 4: hueco que requiere salto + dash ----------
            // Hueco de 9 u: un salto normal alcanza ~7.6 u, así que obliga a usar el dash.
            Block(s, z4, "Plataforma_Dash", 55f, 11.5f, 60f, 12f, s.ground);

            // ---------- Zona 5: sala de portales ----------
            // Muro de lanzamiento (cara derecha = superficie de portal mirando a +X).
            Block(s, z5, "Muro_Lanzamiento", 58f, -4f, 60f, 11.5f, s.portalSurface);
            Block(s, z5, "Suelo_Sala_A", 60f, -4f, 62f, 0f, s.ground);
            Block(s, z5, "Suelo_Portal", 62f, -4f, 66f, 0f, s.portalSurface);
            Block(s, z5, "Suelo_Sala_B", 66f, -4f, 68f, 0f, s.ground);
            // Techo alineado sobre Suelo_Portal: permite el bucle de caída infinita (azul abajo / naranja arriba)
            // para acumular velocidad y luego "reaccionar" moviendo el naranja al muro o al Suelo_Portal_2.
            Block(s, z5, "Techo_Portal", 62.5f, 11f, 66.5f, 12f, s.portalSurface);
            CreateCheckpoint(s, checkpoints, "Checkpoint_3_Sala", new Vector2(61f, 0f));

            // Foso de pinchos (cruzar con lanzamiento horizontal desde el muro)
            Block(s, z5, "Fondo_Foso", 68f, -4f, 74f, -3f, s.ground);
            Spikes(s, z5, "Pinchos_Foso", 68f, 74f, -3f);

            // Zona de llegada con segunda superficie de portal en el piso (lanzamiento vertical a la meta)
            Block(s, z5, "Suelo_Llegada_A", 74f, -4f, 80f, -2f, s.ground);
            Block(s, z5, "Suelo_Portal_2", 80f, -4f, 83f, -2f, s.portalSurface);
            Block(s, z5, "Suelo_Llegada_B", 83f, -4f, 92f, -2f, s.ground);
            Block(s, z5, "Repisa_Meta", 85f, 6f, 92f, 6.5f, s.ground);
            Block(s, z5, "Pared_Final", 92f, -4f, 93f, 16f, s.wall);
            CreateCheckpoint(s, checkpoints, "Checkpoint_4_Llegada", new Vector2(76f, -2f));

            return cp0;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            return t;
        }

        static GameObject Block(Shared s, Transform parent, string name, float xMin, float yMin, float xMax, float yMax, int layer)
        {
            Vector2 size = new Vector2(xMax - xMin, yMax - yMin);
            Vector2 center = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);

            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.layer = layer;
            go.isStatic = true;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = s.spriteMat;
            sr.sortingOrder = 0;
            if (layer == s.portalSurface)
            {
                sr.sprite = s.tile;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.color = PortalSurfaceColor;
            }
            else
            {
                sr.sprite = s.block;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.color = layer == s.wall ? WallColor : GroundColor;
            }
            sr.size = size;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            col.offset = Vector2.zero;
            return go;
        }

        static GameObject Spikes(Shared s, Transform parent, string name, float xMin, float xMax, float floorY)
        {
            const float height = 0.5f;
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = new Vector2((xMin + xMax) * 0.5f, floorY + height * 0.5f);
            go.layer = s.hazard;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s.spike;
            sr.sharedMaterial = s.spriteMat;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(xMax - xMin, height);
            sr.color = HazardColor;
            sr.sortingOrder = 1;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(xMax - xMin - 0.1f, height * 0.7f);
            col.offset = new Vector2(0f, -height * 0.15f);
            return go;
        }

        static Checkpoint CreateCheckpoint(Shared s, Transform parent, string name, Vector2 floorPoint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = floorPoint;
            go.layer = 2; // Ignore Raycast

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.2f, 2.5f);
            col.offset = new Vector2(0f, 1.25f);

            var pole = new GameObject("Asta");
            pole.transform.SetParent(go.transform, false);
            pole.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            var poleSr = pole.AddComponent<SpriteRenderer>();
            poleSr.sprite = s.block;
            poleSr.sharedMaterial = s.spriteMat;
            poleSr.drawMode = SpriteDrawMode.Sliced;
            poleSr.size = new Vector2(0.12f, 1.5f);
            poleSr.color = new Color(0.8f, 0.75f, 0.65f);
            poleSr.sortingOrder = 2;

            var flag = new GameObject("Bandera");
            flag.transform.SetParent(go.transform, false);
            flag.transform.localPosition = new Vector3(0.3f, 1.3f, 0f);
            var flagSr = flag.AddComponent<SpriteRenderer>();
            flagSr.sprite = s.block;
            flagSr.sharedMaterial = s.spriteMat;
            flagSr.drawMode = SpriteDrawMode.Sliced;
            flagSr.size = new Vector2(0.5f, 0.35f);
            flagSr.sortingOrder = 3;

            var cp = go.AddComponent<Checkpoint>();
            cp.flagRenderer = flagSr;
            cp.respawnOffset = new Vector2(0f, 0.5f);
            flagSr.color = cp.inactiveColor;
            return cp;
        }

        static LevelGoal CreateGoal(Shared s, Transform parent, Vector2 floorPoint)
        {
            var go = new GameObject("Meta");
            go.transform.SetParent(parent);
            go.transform.position = floorPoint + new Vector2(0f, 0.7f);
            go.layer = 2;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s.circle;
            sr.sharedMaterial = s.spriteMat;
            sr.color = new Color(1f, 0.85f, 0.35f);
            sr.sortingOrder = 4;
            go.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.6f;

            return go.AddComponent<LevelGoal>();
        }

        static GameObject CreatePlayer(Shared s, Vector3 spawn, PortalManager portalManager)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.layer = 0;
            player.transform.position = spawn;

            var rb = player.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 3f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var box = player.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.62f, 0.77f);
            box.edgeRadius = 0.04f; // evita engancharse en uniones de bloques
            box.sharedMaterial = s.frictionless;

            // Visual (hijo) para poder escalar/animar sin afectar la física.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.075f, 0f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = s.cat;
            sr.sharedMaterial = s.spriteMat;
            sr.sortingOrder = 10;

            // Trail del dash
            var trail = player.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = 1f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.6f, 1f, 0f);
            trail.sharedMaterial = s.spriteMat;
            var trailGrad = new Gradient();
            trailGrad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.75f, 0.4f), 0f), new GradientColorKey(new Color(0.6f, 0.8f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailGrad;
            trail.sortingOrder = 9;
            trail.emitting = false;

            // Línea de mira
            var aim = new GameObject("LineaMira");
            aim.transform.SetParent(player.transform, false);
            var line = aim.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.05f;
            line.sharedMaterial = s.spriteMat;
            line.sortingOrder = 8;
            line.numCapVertices = 2;

            // Componentes de juego
            var input = player.AddComponent<PlayerInputReader>();
            input.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (input.actionsAsset == null)
                Debug.LogWarning("[PurrTales] No se encontró " + InputAssetPath + "; se usarán acciones creadas por código.");

            var movement = player.AddComponent<PlayerMovement>();
            var dash = player.AddComponent<PlayerDash>();
            var death = player.AddComponent<PlayerDeathHandler>();
            var traveler = player.AddComponent<PortalTraveler>();
            var gun = player.AddComponent<PortalGun>();

            movement.rb = rb;
            movement.bodyCollider = box;
            movement.spriteRenderer = sr;
            movement.input = input;
            movement.dash = dash;
            movement.groundMask = s.StandableMask;
            movement.wallMask = s.WallMask;

            dash.rb = rb;
            dash.movement = movement;
            dash.input = input;
            dash.trail = trail;
            dash.spriteRenderer = sr;

            death.rb = rb;
            death.movement = movement;
            death.dash = dash;
            death.input = input;
            death.spriteRenderer = sr;
            death.trail = trail;
            death.hazardMask = s.HazardMask;
            death.killY = -12f;
            death.respawnDelay = 0.12f;

            traveler.rb = rb;

            gun.input = input;
            gun.portalManager = portalManager;
            gun.ownerCollider = box;
            gun.deathHandler = death;
            gun.raycastMask = s.SolidMask;
            gun.aimLine = line;

            return player;
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var sc in scenes)
            {
                if (sc.path == path) return;
            }
            scenes.Insert(0, new EditorBuildSettingsScene(path, true)); // escena de arranque
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
