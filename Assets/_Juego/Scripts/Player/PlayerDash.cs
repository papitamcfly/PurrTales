using System.Collections;
using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Dash en 8 direcciones estilo Celeste.
    ///  - Shift: dash hacia el input (8 direcciones) o hacia donde mira el jugador si no hay input.
    ///  - Gravedad anulada y velocidad constante durante <see cref="dashDuration"/> (0.16 s).
    ///  - 1 carga; se recarga al tocar suelo o al cruzar un portal (<see cref="RechargeDash"/>).
    ///  - <see cref="RedirectDash"/> rota el dash al atravesar un portal sin perder velocidad ni reiniciar la duración.
    ///  - Soporta Wavedash e Hyperdash con jump-cancel en suelo en dash diagonal hacia abajo.
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

        [Header("Wavedash / Hyper")]
        [Tooltip("Velocidad horizontal al ejecutar Wavedash / Hyperdash (conservada por overspeedAirDeceleration).")]
        public float wavedashHorizontalSpeed = 25f;
        [Tooltip("Multiplicador aplicado a JumpVelocity de PlayerMovement para la altura vertical del salto.")]
        [Range(0.1f, 1.5f)] public float wavedashJumpScale = 0.85f;
        [Tooltip("Multiplicador vertical para Wall Bounce a lo largo de una pared.")]
        [Range(0.5f, 2f)] public float wallBounceVerticalScale = 1.2f;
        [Tooltip("Multiplicador horizontal para Wall Wavedash alejándose de una pared.")]
        [Range(0.5f, 2f)] public float wallWavedashHorizontalScale = 1.0f;
        [Tooltip("Tiempo mínimo en el aire durante el dash antes de tocar superficie para recargar dash.")]
        public float wavedashMinAirTime = 0.05f;
        [Tooltip("Duración que permanece activo el TrailRenderer tras el salto de Wavedash como feedback visual.")]
        public float wavedashTrailDuration = 0.2f;

        [Header("Feedback")]
        public Color normalTint = Color.white;
        public Color noChargeTint = new Color(0.55f, 0.75f, 1f, 1f);

        public bool IsDashing { get; private set; }
        public Vector2 DashDirection { get; private set; }
        public int Charges { get; private set; }
        public float CurrentDashSpeed { get; private set; }

        /// <summary>Indica si el dash actual es en dirección diagonal hacia abajo.</summary>
        public bool IsDownDiagonalDash
        {
            get { return IsDashing && DashDirection.y < -0.1f && Mathf.Abs(DashDirection.x) > 0.1f; }
        }

        /// <summary>Tiempo transcurrido en el aire durante este dash antes de tocar suelo.</summary>
        public float CurrentDashAirTime
        {
            get
            {
                if (!dashStartedInAir) return 0f;
                if (dashTouchedGround) return dashAirDuration;
                return Time.time - dashStartTime;
            }
        }

        /// <summary>
        /// Cumple condición de recarga (Wavedash): se inició en el aire y voló al menos wavedashMinAirTime antes de tocar tierra.
        /// </summary>
        public bool CanWavedashRecharge
        {
            get { return dashStartedInAir && (CurrentDashAirTime >= wavedashMinAirTime); }
        }

        float dashEndTime;
        float lastDashTime = -999f;
        float lastDashPressedTime = -999f;
        bool redirectedThisDash;

        float dashStartTime;
        bool dashStartedInAir;
        bool dashTouchedGround;
        float dashAirDuration;
        bool requireAirBeforeRecharge;
        bool preservedMomentum;
        Coroutine trailStopCoroutine;

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
                // Seguimiento del tiempo en el aire antes del primer contacto con el suelo
                if (dashStartedInAir && !dashTouchedGround)
                {
                    bool onGround = movement != null && (movement.IsGrounded || movement.CheckGrounded());
                    if (onGround)
                    {
                        dashTouchedGround = true;
                        dashAirDuration = Time.time - dashStartTime;
                    }
                    else
                    {
                        dashAirDuration = Time.time - dashStartTime;
                    }
                }

                rb.gravityScale = 0f;
                rb.linearVelocity = DashDirection * CurrentDashSpeed;
                if (Time.time >= dashEndTime) EndDash();
                return;
            }

            // Si se ejecutó un Hyperdash, bloquear recarga por suelo hasta que haya despegado claramente al aire
            if (requireAirBeforeRecharge)
            {
                bool clearlyInAir = movement != null && !movement.IsGrounded && !movement.CheckGrounded();
                if (clearlyInAir)
                {
                    requireAirBeforeRecharge = false;
                }
                else
                {
                    return;
                }
            }

            // Recarga al tocar el suelo (después de que el dash terminó y habiendo estado en el aire).
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

            // Conservación de momentum estilo Celeste: si ya teníamos velocidad superior
            // en la misma dirección horizontal (ej. tras un Wavedash previo), no frenamos a dashSpeed.
            Vector2 prevVel = rb != null ? rb.linearVelocity : Vector2.zero;
            float prevHorizontalSpeed = Mathf.Abs(prevVel.x);
            bool sameHorizontalDir = Mathf.Abs(direction.x) > 0.1f && Mathf.Sign(direction.x) == Mathf.Sign(prevVel.x);

            if (sameHorizontalDir && prevHorizontalSpeed > dashSpeed)
            {
                CurrentDashSpeed = prevHorizontalSpeed;
                preservedMomentum = true;
            }
            else
            {
                CurrentDashSpeed = dashSpeed;
                preservedMomentum = false;
            }

            dashEndTime = Time.time + dashDuration;
            lastDashTime = Time.time;
            dashStartTime = Time.time;

            dashStartedInAir = movement != null ? (!movement.IsGrounded && !movement.CheckGrounded()) : true;
            dashTouchedGround = false;
            dashAirDuration = 0f;

            if (trailStopCoroutine != null)
            {
                StopCoroutine(trailStopCoroutine);
                trailStopCoroutine = null;
            }

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
            dashStartedInAir = false;
            dashTouchedGround = false;
            dashAirDuration = 0f;

            if (trailStopCoroutine != null)
            {
                StopCoroutine(trailStopCoroutine);
                trailStopCoroutine = null;
            }
            if (trail != null) trail.emitting = false;

            if (redirectedThisDash)
            {
                // Pasó por un portal: conserva toda la inercia.
                rb.linearVelocity = DashDirection * CurrentDashSpeed;
                if (movement != null) movement.LockHorizontalControl(redirectedEndBrakeLock);
            }
            else if (preservedMomentum || (movement != null && !movement.IsGrounded && CurrentDashSpeed > dashSpeed))
            {
                // Venía con momentum acumulado (ej. Wavedash previo): conserva la inercia horizontal
                // completa para que overspeedAirDeceleration en PlayerMovement la desacelere suavemente.
                float endVx = DashDirection.x * CurrentDashSpeed;
                float endVy = DashDirection.y * CurrentDashSpeed * endSpeedMultiplier;
                rb.linearVelocity = new Vector2(endVx, endVy);
            }
            else
            {
                rb.linearVelocity = DashDirection * CurrentDashSpeed * endSpeedMultiplier;
            }

            if (movement != null) rb.gravityScale = movement.baseGravityScale;
        }

        /// <summary>
        /// Ejecuta el impulso de Wavedash o Hyperdash cancelando el dash activo y lanzando al jugador:
        ///  - Desactiva IsDashing inmediatamente.
        ///  - Asigna velocidad horizontal = dirX * wavedashHorizontalSpeed y vertical = JumpVelocity * wavedashJumpScale.
        ///  - Devuelve rb.gravityScale a baseGravityScale.
        ///  - Mantiene el TrailRenderer emitiendo temporalmente durante wavedashTrailDuration como feedback visual.
        ///  - Registra el salto en PlayerMovement como ascendente (isJumpRising = true).
        ///  - Si <paramref name="isFromAir"/> es true (Wavedash genuino), recarga la carga de dash inmediatamente.
        /// </summary>
        /// <param name="isFromAir">Indica si se cumplen las condiciones para recargar el dash en el aire.</param>
        public void ExecuteWavedash(bool isFromAir)
        {
            if (!IsDashing) return;

            IsDashing = false;
            redirectedThisDash = false;
            lastDashPressedTime = -999f;

            float dirX = Mathf.Sign(DashDirection.x);
            if (Mathf.Abs(DashDirection.x) < 0.01f)
            {
                dirX = movement != null ? movement.FacingDirection : 1f;
            }

            if (movement != null && rb != null)
            {
                rb.gravityScale = movement.baseGravityScale;
            }

            float vx = dirX * wavedashHorizontalSpeed;
            float jumpVel = movement != null ? movement.JumpVelocity : 14f;
            float vy = jumpVel * wavedashJumpScale;

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(vx, vy);
            }

            // Feedback visual: mantiene el trail activo durante el despegue
            if (trail != null)
            {
                trail.emitting = true;
                if (trailStopCoroutine != null) StopCoroutine(trailStopCoroutine);
                trailStopCoroutine = StartCoroutine(StopTrailRoutine(wavedashTrailDuration));
            }

            // Si es Wavedash (iniciado en el aire con tiempo mínimo), recarga inmediatamente el dash
            if (isFromAir)
            {
                RechargeDash();
            }
            else
            {
                // Hyperdash (iniciado en suelo): NO recarga el dash en el aire.
                // Bloquear recarga de suelo hasta que haya despegado claramente y vuelva a tocar tierra.
                Charges = 0;
                requireAirBeforeRecharge = true;
                RefreshTint();
            }

            dashStartedInAir = false;
            dashTouchedGround = false;
            dashAirDuration = 0f;

            // Notificar a PlayerMovement para salto ascendente, orientación y consumo de buffer
            if (movement != null)
            {
                movement.OnWavedashExecuted(dirX);
            }
        }

        /// <summary>
        /// Ejecuta el Wavedash / Hyperdash evaluando automáticamente si cumple la recarga (CanWavedashRecharge).
        /// </summary>
        public void ExecuteWavedash()
        {
            ExecuteWavedash(CanWavedashRecharge);
        }

        /// <summary>
        /// Ejecuta el impulso de Wall Wavedash / Wall Bounce cancelando el dash al contactar una pared:
        ///  - Si el dash iba hacia arriba (DashDirection.y > 0.1f), ejecuta Wall Bounce (gran impulso vertical).
        ///  - Si el dash iba horizontal o hacia abajo, ejecuta Wall Wavedash (impulso horizontal extremo alejándose de la pared).
        ///  - Si isFromAir es true, recarga el dash inmediatamente.
        /// </summary>
        public void ExecuteWallWavedash(int wallSide, bool isFromAir)
        {
            if (!IsDashing) return;

            IsDashing = false;
            redirectedThisDash = false;
            lastDashPressedTime = -999f;

            int away = -wallSide;
            if (away == 0) away = movement != null ? -movement.FacingDirection : -1;

            bool isUpwardDash = DashDirection.y > 0.1f;
            float jumpVel = movement != null ? movement.JumpVelocity : 14f;
            float vx;
            float vy;

            if (isUpwardDash)
            {
                // Wall Bounce: salto vertical masivo a lo largo de la pared
                vx = away * (wavedashHorizontalSpeed * 0.45f);
                vy = jumpVel * wallBounceVerticalScale;
            }
            else
            {
                // Wall Wavedash: rebote horizontal a velocidad de wavedash
                vx = away * (wavedashHorizontalSpeed * wallWavedashHorizontalScale);
                vy = jumpVel * wavedashJumpScale;
            }

            if (movement != null && rb != null)
            {
                rb.gravityScale = movement.baseGravityScale;
            }

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(vx, vy);
            }

            // Feedback visual: estela del trail
            if (trail != null)
            {
                trail.emitting = true;
                if (trailStopCoroutine != null) StopCoroutine(trailStopCoroutine);
                trailStopCoroutine = StartCoroutine(StopTrailRoutine(wavedashTrailDuration));
            }

            // Recarga si inició en el aire y superó el tiempo mínimo
            if (isFromAir)
            {
                RechargeDash();
            }
            else
            {
                Charges = 0;
                requireAirBeforeRecharge = true;
                RefreshTint();
            }

            dashStartedInAir = false;
            dashTouchedGround = false;
            dashAirDuration = 0f;

            if (movement != null)
            {
                movement.OnWallWavedashExecuted(away);
            }
        }

        /// <summary>
        /// Ejecuta Wall Wavedash evaluando automáticamente si cumple la recarga (CanWavedashRecharge).
        /// </summary>
        public void ExecuteWallWavedash(int wallSide)
        {
            ExecuteWallWavedash(wallSide, CanWavedashRecharge);
        }

        IEnumerator StopTrailRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!IsDashing && trail != null)
            {
                trail.emitting = false;
            }
            trailStopCoroutine = null;
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
            dashStartedInAir = false;
            dashTouchedGround = false;
            dashAirDuration = 0f;
            requireAirBeforeRecharge = false;
            preservedMomentum = false;

            if (trailStopCoroutine != null)
            {
                StopCoroutine(trailStopCoroutine);
                trailStopCoroutine = null;
            }
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
