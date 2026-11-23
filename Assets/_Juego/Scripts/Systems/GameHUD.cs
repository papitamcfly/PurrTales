using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// HUD de depuración con IMGUI (sin dependencias de Canvas): muertes, dash, portales,
    /// avisos de checkpoint/meta y controles. Pensado para el nivel de prueba.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public PlayerDeathHandler player;
        public PlayerDash dash;
        public PortalManager portalManager;
        public LevelGoal goal;
        public bool showControls = true;

        string toast;
        float toastUntil;
        GUIStyle label, big;

        void OnEnable()
        {
            if (CheckpointManager.Instance != null) CheckpointManager.Instance.CheckpointChanged += OnCheckpoint;
            if (goal != null) goal.GoalReached += OnGoal;
        }

        void Start()
        {
            // Por si CheckpointManager despertó después de este componente.
            if (CheckpointManager.Instance != null)
            {
                CheckpointManager.Instance.CheckpointChanged -= OnCheckpoint;
                CheckpointManager.Instance.CheckpointChanged += OnCheckpoint;
            }
        }

        void OnDisable()
        {
            if (CheckpointManager.Instance != null) CheckpointManager.Instance.CheckpointChanged -= OnCheckpoint;
            if (goal != null) goal.GoalReached -= OnGoal;
        }

        void OnCheckpoint(Checkpoint cp)
        {
            Toast("¡Checkpoint!", 1.2f);
        }

        void OnGoal(LevelGoal g)
        {
            int deaths = player != null ? player.DeathCount : 0;
            Toast(string.Format("¡Nivel completado!  Tiempo: {0:0.00}s  ·  Muertes: {1}", g.ReachedAtTime, deaths), 6f);
        }

        void Toast(string text, float seconds)
        {
            toast = text;
            toastUntil = Time.time + seconds;
        }

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                label.normal.textColor = Color.white;
                big = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                big.normal.textColor = new Color(1f, 0.9f, 0.5f);
            }

            int deaths = player != null ? player.DeathCount : 0;
            string dashText = dash != null ? (dash.Charges > 0 ? "LISTO" : "—") : "?";
            string blue = portalManager != null && portalManager.BluePortal != null && portalManager.BluePortal.IsActive ? "●" : "○";
            string orange = portalManager != null && portalManager.OrangePortal != null && portalManager.OrangePortal.IsActive ? "●" : "○";

            GUI.Label(new Rect(16, 12, 600, 24), "Purr-tales · Escena de prueba", label);
            GUI.Label(new Rect(16, 36, 600, 24), string.Format("Muertes: {0}    Dash: {1}    Portal azul: {2}   Portal naranja: {3}", deaths, dashText, blue, orange), label);

            if (showControls)
            {
                GUI.Label(new Rect(16, Screen.height - 34, 1200, 24),
                    "A/D o ←/→ mover · Espacio saltar (mantener = más alto) · Shift dash 8 dir. · Clic izq. portal azul · Clic der. portal naranja",
                    label);
            }

            if (!string.IsNullOrEmpty(toast) && Time.time < toastUntil)
            {
                GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 40), toast, big);
            }
        }
    }
}
