#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace PurrTales.EditorTools
{
    /// <summary>
    /// Configura e integra los recursos artísticos de Purr-tales ubicados en Assets/_Juego/Art/:
    ///  1. Configuración global Pixel Art (Sprite 2D/UI, Point, Sin compresión, PPU por categoría).
    ///  2. Corte (Sprite Slicing) en modo Multiple para tilesets, enemigos, trampas, personajes, portales y gato.
    ///  3. Generación de AnimationClips y AnimatorController del jugador (Idle, Walk, Run) y portales.
    ///  4. Actualización de PortalAzul.prefab, PortalNaranja.prefab y Player en PurrTales_Prueba.unity.
    ///  5. Creación de las Tile Palettes iniciales para Nivel 1 (Grutas).
    /// </summary>
    public static class PurrTalesArtSetup
    {
        const string ArtRoot = "Assets/_Juego/Art";
        const string PlayerAnimDir = ArtRoot + "/Animations/Player";
        const string PortalAnimDir = ArtRoot + "/Animations/Portals";
        const string PaletteN1Dir = ArtRoot + "/TilePalettes/Nivel1_Grutas";
        const string PaletteN1TilesDir = PaletteN1Dir + "/Tiles";

        const string PlayerControllerPath = PlayerAnimDir + "/PlayerAnimator.controller";
        const string BluePortalControllerPath = PortalAnimDir + "/PortalAzul.controller";
        const string OrangePortalControllerPath = PortalAnimDir + "/PortalNaranja.controller";

        const string BluePortalPrefabPath = "Assets/_Juego/Prefabs/PortalAzul.prefab";
        const string OrangePortalPrefabPath = "Assets/_Juego/Prefabs/PortalNaranja.prefab";

        const string CatIdleTexPath = ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Reposo_Idle_Redesign.png";
        const string CatRunTexPath = ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Corriendo_Run_Redesign.png";
        const string CatWalkTexPath = ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Caminando_Walk_Redesign.png";

        const string PortalBlueFloorTexPath = ArtRoot + "/Purr-tales/Compartido/02_Portal_Salida_Suelo.png";
        const string PortalBlueWallTexPath = ArtRoot + "/Purr-tales/Compartido/02_Portal_Salida_Pared.png";
        const string PortalOrangeFloorTexPath = ArtRoot + "/Purr-tales/Compartido/02_Portal_Entrada_Suelo.png";
        const string PortalOrangeWallTexPath = ArtRoot + "/Purr-tales/Compartido/02_Portal_Entrada_Pared.png";

        const string N1TilesetTexPath = ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Tileset_Grutas.png";
        const string N1DecorTexPath = ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Plataformas_Decoracion.png";
        const string N1SpikesTexPath = ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Pinchos_Cristal.png";
        const string N1AcidTexPath = ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Acido.png";

        const float WorldPPU = 16f;
        const float CatPPU = 32f;

        struct SliceSpec
        {
            public int cellWidth;
            public int cellHeight;
            public float ppu;
            public SpriteAlignment alignment;
            public Vector2 pivot;
            public bool forceMultiple;

            public SliceSpec(int w, int h, float ppu, SpriteAlignment alignment, Vector2 pivot, bool forceMultiple = true)
            {
                cellWidth = w;
                cellHeight = h;
                this.ppu = ppu;
                this.alignment = alignment;
                this.pivot = pivot;
                this.forceMultiple = forceMultiple;
            }
        }

        static readonly Dictionary<string, SliceSpec> SliceRules = new Dictionary<string, SliceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            // ---- Gato (Personaje Rediseñado, 100x100 por frame, PPU=32 => ~1 unidad de altura) ----
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Reposo_Idle_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Corriendo_Run_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Caminando_Walk_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Acostandose_Laying_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Estirandose_Stretching_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Lamiendose_1_Licking1_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Lamiendose_2_Licking2_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Maullando_Meow_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Rascandose_Itch_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Durmiendo_1_Sleeping1_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f), false) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Durmiendo_2_Sleeping2_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f), false) },
            { ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Sentado_Sitting_Redesign.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f), false) },
            { ArtRoot + "/Personaje (Rediseñado)/Efecto_Maullido_Meow_VFX_Redesign.png", new SliceSpec(32, 32, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },

            // ---- Portales (PPU=16, pivote pegado a la superficie) ----
            { PortalOrangeFloorTexPath, new SliceSpec(32, 16, WorldPPU, SpriteAlignment.Custom, new Vector2(0.5f, 1f / 16f)) },
            { PortalBlueFloorTexPath, new SliceSpec(32, 16, WorldPPU, SpriteAlignment.Custom, new Vector2(0.5f, 1f / 16f)) },
            { PortalOrangeWallTexPath, new SliceSpec(16, 32, WorldPPU, SpriteAlignment.Custom, new Vector2(1f / 16f, 0.5f)) },
            { PortalBlueWallTexPath, new SliceSpec(16, 32, WorldPPU, SpriteAlignment.Custom, new Vector2(1f / 16f, 0.5f)) },

            // ---- Personajes compartidos (32x32, PPU=16) ----
            { ArtRoot + "/Purr-tales/Compartido/Personajes/Hechicera.png", new SliceSpec(32, 32, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Compartido/Personajes/Villano.png", new SliceSpec(32, 32, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },

            // ---- Nivel 1: Grutas ----
            { N1TilesetTexPath, new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { N1DecorTexPath, new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { N1SpikesTexPath, new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { N1AcidTexPath, new SliceSpec(16, 32, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Slime.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Murcielago.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel1_Grutas/Fondos/N1_Fondo_4_Particulas.png", new SliceSpec(320, 180, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },

            // ---- Nivel 2: Torre ----
            { ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Tileset_Torre.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Plataformas_Decoracion.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Pinchos_Hierro.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Plataforma_Desmorona.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Armadura_Animada.png", new SliceSpec(24, 24, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },

            // ---- Nivel 3: Fortaleza ----
            { ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Tileset_Fortaleza.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Plataformas_Decoracion.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Craneo_Flotante.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Orbe_Oscuro.png", new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Conducto_Energia.png", new SliceSpec(48, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Lava_Arcana.png", new SliceSpec(16, 32, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { ArtRoot + "/Purr-tales/Nivel3_Fortaleza/Fondos/N3_Fondo_4_Particulas.png", new SliceSpec(320, 180, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
        };

        [MenuItem("Purr-tales/Integrar Arte (Sprites, Animaciones y Paletas)", false, 2)]
        public static void RunFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Purr-tales", "Sal del modo Play antes de ejecutar la integración.", "OK");
                return;
            }
            Run();
            EditorUtility.DisplayDialog("Purr-tales", "Integración de sprites, animaciones, portales y Tile Palettes completada.", "OK");
        }

        /// <summary>Punto de entrada para ejecución manual o por línea de comandos (-executeMethod).</summary>
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot))
                throw new DirectoryNotFoundException("No se encontró la carpeta " + ArtRoot);

            EnsureFolderTree();

            var factory = new SpriteDataProviderFactories();
            factory.Init();

            ConfigureAndSliceAllTextures(factory);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AnimatorController playerController = CreatePlayerAnimations();
            AnimatorController bluePortalController = CreatePortalAnimations("PortalAzul", PortalBlueFloorTexPath, PortalBlueWallTexPath, BluePortalControllerPath);
            AnimatorController orangePortalController = CreatePortalAnimations("PortalNaranja", PortalOrangeFloorTexPath, PortalOrangeWallTexPath, OrangePortalControllerPath);

            UpdatePortalPrefab(BluePortalPrefabPath, PortalColor.Blue, PortalBlueFloorTexPath, bluePortalController);
            UpdatePortalPrefab(OrangePortalPrefabPath, PortalColor.Orange, PortalOrangeFloorTexPath, orangePortalController);

            UpdatePlayerInScene(playerController);
            CreateLevel1TilePalettes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            VerifyIntegration();
            Debug.Log("[PurrTalesArtSetup] Integración artística completada correctamente.");
        }

        static void VerifyIntegration()
        {
            AssertSpritesCount(CatIdleTexPath, 10);
            AssertSpritesCount(CatWalkTexPath, 8);
            AssertSpritesCount(CatRunTexPath, 8);
            AssertSpritesCount(N1TilesetTexPath, 48);
            AssertSpritesCount(ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Tileset_Torre.png", 48);
            AssertSpritesCount(ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Tileset_Fortaleza.png", 48);
            AssertSpritesCount(ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Slime.png", 11);
            AssertSpritesCount(ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Murcielago.png", 11);
            AssertSpritesCount(N1SpikesTexPath, 4);
            AssertSpritesCount(ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Pinchos_Hierro.png", 4);
            AssertSpritesCount(ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Armadura_Animada.png", 11);
            AssertSpritesCount(ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Craneo_Flotante.png", 11);
            AssertSpritesCount(ArtRoot + "/Purr-tales/Compartido/Personajes/Hechicera.png", 10);
            AssertSpritesCount(ArtRoot + "/Purr-tales/Compartido/Personajes/Villano.png", 21);
            AssertSpritesCount(PortalBlueFloorTexPath, 12);
            AssertSpritesCount(PortalBlueWallTexPath, 12);
            AssertSpritesCount(PortalOrangeFloorTexPath, 12);
            AssertSpritesCount(PortalOrangeWallTexPath, 12);

            VerifyClip(PlayerAnimDir + "/Idle.anim", 10, 8f);
            VerifyClip(PlayerAnimDir + "/Walk.anim", 8, 10f);
            VerifyClip(PlayerAnimDir + "/Run.anim", 8, 16f);

            var playerCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (playerCtrl == null || playerCtrl.parameters.Length != 3)
                throw new InvalidOperationException("PlayerAnimator.controller inválido.");

            var bluePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BluePortalPrefabPath);
            var orangePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrangePortalPrefabPath);
            if (bluePrefab == null || orangePrefab == null)
                throw new InvalidOperationException("Faltan prefabs de portales.");
            var bluePortal = bluePrefab.GetComponent<Portal>();
            var orangePortal = orangePrefab.GetComponent<Portal>();
            if (bluePortal.visual == null || bluePortal.visualAnimator == null || bluePortal.visual.sprite == null)
                throw new InvalidOperationException("PortalAzul.prefab no tiene visual/visualAnimator/sprite asignados.");
            if (orangePortal.visual == null || orangePortal.visualAnimator == null || orangePortal.visual.sprite == null)
                throw new InvalidOperationException("PortalNaranja.prefab no tiene visual/visualAnimator/sprite asignados.");

            var p1 = AssetDatabase.LoadAssetAtPath<GameObject>(PaletteN1Dir + "/Paleta_N1_Grutas_Terreno.prefab");
            var p2 = AssetDatabase.LoadAssetAtPath<GameObject>(PaletteN1Dir + "/Paleta_N1_Grutas_Decoracion_Peligros.prefab");
            if (p1 == null || p2 == null)
                throw new InvalidOperationException("Faltan Tile Palettes de Nivel 1.");

            Debug.Log("[PurrTalesArtSetup] Verificación OK: slices, clips, PlayerAnimator, prefabs de portales y Tile Palettes de Nivel 1 validados.");
        }

        static void AssertSpritesCount(string texPath, int expected)
        {
            Sprite[] sprites = LoadSlicedSprites(texPath);
            if (sprites.Length != expected)
                throw new InvalidOperationException($"Sprite count mismatch en {texPath}: esperado {expected}, encontrado {sprites.Length}");
        }

        static void VerifyClip(string clipPath, int expectedFrames, float expectedFps)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) throw new InvalidOperationException("Falta clip " + clipPath);
            if (!clip.isLooping) throw new InvalidOperationException("Clip sin loopTime: " + clipPath);
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length != 1) throw new InvalidOperationException("Curva inválida en " + clipPath);
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            if (keys.Length != expectedFrames)
                throw new InvalidOperationException($"Frames mismatch en {clipPath}: esperado {expectedFrames}, encontrado {keys.Length}");
            float expectedDuration = expectedFrames / expectedFps;
            if (Mathf.Abs(clip.length - expectedDuration) > 0.001f)
                throw new InvalidOperationException($"Duración mismatch en {clipPath}: esperado {expectedDuration}, encontrado {clip.length}");
        }

        static void EnsureFolderTree()
        {
            EnsureFolder(ArtRoot, "Animations");
            EnsureFolder(ArtRoot + "/Animations", "Player");
            EnsureFolder(ArtRoot + "/Animations", "Portals");
            EnsureFolder(ArtRoot, "TilePalettes");
            EnsureFolder(ArtRoot + "/TilePalettes", "Nivel1_Grutas");
            EnsureFolder(PaletteN1Dir, "Tiles");
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        // =====================================================================
        // 1 y 2. Importación Pixel Art y Sprite Slicing
        // =====================================================================

        static void ConfigureAndSliceAllTextures(SpriteDataProviderFactories factory)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                ti.GetSourceTextureWidthAndHeight(out int texW, out int texH);

                bool hasRule = SliceRules.TryGetValue(path, out SliceSpec spec);
                float ppu = hasRule ? spec.ppu : WorldPPU;
                bool isMultiple = hasRule && spec.forceMultiple;

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = isMultiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                ti.spritePixelsPerUnit = ppu;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(texW, texH)));

                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                if (!isMultiple)
                {
                    settings.spriteAlignment = hasRule ? (int)spec.alignment : (int)SpriteAlignment.Center;
                    settings.spritePivot = hasRule ? spec.pivot : new Vector2(0.5f, 0.5f);
                }
                ti.SetTextureSettings(settings);

                if (isMultiple)
                {
                    ApplyGridSlice(ti, factory, path, texW, texH, spec);
                }

                ti.SaveAndReimport();
                count++;
            }

            Debug.Log("[PurrTalesArtSetup] Texturas configuradas: " + count);
        }

        static void ApplyGridSlice(TextureImporter ti, SpriteDataProviderFactories factory, string assetPath, int texW, int texH, SliceSpec spec)
        {
            int cols = texW / spec.cellWidth;
            int rows = texH / spec.cellHeight;
            if (cols <= 0 || rows <= 0)
            {
                Debug.LogWarning($"[PurrTalesArtSetup] Tamaño incompatible en {assetPath}: {texW}x{texH} vs celda {spec.cellWidth}x{spec.cellHeight}");
                return;
            }

            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(ti);
            dataProvider.InitSpriteEditorDataProvider();

            var existingByName = new Dictionary<string, GUID>();
            SpriteRect[] existing = dataProvider.GetSpriteRects();
            if (existing != null)
            {
                foreach (SpriteRect r in existing)
                {
                    if (!string.IsNullOrEmpty(r.name) && !existingByName.ContainsKey(r.name))
                        existingByName[r.name] = r.spriteID;
                }
            }

            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            var rects = new List<SpriteRect>(cols * rows);
            int index = 0;

            // Orden de arriba a abajo, izquierda a derecha (coincide con *_reglas.json y Unity Grid Slicer).
            for (int row = 0; row < rows; row++)
            {
                int y = texH - (row + 1) * spec.cellHeight;
                for (int col = 0; col < cols; col++)
                {
                    int x = col * spec.cellWidth;
                    string sliceName = baseName + "_" + index;
                    GUID spriteId = existingByName.TryGetValue(sliceName, out GUID prev) ? prev : GUID.Generate();

                    rects.Add(new SpriteRect
                    {
                        name = sliceName,
                        rect = new Rect(x, y, spec.cellWidth, spec.cellHeight),
                        alignment = spec.alignment,
                        pivot = spec.pivot,
                        border = Vector4.zero,
                        spriteID = spriteId
                    });
                    index++;
                }
            }

            dataProvider.SetSpriteRects(rects.ToArray());
            dataProvider.Apply();
        }

        // =====================================================================
        // 3. Animaciones del Jugador
        // =====================================================================

        static AnimatorController CreatePlayerAnimations()
        {
            Sprite[] idleSprites = LoadSlicedSprites(CatIdleTexPath);
            Sprite[] walkSprites = LoadSlicedSprites(CatWalkTexPath);
            Sprite[] runSprites = LoadSlicedSprites(CatRunTexPath);

            AnimationClip idleClip = CreateSpriteAnimationClip(PlayerAnimDir + "/Idle.anim", idleSprites, 8f, true);
            AnimationClip walkClip = CreateSpriteAnimationClip(PlayerAnimDir + "/Walk.anim", walkSprites, 10f, true);
            AnimationClip runClip = CreateSpriteAnimationClip(PlayerAnimDir + "/Run.anim", runSprites, 16f, true);

            if (File.Exists(PlayerControllerPath)) AssetDatabase.DeleteAsset(PlayerControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idleState = sm.AddState("Idle", new Vector3(260, 120, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState walkState = sm.AddState("Walk", new Vector3(520, 40, 0));
            walkState.motion = walkClip;

            AnimatorState runState = sm.AddState("Run", new Vector3(520, 200, 0));
            runState.motion = runClip;

            // Idle -> Walk
            AnimatorStateTransition tIdleToWalk = AddInstantTransition(idleState, walkState);
            tIdleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tIdleToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");

            // Idle -> Run (velocidad alta o dash)
            AnimatorStateTransition tIdleToRunSpeed = AddInstantTransition(idleState, runState);
            tIdleToRunSpeed.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Speed");

            AnimatorStateTransition tIdleToRunDash = AddInstantTransition(idleState, runState);
            tIdleToRunDash.AddCondition(AnimatorConditionMode.If, 0f, "IsDashing");

            // Walk -> Run
            AnimatorStateTransition tWalkToRunSpeed = AddInstantTransition(walkState, runState);
            tWalkToRunSpeed.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Speed");

            AnimatorStateTransition tWalkToRunDash = AddInstantTransition(walkState, runState);
            tWalkToRunDash.AddCondition(AnimatorConditionMode.If, 0f, "IsDashing");

            // Walk -> Idle
            AnimatorStateTransition tWalkToIdle = AddInstantTransition(walkState, idleState);
            tWalkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tWalkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");

            // Run -> Walk (solo en suelo cuando desacelera)
            AnimatorStateTransition tRunToWalk = AddInstantTransition(runState, walkState);
            tRunToWalk.AddCondition(AnimatorConditionMode.Less, 3.0f, "Speed");
            tRunToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tRunToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");

            // Run -> Idle
            AnimatorStateTransition tRunToIdle = AddInstantTransition(runState, idleState);
            tRunToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tRunToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorController CreatePortalAnimations(string prefix, string floorTexPath, string wallTexPath, string controllerPath)
        {
            Sprite[] floorSprites = LoadSlicedSprites(floorTexPath);
            Sprite[] wallSprites = LoadSlicedSprites(wallTexPath);

            AnimationClip floorClip = CreateSpriteAnimationClip(PortalAnimDir + "/" + prefix + "_Suelo.anim", floorSprites, 14f, true);
            AnimationClip wallClip = CreateSpriteAnimationClip(PortalAnimDir + "/" + prefix + "_Pared.anim", wallSprites, 14f, true);

            if (File.Exists(controllerPath)) AssetDatabase.DeleteAsset(controllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("IsWall", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState floorState = sm.AddState("Suelo", new Vector3(260, 100, 0));
            floorState.motion = floorClip;
            sm.defaultState = floorState;

            AnimatorState wallState = sm.AddState("Pared", new Vector3(520, 100, 0));
            wallState.motion = wallClip;

            AnimatorStateTransition toWall = AddInstantTransition(floorState, wallState);
            toWall.AddCondition(AnimatorConditionMode.If, 0f, "IsWall");

            AnimatorStateTransition toFloor = AddInstantTransition(wallState, floorState);
            toFloor.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWall");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorStateTransition AddInstantTransition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.exitTime = 0f;
            t.duration = 0f;
            t.hasFixedDuration = true;
            t.offset = 0f;
            return t;
        }

        static AnimationClip CreateSpriteAnimationClip(string clipPath, Sprite[] sprites, float fps, bool loop)
        {
            if (sprites == null || sprites.Length == 0)
                throw new InvalidOperationException("No se encontraron sprites para generar " + clipPath);

            if (File.Exists(clipPath)) AssetDatabase.DeleteAsset(clipPath);

            var clip = new AnimationClip { frameRate = fps };
            var binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };

            // Para PPtrCurve (Sprite), Unity extiende StopTime un frame (1/fps) tras el último keyframe automáticamente.
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / fps,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, clipPath);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static Sprite[] LoadSlicedSprites(string texPath)
        {
            UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(texPath);
            var list = new List<Sprite>();
            foreach (UnityEngine.Object o in all)
            {
                if (o is Sprite sp) list.Add(sp);
            }
            list.Sort((a, b) => ExtractTrailingIndex(a.name).CompareTo(ExtractTrailingIndex(b.name)));
            return list.ToArray();
        }

        static int ExtractTrailingIndex(string name)
        {
            int underscore = name.LastIndexOf('_');
            if (underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int idx))
                return idx;
            return 0;
        }

        // =====================================================================
        // 4. Prefabs de Portales y Jugador en Escena
        // =====================================================================

        static void UpdatePortalPrefab(string prefabPath, PortalColor color, string floorTexPath, AnimatorController controller)
        {
            Sprite[] floorSprites = LoadSlicedSprites(floorTexPath);
            if (floorSprites.Length == 0)
                throw new InvalidOperationException("No se encontraron sprites de portal en " + floorTexPath);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var portal = root.GetComponent<Portal>();
                portal.portalColor = color;

                Transform nucleo = root.transform.Find("Nucleo");
                if (nucleo != null) UnityEngine.Object.DestroyImmediate(nucleo.gameObject);

                Transform visualTf = root.transform.Find("Visual");
                if (visualTf == null)
                {
                    var vgo = new GameObject("Visual");
                    vgo.transform.SetParent(root.transform, false);
                    visualTf = vgo.transform;
                }

                visualTf.localPosition = Vector3.zero;
                visualTf.localRotation = Quaternion.identity;
                // 30 px útiles / 16 PPU = 1.875 u; 0.85x ajusta el ancho visual al width=1.6 u del Portal.
                visualTf.localScale = new Vector3(0.85f, 0.85f, 1f);

                var sr = visualTf.GetComponent<SpriteRenderer>();
                if (sr == null) sr = visualTf.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = floorSprites[0];
                sr.color = Color.white;
                sr.sortingOrder = 5;

                var anim = visualTf.GetComponent<Animator>();
                if (anim == null) anim = visualTf.gameObject.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;

                portal.visual = sr;
                portal.visualAnimator = anim;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void UpdatePlayerInScene(AnimatorController playerController)
        {
            string scenePath = PurrTalesSceneBuilder.GameScenePath;
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning("[PurrTalesArtSetup] No existe la escena " + scenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogWarning("[PurrTalesArtSetup] No se encontró el GameObject Player en " + scenePath);
                return;
            }

            Sprite[] idleSprites = LoadSlicedSprites(CatIdleTexPath);
            Transform visualTf = player.transform.Find("Visual");
            if (visualTf == null)
            {
                var vgo = new GameObject("Visual");
                vgo.transform.SetParent(player.transform, false);
                visualTf = vgo.transform;
            }

            // Con PPU=32 y pivote Center (y=50 en celda 100x100), las patas están en y=36 (-0.4375 u desde el centro).
            // El fondo del BoxCollider2D está en -0.425 u => offset Y = +0.0125 u para apoyar las patas exacto en el suelo.
            visualTf.localPosition = new Vector3(0f, 0.0125f, 0f);
            visualTf.localScale = Vector3.one;

            var sr = visualTf.GetComponent<SpriteRenderer>();
            if (sr == null) sr = visualTf.gameObject.AddComponent<SpriteRenderer>();
            if (idleSprites.Length > 0) sr.sprite = idleSprites[0];
            sr.color = Color.white;
            sr.sortingOrder = 10;

            var anim = visualTf.GetComponent<Animator>();
            if (anim == null) anim = visualTf.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = playerController;

            var movement = player.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.spriteRenderer = sr;
                movement.animator = anim;
                EditorUtility.SetDirty(movement);
            }

            var dash = player.GetComponent<PlayerDash>();
            if (dash != null)
            {
                dash.spriteRenderer = sr;
                EditorUtility.SetDirty(dash);
            }

            var death = player.GetComponent<PlayerDeathHandler>();
            if (death != null)
            {
                death.spriteRenderer = sr;
                EditorUtility.SetDirty(death);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        // =====================================================================
        // 5. Tile Palettes para Nivel 1 (Grutas)
        // =====================================================================

        static void CreateLevel1TilePalettes()
        {
            // Paleta 1: Terreno (8x6 = 48 tiles de N1_Tileset_Grutas)
            Sprite[] terrainSprites = LoadSlicedSprites(N1TilesetTexPath);
            BuildGridPalette(
                PaletteN1Dir + "/Paleta_N1_Grutas_Terreno.prefab",
                "Paleta_N1_Grutas_Terreno",
                (tilemap) =>
                {
                    const int cols = 8;
                    const int rows = 6;
                    for (int i = 0; i < terrainSprites.Length; i++)
                    {
                        int col = i % cols;
                        int row = i / cols;
                        Tile tile = CreateOrUpdateTileAsset(
                            PaletteN1TilesDir + "/N1_Terreno_" + i + ".asset",
                            terrainSprites[i],
                            Tile.ColliderType.Grid);
                        tilemap.SetTile(new Vector3Int(col, (rows - 1) - row, 0), tile);
                    }
                });

            // Paleta 2: Plataformas, Decoración, Pinchos y Ácido
            Sprite[] decorSprites = LoadSlicedSprites(N1DecorTexPath);
            Sprite[] spikeSprites = LoadSlicedSprites(N1SpikesTexPath);
            Sprite[] acidSprites = LoadSlicedSprites(N1AcidTexPath);

            BuildGridPalette(
                PaletteN1Dir + "/Paleta_N1_Grutas_Decoracion_Peligros.prefab",
                "Paleta_N1_Grutas_Decoracion_Peligros",
                (tilemap) =>
                {
                    // Fila 2: Plataformas (0..3 con collider) y Decoración (4..7 sin collider)
                    for (int i = 0; i < decorSprites.Length; i++)
                    {
                        Tile.ColliderType colType = i < 4 ? Tile.ColliderType.Grid : Tile.ColliderType.None;
                        Tile tile = CreateOrUpdateTileAsset(
                            PaletteN1TilesDir + "/N1_Decor_" + i + ".asset",
                            decorSprites[i],
                            colType);
                        tilemap.SetTile(new Vector3Int(i, 2, 0), tile);
                    }

                    // Fila 1: Pinchos de Cristal (Suelo, Techo, Pared_Izq, Pared_Der)
                    for (int i = 0; i < spikeSprites.Length; i++)
                    {
                        Tile tile = CreateOrUpdateTileAsset(
                            PaletteN1TilesDir + "/N1_Pinchos_" + i + ".asset",
                            spikeSprites[i],
                            Tile.ColliderType.Sprite);
                        tilemap.SetTile(new Vector3Int(i, 1, 0), tile);
                    }

                    // Fila 0: Ácido (frames de animación 16x32)
                    for (int i = 0; i < acidSprites.Length; i++)
                    {
                        Tile tile = CreateOrUpdateTileAsset(
                            PaletteN1TilesDir + "/N1_Acido_" + i + ".asset",
                            acidSprites[i],
                            Tile.ColliderType.Grid);
                        tilemap.SetTile(new Vector3Int(i, 0, 0), tile);
                    }
                });
        }

        static Tile CreateOrUpdateTileAsset(string assetPath, Sprite sprite, Tile.ColliderType colliderType)
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, assetPath);
            }
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = colliderType;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        static void BuildGridPalette(string prefabPath, string paletteName, Action<Tilemap> populate)
        {
            if (File.Exists(prefabPath)) AssetDatabase.DeleteAsset(prefabPath);

            var root = new GameObject(paletteName);
            var grid = root.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellGap = Vector3.zero;

            var layer = new GameObject("Layer1");
            layer.transform.SetParent(root.transform, false);
            var tilemap = layer.AddComponent<Tilemap>();
            var renderer = layer.AddComponent<TilemapRenderer>();
            renderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;

            populate(tilemap);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            var gridPalette = ScriptableObject.CreateInstance<GridPalette>();
            gridPalette.name = "Palette Settings";
            gridPalette.cellSizing = GridPalette.CellSizing.Automatic;
            gridPalette.transparencySortMode = TransparencySortMode.Default;
            AssetDatabase.AddObjectToAsset(gridPalette, prefab);
            EditorUtility.SetDirty(prefab);
        }
    }
}
#endif
