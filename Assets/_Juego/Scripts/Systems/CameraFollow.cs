using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Cámara 2D suave con anticipación en la dirección del movimiento.
    /// Hace "snap" al objetivo si salta más de <see cref="snapDistance"/> (portales lejanos / respawn).
    /// (El paquete Cinemachine no está en el proyecto base; esto lo sustituye para el nivel de prueba.)
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Rigidbody2D targetBody;
        public Vector2 offset = new Vector2(0f, 1.5f);
        public float smoothTime = 0.12f;
        public float lookAhead = 1.5f;
        public float lookAheadSmoothing = 4f;
        public float snapDistance = 14f;

        Vector3 velocity;
        Vector2 currentLookAhead;

        void Start()
        {
            SnapToTarget();
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector2 desiredAhead = Vector2.zero;
            if (targetBody != null)
            {
                Vector2 v = targetBody.linearVelocity;
                desiredAhead = new Vector2(Mathf.Clamp(v.x * 0.15f, -lookAhead, lookAhead), 0f);
            }
            currentLookAhead = Vector2.Lerp(currentLookAhead, desiredAhead, Time.deltaTime * lookAheadSmoothing);

            Vector3 goal = new Vector3(target.position.x + offset.x + currentLookAhead.x,
                                       target.position.y + offset.y,
                                       transform.position.z);

            if ((goal - transform.position).sqrMagnitude > snapDistance * snapDistance)
            {
                transform.position = goal;
                velocity = Vector3.zero;
                return;
            }
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
            velocity = Vector3.zero;
            currentLookAhead = Vector2.zero;
        }
    }
}
