using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Checkpoint por trigger: al tocarlo el jugador queda registrado como punto de reaparición.
    /// La posición de reaparición es transform.position + respawnOffset.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        public Vector2 respawnOffset = new Vector2(0f, 0.5f);
        public SpriteRenderer flagRenderer;
        public Color inactiveColor = new Color(0.45f, 0.45f, 0.5f, 1f);
        public Color activeColor = new Color(0.35f, 1f, 0.55f, 1f);

        public Vector3 RespawnPosition
        {
            get { return transform.position + (Vector3)respawnOffset; }
        }

        void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            SetActiveVisual(false);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (CheckpointManager.Instance == null) return;
            Rigidbody2D body = other.attachedRigidbody;
            if (body == null) return;
            PlayerDeathHandler player = body.GetComponent<PlayerDeathHandler>();
            if (player == null || player.IsDead) return;
            CheckpointManager.Instance.SetCheckpoint(this);
        }

        public void SetActiveVisual(bool active)
        {
            if (flagRenderer != null) flagRenderer.color = active ? activeColor : inactiveColor;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(RespawnPosition, 0.25f);
        }
    }
}
