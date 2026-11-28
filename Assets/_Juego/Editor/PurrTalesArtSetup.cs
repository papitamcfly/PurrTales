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
    ///  2. Corte (Sprite Slicing) en modo Multiple para tilesets, enemigos, trampas, personajes, portales, gato y VFX.
    ///  3. Generación de AnimationClips y AnimatorController del jugador (Idle, Walk, Run, Jump, Fall, WallSlide, Dash),
    ///     VFX (Death_Poof, Dash_Recharge) y portales.
    ///  4. Material URP 2D de feedback de dash (Gato_DashColor.mat) y prefabs de VFX.
    ///  5. Actualización de PortalAzul.prefab, PortalNaranja.prefab y Player en PurrTales_Prueba.unity.
    ///  6. Creación de Tile Palettes para Nivel 1 (Grutas) y Superficie Universal de Portales (layer PortalSurface).
    /// </summary>
    public static class PurrTalesArtSetup
    {
        const string ArtRoot = "Assets/_Juego/Art";
        const string NewGameplayDir = ArtRoot + "/Nuevos_Gameplay";
        const string DashSpentDir = NewGameplayDir + "/Dash_Gastado";

        const string PlayerAnimDir = ArtRoot + "/Animations/Player";
        const string PortalAnimDir = ArtRoot + "/Animations/Portals";
        const string VfxAnimDir = ArtRoot + "/Animations/VFX";
        const string MaterialsDir = ArtRoot + "/Materials";
        const string VfxPrefabDir = "Assets/_Juego/Prefabs/VFX";

        const string PaletteN1Dir = ArtRoot + "/TilePalettes/Nivel1_Grutas";
        const string PaletteN1TilesDir = PaletteN1Dir + "/Tiles";
        const string PaletteUniversalDir = ArtRoot + "/TilePalettes/Universal";
        const string PaletteUniversalTilesDir = PaletteUniversalDir + "/Tiles";

        const string PlayerControllerPath = PlayerAnimDir + "/PlayerAnimator.controller";
        const string BluePortalControllerPath = PortalAnimDir + "/PortalAzul.controller";
        const string OrangePortalControllerPath = PortalAnimDir + "/PortalNaranja.controller";
        const string DeathPoofControllerPath = VfxAnimDir + "/Death_Poof.controller";
        const string DashRechargeControllerPath = VfxAnimDir + "/Dash_Recharge.controller";

        const string BluePortalPrefabPath = "Assets/_Juego/Prefabs/PortalAzul.prefab";
        const string OrangePortalPrefabPath = "Assets/_Juego/Prefabs/PortalNaranja.prefab";
        const string DeathPoofPrefabPath = VfxPrefabDir + "/DeathPoofVFX.prefab";
        const string DashRechargePrefabPath = VfxPrefabDir + "/DashRechargeVFX.prefab";

        const string CatDashShaderPath = DashSpentDir + "/Gato_DashColor.shader";
        const string CatDashMaterialPath = MaterialsDir + "/Gato_DashColor.mat";
        const string SpriteUnlitMaterialPath = "Assets/_Juego/Generated/PurrTales_SpriteUnlit.mat";

        const string CatIdleTexPath = ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Reposo_Idle_Redesign.png";
        const string CatRunTexPath = ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Corriendo_Run_Redesign.png";
        const string CatWalkTexPath = ArtRoot + "/Personaje (Rediseñado)/Gato_Negro_Caminando_Walk_Redesign.png";

        const string CatJumpTexPath = NewGameplayDir + "/Gato_Negro_Salto_JumpRise_Redesign.png";
        const string CatFallTexPath = NewGameplayDir + "/Gato_Negro_Caida_Fall_Redesign.png";
        const string CatWallSlideTexPath = NewGameplayDir + "/Gato_Negro_Pared_WallSlide_Redesign.png";
        const string CatDashTexPath = NewGameplayDir + "/Gato_Negro_Dash_Redesign.png";
        const string DeathPoofTexPath = NewGameplayDir + "/Efecto_Muerte_Poof_VFX_Redesign.png";
        const string DashRechargeTexPath = NewGameplayDir + "/Efecto_Dash_Recarga_VFX_Redesign.png";
        const string PortalSurfaceUniversalTexPath = NewGameplayDir + "/Superficie_Portal_Universal/Superficie_Portal_Universal.png";

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

        static readonly string[] UniversalPortalTileNames =
        {
            "Esquina_SupIzq", "Borde_Sup", "Esquina_SupDer", "Bloque_Suelto",
            "Borde_Izq", "Centro", "Borde_Der", "Columna_Arriba",
            "Esquina_InfIzq", "Borde_Inf", "Esquina_InfDer", "Columna_Centro",
            "Plataforma_Izq", "Plataforma_Centro", "Plataforma_Der", "Columna_Abajo"
        };

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

            // ---- Nuevos Assets de Gameplay: Gato y VFX 100x100 (PPU=32) ----
            { CatJumpTexPath, new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { CatFallTexPath, new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { CatWallSlideTexPath, new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { CatDashTexPath, new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { DeathPoofTexPath, new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },

            // ---- Variantes Dash Gastado 100x100 (PPU=32) ----
            { DashSpentDir + "/Gato_Negro_Salto_JumpRise_Redesign_DashGastado.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { DashSpentDir + "/Gato_Negro_Caida_Fall_Redesign_DashGastado.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { DashSpentDir + "/Gato_Negro_Pared_WallSlide_Redesign_DashGastado.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { DashSpentDir + "/Gato_Negro_Dash_Redesign_DashGastado.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { DashSpentDir + "/Gato_Negro_Reposo_Idle_Redesign_DashGastado.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { DashSpentDir + "/Gato_Negro_Caminando_Walk_Redesign_DashGastado.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { DashSpentDir + "/Gato_Negro_Corriendo_Run_Redesign_DashGastado.png", new SliceSpec(100, 100, CatPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },

            // ---- VFX de Recarga de Dash (32x32, PPU=16) y Superficie Universal de Portales (16x16, PPU=16) ----
            { DashRechargeTexPath, new SliceSpec(32, 32, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },
            { PortalSurfaceUniversalTexPath, new SliceSpec(16, 16, WorldPPU, SpriteAlignment.Center, new Vector2(0.5f, 0.5f)) },

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
            EditorUtility.DisplayDialog("Purr-tales", "Integración de sprites, animaciones, VFX, materiales, portales y Tile Palettes completada.", "OK");
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

            GameObject deathPoofPrefab = CreateVfxClipAndPrefab(
                "Death_Poof",
                DeathPoofTexPath,
                16f,
                VfxAnimDir + "/Death_Poof.anim",
                DeathPoofControllerPath,
                DeathPoofPrefabPath,
                12);

            GameObject dashRechargePrefab = CreateVfxClipAndPrefab(
                "Dash_Recharge",
                DashRechargeTexPath,
                16f,
                VfxAnimDir + "/Dash_Recharge.anim",
                DashRechargeControllerPath,
                DashRechargePrefabPath,
                12);

            Material catDashMat = CreateOrUpdateCatDashMaterial();

            UpdatePortalPrefab(BluePortalPrefabPath, PortalColor.Blue, PortalBlueFloorTexPath, bluePortalController);
            UpdatePortalPrefab(OrangePortalPrefabPath, PortalColor.Orange, PortalOrangeFloorTexPath, orangePortalController);

            UpdatePlayerInScene(playerController, catDashMat, deathPoofPrefab, dashRechargePrefab);
            CreateLevel1TilePalettes();
            CreateUniversalPortalSurfacePalette();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            VerifyIntegration();
            Debug.Log("[PurrTalesArtSetup] Integración artística completada correctamente.");
        }

        static void VerifyIntegration()
        {
            // Sprites existentes
            AssertSpritesCountAndPpu(CatIdleTexPath, 10, CatPPU);
            AssertSpritesCountAndPpu(CatWalkTexPath, 8, CatPPU);
            AssertSpritesCountAndPpu(CatRunTexPath, 8, CatPPU);

            // Nuevos sprites de gameplay y VFX
            AssertSpritesCountAndPpu(CatJumpTexPath, 4, CatPPU);
            AssertSpritesCountAndPpu(CatFallTexPath, 4, CatPPU);
            AssertSpritesCountAndPpu(CatWallSlideTexPath, 4, CatPPU);
            AssertSpritesCountAndPpu(CatDashTexPath, 4, CatPPU);
            AssertSpritesCountAndPpu(DeathPoofTexPath, 5, CatPPU);
            AssertSpritesCountAndPpu(DashRechargeTexPath, 4, WorldPPU);
            AssertSpritesCountAndPpu(PortalSurfaceUniversalTexPath, 64, WorldPPU);

            // Variantes Dash_Gastado
            AssertSpritesCountAndPpu(DashSpentDir + "/Gato_Negro_Salto_JumpRise_Redesign_DashGastado.png", 4, CatPPU);
            AssertSpritesCountAndPpu(DashSpentDir + "/Gato_Negro_Caida_Fall_Redesign_DashGastado.png", 4, CatPPU);
            AssertSpritesCountAndPpu(DashSpentDir + "/Gato_Negro_Pared_WallSlide_Redesign_DashGastado.png", 4, CatPPU);
            AssertSpritesCountAndPpu(DashSpentDir + "/Gato_Negro_Dash_Redesign_DashGastado.png", 4, CatPPU);
            AssertSpritesCountAndPpu(DashSpentDir + "/Gato_Negro_Reposo_Idle_Redesign_DashGastado.png", 10, CatPPU);
            AssertSpritesCountAndPpu(DashSpentDir + "/Gato_Negro_Caminando_Walk_Redesign_DashGastado.png", 8, CatPPU);
            AssertSpritesCountAndPpu(DashSpentDir + "/Gato_Negro_Corriendo_Run_Redesign_DashGastado.png", 8, CatPPU);

            // Resto de sprites de niveles y portales
            AssertSpritesCountAndPpu(N1TilesetTexPath, 48, WorldPPU);
            AssertSpritesCountAndPpu(ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Tileset_Torre.png", 48, WorldPPU);
            AssertSpritesCountAndPpu(ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Tileset_Fortaleza.png", 48, WorldPPU);
            AssertSpritesCountAndPpu(ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Slime.png", 11, WorldPPU);
            AssertSpritesCountAndPpu(ArtRoot + "/Purr-tales/Nivel1_Grutas/N1_Murcielago.png", 11, WorldPPU);
            AssertSpritesCountAndPpu(N1SpikesTexPath, 4, WorldPPU);
            AssertSpritesCountAndPpu(ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Pinchos_Hierro.png", 4, WorldPPU);
            AssertSpritesCountAndPpu(ArtRoot + "/Purr-tales/Nivel2_Torre/N2_Armadura_Animada.png", 11, WorldPPU);
            AssertSpritesCountAndPpu(ArtRoot + "/Purr-tales/Nivel3_Fortaleza/N3_Craneo_Flotante.png", 11, WorldPPU);
            AssertSpritesCountAndPpu(ArtRoot + "/Purr-tales/Compartido/Personajes/Hechicera.png", 10, WorldPPU);
            AssertSpritesCountAndPpu(ArtRoot + "/Purr-tales/Compartido/Personajes/Villano.png", 21, WorldPPU);
            AssertSpritesCountAndPpu(PortalBlueFloorTexPath, 12, WorldPPU);
            AssertSpritesCountAndPpu(PortalBlueWallTexPath, 12, WorldPPU);
            AssertSpritesCountAndPpu(PortalOrangeFloorTexPath, 12, WorldPPU);
            AssertSpritesCountAndPpu(PortalOrangeWallTexPath, 12, WorldPPU);

            // Clips de jugador
            VerifyClip(PlayerAnimDir + "/Idle.anim", 10, 8f, true);
            VerifyClip(PlayerAnimDir + "/Walk.anim", 8, 10f, true);
            VerifyClip(PlayerAnimDir + "/Run.anim", 8, 16f, true);
            VerifyClip(PlayerAnimDir + "/Jump.anim", 4, 12f, false);
            VerifyClip(PlayerAnimDir + "/Fall.anim", 4, 12f, true);
            VerifyClip(PlayerAnimDir + "/WallSlide.anim", 4, 10f, true);
            VerifyClip(PlayerAnimDir + "/Dash.anim", 4, 16f, true);

            // Clips de VFX
            VerifyClip(VfxAnimDir + "/Death_Poof.anim", 5, 16f, false);
            VerifyClip(VfxAnimDir + "/Dash_Recharge.anim", 4, 16f, false);

            // PlayerAnimator.controller
            var playerCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (playerCtrl == null || playerCtrl.parameters.Length != 5)
                throw new InvalidOperationException("PlayerAnimator.controller debe tener 5 parámetros.");
            AnimatorStateMachine sm = playerCtrl.layers[0].stateMachine;
            if (sm.states.Length != 7)
                throw new InvalidOperationException("PlayerAnimator.controller debe tener 7 estados (encontrados: " + sm.states.Length + ").");

            // Shader y material URP 2D
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(CatDashShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Gato_DashColor.shader falta o tiene errores de compilación.");
            ShaderMessage[] shaderMessages = ShaderUtil.GetShaderMessages(shader);
            if (shaderMessages != null && shaderMessages.Length > 0)
            {
                foreach (ShaderMessage msg in shaderMessages)
                {
                    Debug.LogError($"[PurrTalesArtSetup] Shader message ({msg.severity}) en {CatDashShaderPath}:{msg.line}: {msg.message} ({msg.messageDetails})");
                }
                throw new InvalidOperationException("Gato_DashColor.shader tiene advertencias o errores de compilación (" + shaderMessages.Length + ").");
            }
            var catMat = AssetDatabase.LoadAssetAtPath<Material>(CatDashMaterialPath);
            if (catMat == null || catMat.shader != shader)
                throw new InvalidOperationException("Gato_DashColor.mat inválido o no usa PurrTales/GatoDashColor.");

            // Prefabs de VFX
            VerifyVfxPrefab(DeathPoofPrefabPath);
            VerifyVfxPrefab(DashRechargePrefabPath);

            // Prefabs de portales
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

            // Tile Palettes
            var p1 = AssetDatabase.LoadAssetAtPath<GameObject>(PaletteN1Dir + "/Paleta_N1_Grutas_Terreno.prefab");
            var p2 = AssetDatabase.LoadAssetAtPath<GameObject>(PaletteN1Dir + "/Paleta_N1_Grutas_Decoracion_Peligros.prefab");
            var pUniversal = AssetDatabase.LoadAssetAtPath<GameObject>(PaletteUniversalDir + "/Paleta_Superficie_Portal_Universal.prefab");
            if (p1 == null || p2 == null || pUniversal == null)
                throw new InvalidOperationException("Faltan Tile Palettes de Nivel 1 o Superficie Universal de Portales.");

            int portalLayer = LayerMask.NameToLayer(GameLayers.PortalSurface);
            if (portalLayer < 0 || pUniversal.layer != portalLayer)
                throw new InvalidOperationException("Paleta_Superficie_Portal_Universal no está asociada a la Layer PortalSurface.");
            Transform universalLayerTf = pUniversal.transform.Find("Layer1");
            if (universalLayerTf == null || universalLayerTf.gameObject.layer != portalLayer)
                throw new InvalidOperationException("Layer1 de Paleta_Superficie_Portal_Universal no está en la Layer PortalSurface.");

            // Verificar escena PurrTales_Prueba.unity
            Scene scene = EditorSceneManager.OpenScene(PurrTalesSceneBuilder.GameScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null)
                throw new InvalidOperationException("No se encontró Player en PurrTales_Prueba.unity.");
            Transform visualTf = player.transform.Find("Visual");
            if (visualTf == null || visualTf.localScale != Vector3.one)
                throw new InvalidOperationException("Visual del Player tiene escala incorrecta.");
            var sr = visualTf.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sharedMaterial != catMat)
                throw new InvalidOperationException("Visual del Player no tiene Gato_DashColor.mat asignado.");
            var death = player.GetComponent<PlayerDeathHandler>();
            var dash = player.GetComponent<PlayerDash>();
            if (death == null || death.deathPoofPrefab == null)
                throw new InvalidOperationException("PlayerDeathHandler no tiene deathPoofPrefab asignado.");
            if (dash == null || dash.dashRechargeVfxPrefab == null)
                throw new InvalidOperationException("PlayerDash no tiene dashRechargeVfxPrefab asignado.");

            Debug.Log("[PurrTalesArtSetup] Verificación OK: slices, PPU, clips, PlayerAnimator (5 params / 7 estados), shader URP 2D, VFX prefabs, escena PurrTales_Prueba y Tile Palettes validados.");
        }

        static void VerifyVfxPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Falta prefab VFX: " + prefabPath);
            if (prefab.GetComponent<SpriteRenderer>() == null ||
                prefab.GetComponent<Animator>() == null ||
                prefab.GetComponent<AutoDestroyVFX>() == null)
            {
                throw new InvalidOperationException("Prefab VFX incompleto: " + prefabPath);
            }
        }

        static void AssertSpritesCountAndPpu(string texPath, int expectedCount, float expectedPpu)
        {
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti == null)
                throw new InvalidOperationException("TextureImporter faltante en " + texPath);
            if (Mathf.Abs(ti.spritePixelsPerUnit - expectedPpu) > 0.01f)
                throw new InvalidOperationException($"PPU mismatch en {texPath}: esperado {expectedPpu}, encontrado {ti.spritePixelsPerUnit}");
            if (ti.filterMode != FilterMode.Point)
                throw new InvalidOperationException($"FilterMode != Point en {texPath}");
            if (ti.textureCompression != TextureImporterCompression.Uncompressed)
                throw new InvalidOperationException($"Compression != None en {texPath}");

            Sprite[] sprites = LoadSlicedSprites(texPath);
            if (sprites.Length != expectedCount)
                throw new InvalidOperationException($"Sprite count mismatch en {texPath}: esperado {expectedCount}, encontrado {sprites.Length}");
        }

        static void VerifyClip(string clipPath, int expectedFrames, float expectedFps, bool expectedLoop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) throw new InvalidOperationException("Falta clip " + clipPath);
            if (clip.isLooping != expectedLoop)
                throw new InvalidOperationException($"LoopTime mismatch en {clipPath}: esperado {expectedLoop}, encontrado {clip.isLooping}");
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
            EnsureFolder(ArtRoot + "/Animations", "VFX");
            EnsureFolder(ArtRoot, "Materials");
            EnsureFolder(ArtRoot, "TilePalettes");
            EnsureFolder(ArtRoot + "/TilePalettes", "Nivel1_Grutas");
            EnsureFolder(PaletteN1Dir, "Tiles");
            EnsureFolder(ArtRoot + "/TilePalettes", "Universal");
            EnsureFolder(PaletteUniversalDir, "Tiles");
            EnsureFolder("Assets/_Juego", "Prefabs");
            EnsureFolder("Assets/_Juego/Prefabs", "VFX");
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
        // 3. Animaciones del Jugador, VFX y Portales
        // =====================================================================

        static AnimatorController CreatePlayerAnimations()
        {
            Sprite[] idleSprites = LoadSlicedSprites(CatIdleTexPath);
            Sprite[] walkSprites = LoadSlicedSprites(CatWalkTexPath);
            Sprite[] runSprites = LoadSlicedSprites(CatRunTexPath);
            Sprite[] jumpSprites = LoadSlicedSprites(CatJumpTexPath);
            Sprite[] fallSprites = LoadSlicedSprites(CatFallTexPath);
            Sprite[] wallSlideSprites = LoadSlicedSprites(CatWallSlideTexPath);
            Sprite[] dashSprites = LoadSlicedSprites(CatDashTexPath);

            AnimationClip idleClip = CreateSpriteAnimationClip(PlayerAnimDir + "/Idle.anim", idleSprites, 8f, true);
            AnimationClip walkClip = CreateSpriteAnimationClip(PlayerAnimDir + "/Walk.anim", walkSprites, 10f, true);
            AnimationClip runClip = CreateSpriteAnimationClip(PlayerAnimDir + "/Run.anim", runSprites, 16f, true);
            AnimationClip jumpClip = CreateSpriteAnimationClip(PlayerAnimDir + "/Jump.anim", jumpSprites, 12f, false);
            AnimationClip fallClip = CreateSpriteAnimationClip(PlayerAnimDir + "/Fall.anim", fallSprites, 12f, true);
            AnimationClip wallSlideClip = CreateSpriteAnimationClip(PlayerAnimDir + "/WallSlide.anim", wallSlideSprites, 10f, true);
            AnimationClip dashClip = CreateSpriteAnimationClip(PlayerAnimDir + "/Dash.anim", dashSprites, 16f, true);

            if (File.Exists(PlayerControllerPath)) AssetDatabase.DeleteAsset(PlayerControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsWallSliding", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idleState = sm.AddState("Idle", new Vector3(260, 120, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState walkState = sm.AddState("Walk", new Vector3(540, 40, 0));
            walkState.motion = walkClip;

            AnimatorState runState = sm.AddState("Run", new Vector3(540, 200, 0));
            runState.motion = runClip;

            AnimatorState jumpState = sm.AddState("Jump", new Vector3(260, -100, 0));
            jumpState.motion = jumpClip;

            AnimatorState fallState = sm.AddState("Fall", new Vector3(260, 320, 0));
            fallState.motion = fallClip;

            AnimatorState wallSlideState = sm.AddState("WallSlide", new Vector3(0, 320, 0));
            wallSlideState.motion = wallSlideClip;

            AnimatorState dashState = sm.AddState("Dash", new Vector3(0, -100, 0));
            dashState.motion = dashClip;

            // ---------- Transiciones AnyState (prioridad: Dash > WallSlide > Jump > Fall) ----------
            AnimatorStateTransition anyToDash = AddInstantAnyStateTransition(sm, dashState);
            anyToDash.AddCondition(AnimatorConditionMode.If, 0f, "IsDashing");

            AnimatorStateTransition anyToWallSlide = AddInstantAnyStateTransition(sm, wallSlideState);
            anyToWallSlide.AddCondition(AnimatorConditionMode.If, 0f, "IsWallSliding");
            anyToWallSlide.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            anyToWallSlide.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");

            AnimatorStateTransition anyToJump = AddInstantAnyStateTransition(sm, jumpState);
            anyToJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            anyToJump.AddCondition(AnimatorConditionMode.Greater, 0.5f, "VerticalVelocity");
            anyToJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWallSliding");
            anyToJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");

            AnimatorStateTransition anyToFall = AddInstantAnyStateTransition(sm, fallState);
            anyToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            anyToFall.AddCondition(AnimatorConditionMode.Less, -0.5f, "VerticalVelocity");
            anyToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWallSliding");
            anyToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");

            // ---------- Movimiento en suelo (Idle <-> Walk <-> Run) ----------
            AnimatorStateTransition tIdleToRun = AddInstantTransition(idleState, runState);
            tIdleToRun.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tIdleToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            tIdleToRun.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Speed");

            AnimatorStateTransition tIdleToWalk = AddInstantTransition(idleState, walkState);
            tIdleToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tIdleToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            tIdleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition tWalkToRun = AddInstantTransition(walkState, runState);
            tWalkToRun.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tWalkToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            tWalkToRun.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Speed");

            AnimatorStateTransition tWalkToIdle = AddInstantTransition(walkState, idleState);
            tWalkToIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tWalkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            tWalkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AnimatorStateTransition tRunToIdle = AddInstantTransition(runState, idleState);
            tRunToIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tRunToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            tRunToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AnimatorStateTransition tRunToWalk = AddInstantTransition(runState, walkState);
            tRunToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            tRunToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            tRunToWalk.AddCondition(AnimatorConditionMode.Less, 3.0f, "Speed");

            // ---------- Aterrizaje desde estados de aire/pared/dash hacia Run / Walk / Idle ----------
            AddLandingTransitions(jumpState, idleState, walkState, runState);
            AddLandingTransitions(fallState, idleState, walkState, runState);
            AddLandingTransitions(wallSlideState, idleState, walkState, runState);
            AddLandingTransitions(dashState, idleState, walkState, runState);

            // ---------- Salida en aire desde Dash y WallSlide (incluye tramo -0.5..0.5 de VerticalVelocity) ----------
            AnimatorStateTransition tDashToFall = AddInstantTransition(dashState, fallState);
            tDashToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            tDashToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            tDashToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWallSliding");
            tDashToFall.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalVelocity");

            AnimatorStateTransition tWallSlideToFall = AddInstantTransition(wallSlideState, fallState);
            tWallSlideToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWallSliding");
            tWallSlideToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            tWallSlideToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            tWallSlideToFall.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalVelocity");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void AddLandingTransitions(AnimatorState from, AnimatorState idleState, AnimatorState walkState, AnimatorState runState)
        {
            // Orden: Run (> 3.5) -> Walk (> 0.1) -> Idle (< 0.1)
            AnimatorStateTransition toRun = AddInstantTransition(from, runState);
            toRun.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            toRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            toRun.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Speed");

            AnimatorStateTransition toWalk = AddInstantTransition(from, walkState);
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            toWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition toIdle = AddInstantTransition(from, idleState);
            toIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        static GameObject CreateVfxClipAndPrefab(
            string stateName,
            string texPath,
            float fps,
            string clipPath,
            string controllerPath,
            string prefabPath,
            int sortingOrder)
        {
            Sprite[] sprites = LoadSlicedSprites(texPath);
            AnimationClip clip = CreateSpriteAnimationClip(clipPath, sprites, fps, false);

            if (File.Exists(controllerPath)) AssetDatabase.DeleteAsset(controllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.AddState(stateName, new Vector3(260, 100, 0));
            state.motion = clip;
            sm.defaultState = state;
            EditorUtility.SetDirty(controller);

            Material vfxMat = AssetDatabase.LoadAssetAtPath<Material>(SpriteUnlitMaterialPath);
            var go = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[0];
            if (vfxMat != null) sr.sharedMaterial = vfxMat;
            sr.color = Color.white;
            sr.sortingOrder = sortingOrder;

            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            var autoDestroy = go.AddComponent<AutoDestroyVFX>();
            autoDestroy.fallbackDuration = sprites.Length / fps;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            EditorUtility.SetDirty(prefab);
            return prefab;
        }

        static Material CreateOrUpdateCatDashMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(CatDashShaderPath);
            if (shader == null) shader = Shader.Find("PurrTales/GatoDashColor");
            if (shader == null)
                throw new InvalidOperationException("No se encontró el shader PurrTales/GatoDashColor en " + CatDashShaderPath);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CatDashMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, CatDashMaterialPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_DashListo", 1f);
            mat.SetFloat("_Brillo", 0.25f);
            EditorUtility.SetDirty(mat);
            return mat;
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

        static AnimatorStateTransition AddInstantAnyStateTransition(AnimatorStateMachine sm, AnimatorState to)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.exitTime = 0f;
            t.duration = 0f;
            t.hasFixedDuration = true;
            t.offset = 0f;
            t.canTransitionToSelf = false;
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

        static void UpdatePlayerInScene(
            AnimatorController playerController,
            Material catDashMat,
            GameObject deathPoofPrefab,
            GameObject dashRechargePrefab)
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
            if (catDashMat != null) sr.sharedMaterial = catDashMat;
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
                dash.dashRechargeVfxPrefab = dashRechargePrefab;
                EditorUtility.SetDirty(dash);
            }

            var death = player.GetComponent<PlayerDeathHandler>();
            if (death != null)
            {
                death.spriteRenderer = sr;
                death.deathPoofPrefab = deathPoofPrefab;
                EditorUtility.SetDirty(death);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        // =====================================================================
        // 5. Tile Palettes (Nivel 1 y Superficie Universal de Portales)
        // =====================================================================

        static void CreateLevel1TilePalettes()
        {
            // Paleta 1: Terreno (8x6 = 48 tiles de N1_Tileset_Grutas)
            Sprite[] terrainSprites = LoadSlicedSprites(N1TilesetTexPath);
            BuildGridPalette(
                PaletteN1Dir + "/Paleta_N1_Grutas_Terreno.prefab",
                "Paleta_N1_Grutas_Terreno",
                0,
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
                0,
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

        static void CreateUniversalPortalSurfacePalette()
        {
            Sprite[] sheetSprites = LoadSlicedSprites(PortalSurfaceUniversalTexPath);
            if (sheetSprites.Length != 64)
                throw new InvalidOperationException("Superficie_Portal_Universal.png debe tener 64 sprites (16x4), encontrados: " + sheetSprites.Length);

            int portalSurfaceLayer = LayerMask.NameToLayer(GameLayers.PortalSurface);
            if (portalSurfaceLayer < 0)
                throw new InvalidOperationException("No existe la Layer 'PortalSurface' en ProjectSettings/TagManager.asset.");

            // Hoja: 4x4 por frame, 4 frames horizontales (16 cols x 4 rows), 150 ms por frame (~6.67 FPS).
            const int tileCols = 4;
            const int tileRows = 4;
            const int frameCount = 4;
            const int sheetCols = tileCols * frameCount;
            const float animFps = 1000f / 150f;

            BuildGridPalette(
                PaletteUniversalDir + "/Paleta_Superficie_Portal_Universal.prefab",
                "Paleta_Superficie_Portal_Universal",
                portalSurfaceLayer,
                (tilemap) =>
                {
                    for (int row = 0; row < tileRows; row++)
                    {
                        for (int col = 0; col < tileCols; col++)
                        {
                            int tileIdx = row * tileCols + col;
                            var frames = new Sprite[frameCount];
                            for (int f = 0; f < frameCount; f++)
                            {
                                int spriteIndex = row * sheetCols + (f * tileCols + col);
                                frames[f] = sheetSprites[spriteIndex];
                            }

                            string tileName = tileIdx < UniversalPortalTileNames.Length
                                ? UniversalPortalTileNames[tileIdx]
                                : ("Tile_" + tileIdx);
                            string assetPath = $"{PaletteUniversalTilesDir}/PortalSurface_{tileIdx:D2}_{tileName}.asset";

                            AnimatedTile animTile = CreateOrUpdateAnimatedTileAsset(assetPath, frames, animFps, Tile.ColliderType.Grid);
                            tilemap.SetTile(new Vector3Int(col, (tileRows - 1) - row, 0), animTile);
                        }
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

        static AnimatedTile CreateOrUpdateAnimatedTileAsset(string assetPath, Sprite[] frames, float fps, Tile.ColliderType colliderType)
        {
            AnimatedTile tile = AssetDatabase.LoadAssetAtPath<AnimatedTile>(assetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<AnimatedTile>();
                AssetDatabase.CreateAsset(tile, assetPath);
            }
            tile.m_AnimatedSprites = frames;
            tile.m_MinSpeed = fps;
            tile.m_MaxSpeed = fps;
            tile.m_AnimationStartTime = 0f;
            tile.m_AnimationStartFrame = 0;
            tile.m_TileColliderType = colliderType;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        static void BuildGridPalette(string prefabPath, string paletteName, int layerIndex, Action<Tilemap> populate)
        {
            if (File.Exists(prefabPath)) AssetDatabase.DeleteAsset(prefabPath);

            var root = new GameObject(paletteName);
            root.layer = layerIndex;
            var grid = root.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellGap = Vector3.zero;

            var layer = new GameObject("Layer1");
            layer.layer = layerIndex;
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
