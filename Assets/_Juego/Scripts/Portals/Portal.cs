using UnityEngine;

namespace PurrTales
{
    public enum PortalColor
    {
        Blue,
        Orange
    }

    /// <summary>
    /// Un portal colocado sobre una superficie PortalSurface.
    /// Convención local: eje X = a lo largo de la superficie, eje Y (transform.up) = normal de salida.
    /// El trigger sobresale de la superficie para detectar al viajero antes del contacto sólido.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class Portal : MonoBehaviour
    {
        public PortalColor portalColor = PortalColor.Blue;
        [Tooltip("Ancho del portal a lo largo de la superficie (debe coincidir con el visual / trigger).")]
        public float width = 1.6f;
        public SpriteRenderer visual;
        public Animator visualAnimator;
        public ParticleSystem particles;

        static readonly int AnimIsWallHash = Animator.StringToHash("IsWall");

        /// <summary>Dirección de salida (normal de la superficie donde se colocó).</summary>
        public Vector2 ExitDirection { get; private set; }
        /// <summary>Punto exacto sobre la superficie.</summary>
        public Vector2 SurfacePoint { get; private set; }
        public PortalManager Manager { get; set; }
        public bool IsActive { get { return gameObject.activeSelf; } }

        void Awake()
        {
            if (ExitDirection == Vector2.zero) ExitDirection = transform.up;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            if (visualAnimator == null)
            {
                visualAnimator = visual != null ? visual.GetComponent<Animator>() : GetComponentInChildren<Animator>();
            }
        }

        /// <summary>Coloca y activa el portal en <paramref name="point"/> mirando hacia <paramref name="normal"/>.</summary>
        public void Place(Vector2 point, Vector2 normal)
        {
            ExitDirection = normal.normalized;
            SurfacePoint = point;
            float angle = Mathf.Atan2(ExitDirection.y, ExitDirection.x) * Mathf.Rad2Deg - 90f;
            transform.SetPositionAndRotation(new Vector3(point.x, point.y, 0f), Quaternion.Euler(0f, 0f, angle));
            gameObject.SetActive(true);

            bool isWall = Mathf.Abs(ExitDirection.x) > Mathf.Abs(ExitDirection.y);
            if (visual != null)
            {
                visual.transform.localRotation = isWall ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            }
            if (visualAnimator == null && visual != null)
            {
                visualAnimator = visual.GetComponent<Animator>();
            }
            if (visualAnimator != null && visualAnimator.runtimeAnimatorController != null)
            {
                visualAnimator.SetBool(AnimIsWallHash, isWall);
            }

            if (particles != null)
            {
                particles.Clear();
                particles.Play();
            }
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (Manager != null) Manager.TryTeleport(this, other);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (Manager != null) Manager.TryTeleport(this, other);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = portalColor == PortalColor.Blue ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.55f, 0.1f);
            Vector3 p = transform.position;
            Vector3 dir = Application.isPlaying ? (Vector3)ExitDirection : transform.up;
            Gizmos.DrawLine(p, p + dir * 1.2f);
        }
    }
}
