using System;
using System.Collections;
using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Muerte y reaparición instantánea estilo Super Meat Boy.
    /// Al tocar la capa Hazard o caer por debajo de <see cref="killY"/>:
    /// velocidad a cero, limpia portales, recarga dash y reaparece en el último Checkpoint en menos de 0.25 s.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        [Header("Referencias")]
        public Rigidbody2D rb;
        public PlayerMovement movement;
        public PlayerDash dash;
        public PlayerInputReader input;
        public SpriteRenderer spriteRenderer;
        public TrailRenderer trail;

        [Header("Muerte")]
        public LayerMask hazardMask;
        [Tooltip("Si el jugador cae por debajo de esta altura, muere (vacío).")]
        public float killY = -15f;
        [Tooltip("Tiempo total hasta reaparecer. Se limita a < 0.25 s.")]
        [Range(0f, 0.24f)] public float respawnDelay = 0.12f;
        [Tooltip("Pequeña invulnerabilidad al reaparecer para no morir en bucle si el checkpoint toca un peligro.")]
        public float respawnGraceTime = 0.1f;

        public bool IsDead { get; private set; }
        public int DeathCount { get; private set; }

        public event Action Died;
        public event Action Respawned;

        Vector3 fallbackSpawn;
        float graceUntil;

        void Reset()
        {
            rb = GetComponent<Rigidbody2D>();
            movement = GetComponent<PlayerMovement>();
            dash = GetComponent<PlayerDash>();
            input = GetComponent<PlayerInputReader>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            trail = GetComponentInChildren<TrailRenderer>();
        }

        void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (dash == null) dash = GetComponent<PlayerDash>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            fallbackSpawn = transform.position;
        }

        void Update()
        {
            if (!IsDead && transform.position.y < killY) Die();
        }

        bool IsHazard(GameObject go)
        {
            return go != null && (hazardMask.value & (1 << go.layer)) != 0;
        }

        void OnCollisionEnter2D(Collision2D collision) { if (IsHazard(collision.gameObject)) Die(); }
        void OnCollisionStay2D(Collision2D collision) { if (IsHazard(collision.gameObject)) Die(); }
        void OnTriggerEnter2D(Collider2D other) { if (IsHazard(other.gameObject)) Die(); }
        void OnTriggerStay2D(Collider2D other) { if (IsHazard(other.gameObject)) Die(); }

        /// <summary>Mata al jugador y lo reaparece en el último checkpoint.</summary>
        public void Die()
        {
            if (IsDead || Time.time < graceUntil) return;
            IsDead = true;
            DeathCount++;

            if (dash != null)
            {
                dash.CancelDash();
                dash.RechargeDash();
            }
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }
            if (input != null) input.InputLocked = true;
            if (PortalManager.Instance != null) PortalManager.Instance.ClearAllPortals();
            if (spriteRenderer != null) spriteRenderer.enabled = false;

            if (Died != null) Died();

            if (respawnDelay <= 0f) Respawn();
            else StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(Mathf.Min(respawnDelay, 0.24f));
            Respawn();
        }

        void Respawn()
        {
            Vector3 target = fallbackSpawn;
            if (CheckpointManager.Instance != null) target = CheckpointManager.Instance.GetRespawnPosition(fallbackSpawn);

            transform.position = target;
            if (rb != null)
            {
                rb.position = target;
                rb.linearVelocity = Vector2.zero;
                rb.simulated = true;
            }
            if (movement != null) movement.ResetState();
            if (dash != null) dash.RechargeDash();
            if (trail != null) trail.Clear();
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            if (input != null) input.InputLocked = false;

            graceUntil = Time.time + respawnGraceTime;
            IsDead = false;
            if (Respawned != null) Respawned();
        }
    }
}
