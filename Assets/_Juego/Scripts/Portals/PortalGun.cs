using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Pistola de portales: Clic izquierdo = Portal Azul, Clic derecho = Portal Naranja.
    /// Lanza un Raycast2D hacia el cursor (o stick derecho); el portal solo se abre si el primer
    /// impacto es una superficie de la capa PortalSurface, orientando la salida según hit.normal.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalGun : MonoBehaviour
    {
        [Header("Referencias")]
        public PlayerInputReader input;
        public PortalManager portalManager;
        public Camera aimCamera;
        [Tooltip("Origen del disparo. Si es nulo se usa el centro del collider del jugador.")]
        public Transform muzzle;
        public Collider2D ownerCollider;
        public PlayerDeathHandler deathHandler;

        [Header("Disparo")]
        [Tooltip("Capas que detienen el disparo (Ground, Wall, PortalSurface, Hazard).")]
        public LayerMask raycastMask;
        public float maxDistance = 40f;

        [Header("Mira")]
        public LineRenderer aimLine;
        public Color validAimColor = new Color(1f, 1f, 1f, 0.55f);
        public Color invalidAimColor = new Color(1f, 0.3f, 0.3f, 0.25f);
        public Color blueColor = new Color(0.2f, 0.6f, 1f, 0.9f);
        public Color orangeColor = new Color(1f, 0.55f, 0.1f, 0.9f);
        [Tooltip("Duración del destello de color de la mira tras disparar.")]
        public float shotFlashTime = 0.12f;

        public RaycastHit2D LastAimHit { get; private set; }
        public bool AimIsValid { get; private set; }

        float flashUntil;
        Color flashColor;

        void Reset()
        {
            input = GetComponent<PlayerInputReader>();
            ownerCollider = GetComponent<Collider2D>();
            deathHandler = GetComponent<PlayerDeathHandler>();
            aimLine = GetComponentInChildren<LineRenderer>();
        }

        void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (ownerCollider == null) ownerCollider = GetComponent<Collider2D>();
            if (deathHandler == null) deathHandler = GetComponent<PlayerDeathHandler>();
        }

        void Update()
        {
            if (aimCamera == null) aimCamera = Camera.main;
            if (portalManager == null) portalManager = PortalManager.Instance;

            bool dead = deathHandler != null && deathHandler.IsDead;
            if (input == null || dead)
            {
                if (aimLine != null) aimLine.enabled = false;
                return;
            }

            Vector2 origin = GetOrigin();
            Vector2 dir = input.GetAimDirection(origin, aimCamera);
            RaycastHit2D hit = Physics2D.Raycast(origin, dir, maxDistance, raycastMask);
            LastAimHit = hit;
            AimIsValid = hit.collider != null && portalManager != null && portalManager.IsPortalSurface(hit.collider);

            if (input.FireBluePressed) Fire(PortalColor.Blue, hit);
            if (input.FireOrangePressed) Fire(PortalColor.Orange, hit);

            UpdateAimLine(origin, dir, hit);
        }

        Vector2 GetOrigin()
        {
            if (muzzle != null) return muzzle.position;
            if (ownerCollider != null) return ownerCollider.bounds.center;
            return transform.position;
        }

        void Fire(PortalColor color, RaycastHit2D hit)
        {
            if (portalManager == null) return;
            bool placed = AimIsValid && portalManager.TryPlacePortal(color, hit);
            if (placed)
            {
                flashColor = color == PortalColor.Blue ? blueColor : orangeColor;
                flashUntil = Time.time + shotFlashTime;
            }
            else
            {
                flashColor = invalidAimColor;
                flashColor.a = 0.8f;
                flashUntil = Time.time + shotFlashTime * 0.5f;
            }
        }

        void UpdateAimLine(Vector2 origin, Vector2 dir, RaycastHit2D hit)
        {
            if (aimLine == null) return;
            aimLine.enabled = true;
            Vector2 end = hit.collider != null ? hit.point : origin + dir * maxDistance;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, new Vector3(origin.x, origin.y, 0f));
            aimLine.SetPosition(1, new Vector3(end.x, end.y, 0f));

            Color c = Time.time < flashUntil ? flashColor : (AimIsValid ? validAimColor : invalidAimColor);
            Color endColor = c;
            endColor.a *= 0.35f;
            aimLine.startColor = c;
            aimLine.endColor = endColor;
        }
    }
}
