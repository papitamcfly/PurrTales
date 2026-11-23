using System;
using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Gestiona el par de portales (Azul / Naranja): colocación sobre PortalSurface,
    /// teletransporte con conservación de inercia y limpieza al morir.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalManager : MonoBehaviour
    {
        public static PortalManager Instance { get; private set; }

        [Header("Prefabs")]
        public Portal bluePortalPrefab;
        public Portal orangePortalPrefab;

        [Header("Colocación")]
        [Tooltip("Capas sobre las que se puede abrir un portal.")]
        public LayerMask portalSurfaceMask;
        [Tooltip("Capas sólidas que se sondean para validar que el portal cabe en la superficie.")]
        public LayerMask surfaceProbeMask;
        public float portalWidth = 1.6f;
        [Tooltip("Distancia máxima que se desliza el portal a lo largo de la superficie para que quepa.")]
        public float maxSlide = 0.8f;
        public float slideStep = 0.1f;

        [Header("Teletransporte")]
        [Tooltip("Velocidad mínima de salida.")]
        public float minExitSpeed = 12f;
        [Tooltip("Cooldown anti-bucle por viajero.")]
        public float teleportCooldown = 0.15f;
        [Tooltip("Micro-bloqueo del frenado horizontal al salir lanzado.")]
        public float exitBrakeLockTime = 0.15f;
        [Tooltip("Separación extra entre la superficie del portal de salida y el viajero.")]
        public float exitClearance = 0.15f;
        public bool rechargeDashOnTeleport = true;

        public Portal BluePortal { get; private set; }
        public Portal OrangePortal { get; private set; }
        public bool BothActive { get { return BluePortal != null && OrangePortal != null && BluePortal.IsActive && OrangePortal.IsActive; } }

        public event Action<Portal> PortalPlaced;
        public event Action PortalsCleared;
        public event Action<Portal, Portal, Rigidbody2D> Teleported;

        const float ProbeHeight = 0.2f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PurrTales] Hay más de un PortalManager en la escena; se usa el primero.");
                enabled = false;
                return;
            }
            Instance = this;
            BluePortal = SpawnPortal(bluePortalPrefab, PortalColor.Blue);
            OrangePortal = SpawnPortal(orangePortalPrefab, PortalColor.Orange);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        Portal SpawnPortal(Portal prefab, PortalColor color)
        {
            Portal p;
            if (prefab != null)
            {
                p = Instantiate(prefab, transform);
            }
            else
            {
                // Fallback mínimo si no se asignó prefab.
                var go = new GameObject(color == PortalColor.Blue ? "PortalAzul (runtime)" : "PortalNaranja (runtime)");
                go.transform.SetParent(transform);
                go.layer = 2; // Ignore Raycast
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(portalWidth * 0.9f, 0.6f);
                box.offset = new Vector2(0f, 0.3f);
                p = go.AddComponent<Portal>();
            }
            p.name = color == PortalColor.Blue ? "PortalAzul" : "PortalNaranja";
            p.portalColor = color;
            p.Manager = this;
            p.gameObject.SetActive(false);
            return p;
        }

        public Portal GetPortal(PortalColor color)
        {
            return color == PortalColor.Blue ? BluePortal : OrangePortal;
        }

        public Portal GetLinked(Portal portal)
        {
            return portal == BluePortal ? OrangePortal : BluePortal;
        }

        public bool IsPortalSurface(Collider2D col)
        {
            return col != null && (portalSurfaceMask.value & (1 << col.gameObject.layer)) != 0;
        }

        /// <summary>
        /// Intenta colocar el portal del color indicado en el impacto de un Raycast2D.
        /// Solo funciona sobre PortalSurface; la salida se orienta según hit.normal.
        /// </summary>
        public bool TryPlacePortal(PortalColor color, RaycastHit2D hit)
        {
            if (hit.collider == null || !IsPortalSurface(hit.collider)) return false;
            return TryPlacePortal(color, hit.point, hit.normal);
        }

        public bool TryPlacePortal(PortalColor color, Vector2 point, Vector2 normal)
        {
            Portal portal = GetPortal(color);
            if (portal == null || normal.sqrMagnitude < 0.0001f) return false;

            normal = normal.normalized;
            Vector2 tangent = new Vector2(normal.y, -normal.x);
            Portal other = GetLinked(portal);

            int steps = Mathf.Max(0, Mathf.CeilToInt(maxSlide / Mathf.Max(0.01f, slideStep)));
            for (int i = 0; i <= steps; i++)
            {
                for (int s = 0; s < (i == 0 ? 1 : 2); s++)
                {
                    float offset = (s == 0 ? 1f : -1f) * i * slideStep;
                    Vector2 center = point + tangent * offset;
                    if (FitsAt(center, normal, tangent, other))
                    {
                        portal.Place(center, normal);
                        if (PortalPlaced != null) PortalPlaced(portal);
                        return true;
                    }
                }
            }
            return false;
        }

        bool FitsAt(Vector2 center, Vector2 normal, Vector2 tangent, Portal other)
        {
            float half = portalWidth * 0.5f * 0.95f;
            if (!SurfaceUnder(center, normal)) return false;
            if (!SurfaceUnder(center + tangent * half, normal)) return false;
            if (!SurfaceUnder(center - tangent * half, normal)) return false;

            if (other != null && other.IsActive && Vector2.Dot(other.ExitDirection, normal) > 0.9f)
            {
                Vector2 d = other.SurfacePoint - center;
                if (Mathf.Abs(Vector2.Dot(d, tangent)) < portalWidth && Mathf.Abs(Vector2.Dot(d, normal)) < 0.1f)
                    return false; // se solaparía con el otro portal
            }
            return true;
        }

        bool SurfaceUnder(Vector2 point, Vector2 normal)
        {
            Vector2 start = point + normal * ProbeHeight;
            RaycastHit2D h = Physics2D.Raycast(start, -normal, ProbeHeight + 0.1f, surfaceProbeMask);
            if (h.collider == null || !IsPortalSurface(h.collider)) return false;
            return Mathf.Abs(h.distance - ProbeHeight) < 0.06f && Vector2.Dot(h.normal, normal) > 0.95f;
        }

        /// <summary>Desactiva ambos portales.</summary>
        public void ClearAllPortals()
        {
            if (BluePortal != null) BluePortal.Deactivate();
            if (OrangePortal != null) OrangePortal.Deactivate();
            if (PortalsCleared != null) PortalsCleared();
        }

        /// <summary>
        /// Llamado por Portal al detectar un collider en su trigger.
        /// Conserva la magnitud de la velocidad (mínimo <see cref="minExitSpeed"/>) y la proyecta
        /// en la ExitDirection del portal de salida. Si el viajero está en dash, redirige el dash.
        /// </summary>
        public void TryTeleport(Portal entry, Collider2D other)
        {
            if (!BothActive || entry == null || other == null) return;

            Rigidbody2D body = other.attachedRigidbody;
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic || !body.simulated) return;

            PortalTraveler traveler = body.GetComponent<PortalTraveler>();
            if (traveler == null || !traveler.CanTeleport) return;

            Vector2 entryVelocity = traveler.GetEntryVelocity();
            // Si se está alejando claramente del portal (p. ej. recién salido de él), ignorar.
            if (Vector2.Dot(entryVelocity, entry.ExitDirection) > 0.5f) return;

            Portal exit = GetLinked(entry);
            Vector2 exitDir = exit.ExitDirection;

            PlayerDash dash = body.GetComponent<PlayerDash>();
            PlayerMovement movement = body.GetComponent<PlayerMovement>();
            bool dashing = dash != null && dash.IsDashing;

            // Posición: centrado en el portal de salida, separado de la superficie según el tamaño del viajero.
            Bounds b = other.bounds;
            float extentAlongNormal = Mathf.Abs(exitDir.x) * b.extents.x + Mathf.Abs(exitDir.y) * b.extents.y;
            Vector2 centerOffset = (Vector2)b.center - body.position;
            Vector2 exitCenter = exit.SurfacePoint + exitDir * (extentAlongNormal + exitClearance);
            Vector2 newPos = exitCenter - centerOffset;

            body.position = newPos;
            body.transform.position = new Vector3(newPos.x, newPos.y, body.transform.position.z);

            Vector2 newVelocity;
            if (dashing)
            {
                dash.RedirectDash(exitDir);
                newVelocity = body.linearVelocity;
            }
            else
            {
                float speed = Mathf.Max(entryVelocity.magnitude, minExitSpeed);
                newVelocity = exitDir * speed;
                body.linearVelocity = newVelocity;
            }

            traveler.StartCooldown(teleportCooldown);
            traveler.NotifyTeleported(newVelocity);

            if (movement != null) movement.LockHorizontalControl(exitBrakeLockTime);
            if (dash != null && rechargeDashOnTeleport) dash.RechargeDash();

            TrailRenderer trail = body.GetComponentInChildren<TrailRenderer>();
            if (trail != null) trail.Clear();

            if (Teleported != null) Teleported(entry, exit, body);
        }
    }
}
