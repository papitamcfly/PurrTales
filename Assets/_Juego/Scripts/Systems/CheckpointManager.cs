using System;
using UnityEngine;

namespace PurrTales
{
    /// <summary>Guarda el último checkpoint alcanzado y entrega la posición de reaparición.</summary>
    [DisallowMultipleComponent]
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        [Tooltip("Checkpoint inicial del nivel (opcional).")]
        public Checkpoint initialCheckpoint;
        [Tooltip("Punto de reaparición si no hay ningún checkpoint activado.")]
        public Transform defaultSpawnPoint;

        public Checkpoint CurrentCheckpoint { get; private set; }
        public event Action<Checkpoint> CheckpointChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PurrTales] Hay más de un CheckpointManager en la escena; se usa el primero.");
                enabled = false;
                return;
            }
            Instance = this;
        }

        void Start()
        {
            if (initialCheckpoint != null) SetCheckpoint(initialCheckpoint, false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetCheckpoint(Checkpoint checkpoint, bool notify = true)
        {
            if (checkpoint == null || checkpoint == CurrentCheckpoint) return;
            if (CurrentCheckpoint != null) CurrentCheckpoint.SetActiveVisual(false);
            CurrentCheckpoint = checkpoint;
            CurrentCheckpoint.SetActiveVisual(true);
            if (notify && CheckpointChanged != null) CheckpointChanged(checkpoint);
        }

        public Vector3 GetRespawnPosition(Vector3 fallback)
        {
            if (CurrentCheckpoint != null) return CurrentCheckpoint.RespawnPosition;
            if (defaultSpawnPoint != null) return defaultSpawnPoint.position;
            return fallback;
        }
    }
}
