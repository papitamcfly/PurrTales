using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Marca un Rigidbody2D como capaz de cruzar portales.
    /// Guarda la velocidad previa al paso de físicas: si el viajero choca con la superficie del portal
    /// en el mismo paso en que entra al trigger, la colisión ya habría anulado la velocidad;
    /// así el PortalManager puede conservar la inercia real.
    /// Se ejecuta tarde (orden 1000) para leer la velocidad que ya fijaron movimiento y dash.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PortalTraveler : MonoBehaviour
    {
        public Rigidbody2D rb;

        public Vector2 PreStepVelocity { get; private set; }
        public float CooldownUntil { get; private set; }
        public bool CanTeleport { get { return Time.time >= CooldownUntil; } }

        void Reset()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
        }

        void FixedUpdate()
        {
            if (rb != null) PreStepVelocity = rb.linearVelocity;
        }

        /// <summary>Devuelve la velocidad de mayor magnitud entre la actual y la previa al paso.</summary>
        public Vector2 GetEntryVelocity()
        {
            Vector2 current = rb != null ? rb.linearVelocity : Vector2.zero;
            return PreStepVelocity.sqrMagnitude > current.sqrMagnitude ? PreStepVelocity : current;
        }

        public void StartCooldown(float duration)
        {
            CooldownUntil = Time.time + duration;
        }

        public void NotifyTeleported(Vector2 newVelocity)
        {
            PreStepVelocity = newVelocity;
        }
    }
}
