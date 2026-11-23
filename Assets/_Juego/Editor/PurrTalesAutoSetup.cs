#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PurrTales.EditorTools
{
    /// <summary>
    /// Primer arranque del proyecto: en cuanto Unity termina de importar y compilar,
    /// genera la escena del juego (layers, sprites, prefabs, player y nivel) y la deja abierta,
    /// lista para dar Play. Solo ocurre una vez: después se guarda una marca en ProjectSettings
    /// (que va al repositorio) y la escena ya existe como asset normal para seguir editándola.
    /// </summary>
    [InitializeOnLoad]
    static class PurrTalesAutoSetup
    {
        const string MarkerPath = "ProjectSettings/PurrTalesSetup.txt";
        const string SessionKey = "PurrTales.AutoSetup.Attempted";

        static PurrTalesAutoSetup()
        {
            if (File.Exists(MarkerPath)) return;
            EditorApplication.delayCall += TryRun;
        }

        static void TryRun()
        {
            if (File.Exists(MarkerPath) || SessionState.GetBool(SessionKey, false)) return;

            // Esperar a que el editor termine de importar/compilar.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            SessionState.SetBool(SessionKey, true);

            if (File.Exists(PurrTalesSceneBuilder.GameScenePath))
            {
                // La escena ya viene en el proyecto (p. ej. clonado del repo): solo abrirla.
                EditorSceneManager.OpenScene(PurrTalesSceneBuilder.GameScenePath);
                WriteMarker();
                return;
            }

            Debug.Log("[PurrTales] Primer arranque: generando la escena del juego...");
            if (PurrTalesSceneBuilder.Build(false)) WriteMarker();
        }

        static void WriteMarker()
        {
            File.WriteAllText(MarkerPath,
                "Purr-tales: configuración inicial completada.\n" +
                "Borra este archivo para que la escena se vuelva a generar automáticamente al abrir el proyecto.\n");
        }
    }
}
#endif
