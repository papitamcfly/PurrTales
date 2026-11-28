using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Movimiento de precisión estilo Celeste / Super Meat Boy:
    ///  - Aceleración / desaceleración responsiva con conservación de momentum por encima de la velocidad máxima.
    ///  - Salto variable (corte al soltar), gravedad aumentada al caer, Coyote Time y Jump Buffer.
    ///  - Wall Slide y Wall Jump (capa Wall) detectados con Physics2D.OverlapBox.
    ///  - Bloqueo horizontal temporal (wall jump y salida de portales).
    ///  - Wavedash / Hyperdash con jump-cancel al hacer dash diagonal abajo tocando el suelo.
    /// Durante el dash regular (PlayerDash.IsDashing) este script no toca la velocidad ni la gravedad.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Referencias")]
        public Rigidbody2D rb;
        public Collider2D bodyCollider;
        public SpriteRenderer spriteRenderer;
        public Animator animator;
        public PlayerInputReader input;
        public PlayerDash dash;

        [Header("Capas")]
        [Tooltip("Capas sobre las que se puede estar parado (Ground; también Wall y PortalSurface para pisar sus caras superiores).")]
        public LayerMask groundMask;
        [Tooltip("Capas que permiten Wall Slide / Wall Jump.")]
        public LayerMask wallMask;

        [Header("Movimiento horizontal")]
        public float maxRunSpeed = 9f;
        public float groundAcceleration = 95f;
        public float groundDeceleration = 120f;
        public float airAcceleration = 70f;
        public float airDeceleration = 40f;
        [Tooltip("Multiplicador de aceleración al invertir dirección (giro rápido).")]
        public float turnMultiplier = 1.6f;
        [Tooltip("Frenado cuando se va por encima de maxRunSpeed (p. ej. tras un portal). Bajo = conserva inercia.")]
        public float overspeedGroundDeceleration = 45f;
        public float overspeedAirDeceleration = 6f;
        [Range(0f, 0.5f)] public float inputDeadzone = 0.2f;

        [Header("Salto")]
        public float baseGravityScale = 3f;
        public float jumpHeight = 3.2f;
        [Tooltip("Multiplicador de la velocidad vertical al soltar el botón de salto (salto variable).")]
        [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.45f;
        public float fallGravityMultiplier = 1.6f;
        public float maxFallSpeed = 24f;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.12f;

        [Header("Pared")]
        public float wallCheckDistance = 0.08f;
        public float wallSlideMaxSpeed = 3f;
        public float wallSlideDeceleration = 80f;
        public float wallJumpHorizontalSpeed = 10f;
        public float wallJumpVerticalSpeed = 14f;
        [Tooltip("Tiempo sin control horizontal tras un wall jump (impide escalar una sola pared sin esfuerzo).")]
        public float wallJumpControlLock = 0.15f;
        [Tooltip("Margen para hacer wall jump justo después de despegarse de la pared.")]
        public float wallCoyoteTime = 0.08f;

        [Header("Detección de suelo")]
        public float groundCheckDepth = 0.08f;

        // ---- Estado ----
        public bool IsGrounded { get; private set; }
        public bool IsTouchingWall { get; private set; }
        public int WallSide { get; private set; }          // -1 izquierda, 1 derecha, 0 nada
        public bool IsWallSliding { get; private set; }
        public int FacingDirection { get; private set; }   // -1 / 1
        public bool IsHorizontalLocked { get { return Time.time < horizontalLockUntil; } }
        public bool IsJumpRising { get { return isJumpRising; } set { isJumpRising = value; } }
        public bool IsJumpBuffered { get { return Time.time - lastJumpPressedTime <= jumpBufferTime; } }

        public float wavedashHorizontalSpeed { get { return dash != null ? dash.wavedashHorizontalSpeed : 25f; } }
        public float wavedashJumpScale { get { return dash != null ? dash.wavedashJumpScale : 0.85f; } }
        public float wavedashMinAirTime { get { return dash != null ? dash.wavedashMinAirTime : 0.05f; } }

        float lastGroundedTime = -999f;
        float lastJumpPressedTime = -999f;
        float lastWallTime = -999f;
        int lastWallSide;
        float horizontalLockUntil = -999f;
        bool isJumpRising;
        bool pendingJumpCut;

        static readonly int AnimSpeedHash = Animator.StringToHash("Speed");
        static readonly int AnimIsGroundedHash = Animator.StringToHash("IsGrounded");
        static readonly int AnimIsDashingHash = Animator.StringToHash("IsDashing");
        static readonly int AnimVerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
        static readonly int AnimIsWallSlidingHash = Animator.StringToHash("IsWallSliding");

        public float JumpVelocity
        {
            get { return Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * baseGravityScale * jumpHeight); }
        }

        void Reset()
        {
            rb = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            animator = GetComponentInChildren<Animator>();
            input = GetComponent<PlayerInputReader>();
            dash = GetComponent<PlayerDash>();
        }

        void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (dash == null) dash = GetComponent<PlayerDash>();
            FacingDirection = 1;
            rb.gravityScale = baseGravityScale;
        }

        void Update()
        {
            if (input == null)
            {
                UpdateAnimator();
                return;
            }

            if (input.JumpPressed) lastJumpPressedTime = Time.time;
            if (input.JumpReleased) pendingJumpCut = true;

            bool dashing = dash != null && dash.IsDashing;
            if (dashing)
            {
                float dx = dash.DashDirection.x;
                if (Mathf.Abs(dx) > 0.01f) FacingDirection = dx > 0f ? 1 : -1;
            }
            else if (!IsHorizontalLocked)
            {
                float x = input.Move.x;
                if (Mathf.Abs(x) > inputDeadzone) FacingDirection = x > 0f ? 1 : -1;
            }

            if (spriteRenderer != null)
            {
                if (IsWallSliding && WallSide != 0)
                {
                    // Pared a la derecha (WallSide > 0): flipX = false (patas en columna 69 hacia la pared).
                    // Pared a la izquierda (WallSide < 0): flipX = true.
                    spriteRenderer.flipX = WallSide < 0;
                }
                else
                {
                    spriteRenderer.flipX = FacingDirection < 0;
                }
            }
            UpdateAnimator();
        }

        void UpdateAnimator()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            float speed = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
            float verticalVelocity = rb != null ? rb.linearVelocity.y : 0f;
            bool dashing = dash != null && dash.IsDashing;
            animator.SetFloat(AnimSpeedHash, speed);
            animator.SetFloat(AnimVerticalVelocityHash, verticalVelocity);
            animator.SetBool(AnimIsGroundedHash, IsGrounded);
            animator.SetBool(AnimIsDashingHash, dashing);
            animator.SetBool(AnimIsWallSlidingHash, IsWallSliding);
        }

        void FixedUpdate()
        {
            if (rb == null || !rb.simulated) return;

            float now = Time.time;
            UpdateCollisionChecks();

            if (IsGrounded)
            {
                lastGroundedTime = now;
                isJumpRising = false;
            }
            if (IsTouchingWall && !IsGrounded)
            {
                lastWallTime = now;
                lastWallSide = WallSide;
            }

            // Durante el dash, comprobamos si cumple Jump-Cancel para Wavedash, Hyperdash o Wall Wavedash.
            if (dash != null && dash.IsDashing)
            {
                bool jumpRequested = (now - lastJumpPressedTime <= jumpBufferTime) || (input != null && input.JumpPressed);

                if (jumpRequested)
                {
                    // 1. Suelo: Wavedash (iniciado en aire) o Hyperdash / Superdash (iniciado en suelo)
                    bool touchesGround = IsGrounded || CheckGrounded() || (now - lastGroundedTime <= coyoteTime);
                    bool validGroundDir = dash.DashDirection.y < -0.1f || (touchesGround && Mathf.Abs(dash.DashDirection.x) > 0.1f);

                    if (touchesGround && validGroundDir)
                    {
                        dash.ExecuteWavedash();
                        return;
                    }

                    // 2. Pared: Wall Wavedash / Wall Bounce
                    int wallSide = WallSide;
                    bool touchesWall = (IsTouchingWall && !IsGrounded) || CheckTouchingWall(out wallSide) || (now - lastWallTime <= wallCoyoteTime && lastWallSide != 0);
                    if (wallSide == 0 && lastWallSide != 0) wallSide = lastWallSide;

                    if (touchesWall && wallSide != 0)
                    {
                        dash.ExecuteWallWavedash(wallSide);
                        return;
                    }
                }

                IsWallSliding = false;
                pendingJumpCut = false;
                return;
            }

            Vector2 v = rb.linearVelocity;
            float inputX = input != null ? input.Move.x : 0f;
            if (Mathf.Abs(inputX) < inputDeadzone) inputX = 0f;

            // ---------- Salto / Wall Jump (con buffer y coyote) ----------
            bool jumpBuffered = now - lastJumpPressedTime <= jumpBufferTime;
            if (jumpBuffered)
            {
                if (now - lastGroundedTime <= coyoteTime)
                {
                    DoJump(ref v);
                }
                else if (now - lastWallTime <= wallCoyoteTime && lastWallSide != 0)
                {
                    DoWallJump(ref v);
                }
            }

            // ---------- Salto variable ----------
            if (pendingJumpCut)
            {
                if (isJumpRising && v.y > 0f) v.y *= jumpCutMultiplier;
                isJumpRising = false;
                pendingJumpCut = false;
            }
            if (v.y <= 0f) isJumpRising = false;

            // ---------- Horizontal ----------
            if (now >= horizontalLockUntil)
            {
                float target = inputX * maxRunSpeed;
                bool hasInput = inputX != 0f;
                bool overspeed = Mathf.Abs(v.x) > maxRunSpeed;
                bool sameDir = hasInput && Mathf.Sign(inputX) == Mathf.Sign(v.x);
                float accel;

                if (overspeed && (sameDir || !hasInput))
                {
                    // Conserva la inercia (portales / dash): solo frena suavemente hasta la velocidad máxima.
                    accel = IsGrounded ? overspeedGroundDeceleration : overspeedAirDeceleration;
                    target = Mathf.Sign(v.x) * maxRunSpeed;
                    if (!hasInput && IsGrounded) target = 0f;
                }
                else if (hasInput)
                {
                    accel = IsGrounded ? groundAcceleration : airAcceleration;
                    if (Mathf.Abs(v.x) > 0.01f && Mathf.Sign(target) != Mathf.Sign(v.x)) accel *= turnMultiplier;
                }
                else
                {
                    accel = IsGrounded ? groundDeceleration : airDeceleration;
                }

                v.x = Mathf.MoveTowards(v.x, target, accel * Time.fixedDeltaTime);
            }

            // ---------- Wall Slide ----------
            bool pushingIntoWall = IsTouchingWall && inputX != 0f && Mathf.Sign(inputX) == WallSide;
            IsWallSliding = !IsGrounded && pushingIntoWall && v.y < 0f;

            // ---------- Gravedad ----------
            float g = baseGravityScale;
            if (v.y < 0f) g *= fallGravityMultiplier;
            rb.gravityScale = g;

            if (IsWallSliding)
            {
                if (v.y < -wallSlideMaxSpeed)
                    v.y = Mathf.MoveTowards(v.y, -wallSlideMaxSpeed, wallSlideDeceleration * Time.fixedDeltaTime);
            }
            else if (v.y < -maxFallSpeed)
            {
                v.y = -maxFallSpeed;
            }

            rb.linearVelocity = v;
        }

        void DoJump(ref Vector2 v)
        {
            v.y = JumpVelocity;
            isJumpRising = true;
            ConsumeJump();
            lastGroundedTime = -999f;
        }

        void DoWallJump(ref Vector2 v)
        {
            int away = -lastWallSide;
            v.x = away * wallJumpHorizontalSpeed;
            v.y = wallJumpVerticalSpeed;
            FacingDirection = away;
            isJumpRising = true;
            ConsumeJump();
            lastWallTime = -999f;
            LockHorizontalControl(wallJumpControlLock);
        }

        public void ConsumeJump()
        {
            lastJumpPressedTime = -999f;
            pendingJumpCut = false;
            // Si el botón ya se soltó antes de ejecutar el salto bufferizado, aplicar corte en el siguiente paso.
            if (input != null && !input.JumpHeld) pendingJumpCut = true;
        }

        /// <summary>Configura salto ascendente, orientación y limpia el buffer tras un Wavedash / Hyperdash.</summary>
        public void OnWavedashExecuted(float dirX)
        {
            FacingDirection = (int)Mathf.Sign(dirX);
            if (spriteRenderer != null) spriteRenderer.flipX = FacingDirection < 0;

            isJumpRising = true;
            lastGroundedTime = -999f;
            ConsumeJump();
        }

        /// <summary>Configura salto ascendente, orientación y bloqueo temporal tras un Wall Wavedash / Wall Bounce.</summary>
        public void OnWallWavedashExecuted(int away)
        {
            FacingDirection = away;
            if (spriteRenderer != null) spriteRenderer.flipX = FacingDirection < 0;

            isJumpRising = true;
            lastWallTime = -999f;
            lastGroundedTime = -999f;
            ConsumeJump();
            LockHorizontalControl(wallJumpControlLock);
        }

        /// <summary>Comprueba directamente si el collider del jugador está en contacto con una pared dentro de wallCheckDistance.</summary>
        public bool CheckTouchingWall(out int wallSide)
        {
            wallSide = 0;
            if (bodyCollider == null) return false;
            Bounds b = bodyCollider.bounds;
            Vector2 wallSize = new Vector2(wallCheckDistance, b.size.y * 0.7f);
            Vector2 rightCenter = new Vector2(b.max.x + wallCheckDistance * 0.5f, b.center.y);
            Vector2 leftCenter = new Vector2(b.min.x - wallCheckDistance * 0.5f, b.center.y);
            bool right = Physics2D.OverlapBox(rightCenter, wallSize, 0f, wallMask) != null;
            bool left = Physics2D.OverlapBox(leftCenter, wallSize, 0f, wallMask) != null;

            if (right && left) wallSide = FacingDirection;
            else if (right) wallSide = 1;
            else if (left) wallSide = -1;

            return right || left;
        }

        /// <summary>Comprueba si el collider del jugador está en contacto con el suelo dentro de groundCheckDepth.</summary>
        public bool CheckGrounded()
        {
            if (bodyCollider == null) return false;
            Bounds b = bodyCollider.bounds;
            Vector2 groundCenter = new Vector2(b.center.x, b.min.y - groundCheckDepth * 0.5f);
            Vector2 groundSize = new Vector2(b.size.x * 0.9f, groundCheckDepth);
            return Physics2D.OverlapBox(groundCenter, groundSize, 0f, groundMask) != null;
        }

        /// <summary>Ejecuta Wavedash / Hyperdash delegando en PlayerDash.</summary>
        public void ExecuteWavedash(bool isFromAir)
        {
            if (dash != null) dash.ExecuteWavedash(isFromAir);
        }

        public void ExecuteWavedash()
        {
            if (dash != null) dash.ExecuteWavedash();
        }

        public void ExecuteWallWavedash(int wallSide, bool isFromAir)
        {
            if (dash != null) dash.ExecuteWallWavedash(wallSide, isFromAir);
        }

        public void ExecuteWallWavedash(int wallSide)
        {
            if (dash != null) dash.ExecuteWallWavedash(wallSide);
        }

        void UpdateCollisionChecks()
        {
            if (bodyCollider == null)
            {
                IsGrounded = false;
                IsTouchingWall = false;
                WallSide = 0;
                return;
            }

            Bounds b = bodyCollider.bounds;

            Vector2 groundCenter = new Vector2(b.center.x, b.min.y - groundCheckDepth * 0.5f);
            Vector2 groundSize = new Vector2(b.size.x * 0.9f, groundCheckDepth);
            bool groundHit = Physics2D.OverlapBox(groundCenter, groundSize, 0f, groundMask) != null;
            IsGrounded = groundHit && rb.linearVelocity.y <= 0.1f;

            Vector2 wallSize = new Vector2(wallCheckDistance, b.size.y * 0.7f);
            Vector2 rightCenter = new Vector2(b.max.x + wallCheckDistance * 0.5f, b.center.y);
            Vector2 leftCenter = new Vector2(b.min.x - wallCheckDistance * 0.5f, b.center.y);
            bool right = Physics2D.OverlapBox(rightCenter, wallSize, 0f, wallMask) != null;
            bool left = Physics2D.OverlapBox(leftCenter, wallSize, 0f, wallMask) != null;

            IsTouchingWall = right || left;
            if (right && left) WallSide = FacingDirection;
            else if (right) WallSide = 1;
            else if (left) WallSide = -1;
            else WallSide = 0;
        }

        /// <summary>Bloquea aceleración/frenado horizontal durante <paramref name="duration"/> segundos.</summary>
        public void LockHorizontalControl(float duration)
        {
            horizontalLockUntil = Mathf.Max(horizontalLockUntil, Time.time + duration);
        }

        public void ClearHorizontalLock()
        {
            horizontalLockUntil = -999f;
        }

        /// <summary>Reinicia timers y estado (respawn).</summary>
        public void ResetState()
        {
            lastGroundedTime = -999f;
            lastJumpPressedTime = -999f;
            lastWallTime = -999f;
            horizontalLockUntil = -999f;
            isJumpRising = false;
            pendingJumpCut = false;
            IsWallSliding = false;
            FacingDirection = 1;
            if (spriteRenderer != null) spriteRenderer.flipX = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = baseGravityScale;
            }
            UpdateAnimator();
        }

        void OnDrawGizmosSelected()
        {
            Collider2D c = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
            if (c == null) return;
            Bounds b = c.bounds;
            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y - groundCheckDepth * 0.5f),
                new Vector3(b.size.x * 0.9f, groundCheckDepth));
            Gizmos.color = IsTouchingWall ? Color.cyan : Color.gray;
            Vector3 ws = new Vector3(wallCheckDistance, b.size.y * 0.7f);
            Gizmos.DrawWireCube(new Vector3(b.max.x + wallCheckDistance * 0.5f, b.center.y), ws);
            Gizmos.DrawWireCube(new Vector3(b.min.x - wallCheckDistance * 0.5f, b.center.y), ws);
        }
    }
}
