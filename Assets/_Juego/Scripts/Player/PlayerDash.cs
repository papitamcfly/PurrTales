using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Dash en 8 direcciones estilo Celeste.
    ///  - Shift: dash hacia el input (8 direcciones) o hacia donde mira el jugador si no hay input.
    ///  - Gravedad anulada y velocidad constante durante <see cref="dashDuration"/> (0.16 s).
    ///  - 1 carga; se recarga al tocar suelo o al cruzar un portal (<see cref="RechargeDash"/>).
    ///  - <see cref="RedirectDash"/> rota el dash al atravesar un portal sin perder velocidad ni reiniciar la duración.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerDash : MonoBehaviour
    {
        [Header("Referencias")]
        public Rigidbody2D rb;
        public PlayerMovement movement;
        public PlayerInputReader input;
        public TrailRenderer trail;
        [Tooltip("Sprite que se tiñe cuando no hay carga de dash (feedback tipo Celeste).")]
        public SpriteRenderer spriteRenderer;

        [Header("Dash")]
        public float dashSpeed = 22f;
        public float dashDuration = 0.16f;
        public int maxCharges = 1;
        [Tooltip("Tiempo mínimo entre dashes.")]
        public float dashCooldown = 0.05f;
        [Tooltip("Margen para aceptar el botón de dash un poco antes de que haya carga.")]
        public float dashInputBuffer = 0.08f;
        [Tooltip("Fracción de la velocidad del dash que se conserva al terminar (si no pasó por un portal).")]
        [Range(0f, 1f)] public float endSpeedMultiplier = 0.55f;
        [Tooltip("Bloqueo de frenado horizontal al terminar un dash redirigido por portal.")]
        public float redirectedEndBrakeLock = 0.15f;
        [Range(0f, 1f)] public float inputDeadzone = 0.3f;

        [Header("Feedback")]
        public Color normalTint = Color.white;
        public Color noChargeTint = new Color(0.55f, 0.75f, 1f, 1f);

        public bool IsDashing { get; private set; }
        public Vector2 DashDirection { get; private set; }
        public int Charges { get; private set; }
        public float CurrentDashSpeed { get; private set; }

        float dashEndTime;
        float lastDashTime = -999f;
        float lastDashPressedTime = -999f;
        bool redirectedThisDash;

        void Reset()
        {
            rb = GetComponent<Rigidbody2D>();
            movement = GetComponent<PlayerMovement>();
            input = GetComponent<PlayerInputReader>();
            trail = GetComponentInChildren<TrailRenderer>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (trail == null) trail = GetComponentInChildren<TrailRenderer>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            Charges = maxCharges;
            DashDirection = Vector2.right;
            if (trail != null) trail.emitting = false;
            RefreshTint();
        }

        void Update()
        {
            if (input == null) return;
            if (input.DashPressed) lastDashPressedTime = Time.time;

            bool buffered = Time.time - lastDashPressedTime <= dashInputBuffer;
            if (buffered && !IsDashing && Charges > 0 && Time.time - lastDashTime >= dashCooldown)
            {
                lastDashPressedTime = -999f;
                StartDash(GetDashDirection());
            }
        }

        void FixedUpdate()
        {
            if (rb == null || !rb.simulated) return;

            if (IsDashing)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = DashDirection * CurrentDashSpeed;
                if (Time.time >= dashEndTime) EndDash();
                return;
            }

            // Recarga al tocar el suelo (después de que el dash terminó).
            if (movement != null && movement.IsGrounded && Charges < maxCharges && Time.time - lastDashTime > 0.02f)
            {
                RechargeDash();
            }
        }

        Vector2 GetDashDirection()
        {
            Vector2 m = input != null ? input.Move : Vector2.zero;
            if (m.magnitude < inputDeadzone)
            {
                int facing = movement != null ? movement.FacingDirection : 1;
                return new Vector2(facing, 0f);
            }
            float step = Mathf.PI * 0.25f;
            float angle = Mathf.Round(Mathf.Atan2(m.y, m.x) / step) * step;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        void StartDash(Vector2 direction)
        {
            Charges--;
            IsDashing = true;
            redirectedThisDash = false;
            DashDirection = direction.normalized;
            CurrentDashSpeed = dashSpeed;
            dashEndTime = Time.time + dashDuration;
            lastDashTime = Time.time;

            rb.gravityScale = 0f;
            rb.linearVelocity = DashDirection * CurrentDashSpeed;

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
            RefreshTint();
        }

        void EndDash()
        {
            IsDashing = false;
            if (trail != null) trail.emitting = false;

            if (redirectedThisDash)
            {
                // Pasó por un portal: conserva toda la inercia.
                rb.linearVelocity = DashDirection * CurrentDashSpeed;
                if (movement != null) movement.LockHorizontalControl(redirectedEndBrakeLock);
            }
            else
            {
                rb.linearVelocity = DashDirection * CurrentDashSpeed * endSpeedMultiplier;
            }

            if (movement != null) rb.gravityScale = movement.baseGravityScale;
        }

        /// <summary>
        /// Rota el dash activo hacia <paramref name="newDirection"/> sin perder velocidad ni reiniciar la duración.
        /// Si no hay dash activo no hace nada.
        /// </summary>
        public void RedirectDash(Vector2 newDirection)
        {
            if (!IsDashing || newDirection.sqrMagnitude < 0.0001f) return;
            DashDirection = newDirection.normalized;
            redirectedThisDash = true;
            rb.linearVelocity = DashDirection * CurrentDashSpeed;
        }

        /// <summary>Restaura las cargas de dash (suelo, portal, respawn).</summary>
        public void RechargeDash()
        {
            Charges = maxCharges;
            RefreshTint();
        }

        /// <summary>Cancela el dash inmediatamente (muerte / respawn).</summary>
        public void CancelDash()
        {
            IsDashing = false;
            redirectedThisDash = false;
            lastDashPressedTime = -999f;
            if (trail != null)
            {
                trail.emitting = false;
                trail.Clear();
            }
            if (movement != null && rb != null) rb.gravityScale = movement.baseGravityScale;
        }

        void RefreshTint()
        {
            if (spriteRenderer == null) return;
            spriteRenderer.color = Charges > 0 ? normalTint : noChargeTint;
        }
    }
}
