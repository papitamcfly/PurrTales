using UnityEngine;

namespace PurrTales
{
    /// <summary>
    /// Reproduce un VFX de un solo ciclo y destruye automáticamente el GameObject al finalizar el clip.
    /// Opcionalmente sigue a un Transform objetivo (por ejemplo, recarga de dash sobre el jugador).
    /// </summary>
    public class AutoDestroyVFX : MonoBehaviour
    {
        [Tooltip("Duración de respaldo en segundos si no se detecta un AnimationClip en el Animator.")]
        public float fallbackDuration = 0.35f;

        [Tooltip("Transform opcional al que seguirá el VFX durante su reproducción.")]
        public Transform followTarget;

        [Tooltip("Desplazamiento respecto al followTarget.")]
        public Vector3 followOffset;

        float destroyAt;

        void Awake()
        {
            float duration = fallbackDuration;
            Animator anim = GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0 && clips[0] != null && clips[0].length > 0f)
                {
                    duration = clips[0].length;
                }
            }
            destroyAt = Time.time + duration;
        }

        void LateUpdate()
        {
            if (followTarget != null)
            {
                transform.position = followTarget.position + followOffset;
            }

            if (Time.time >= destroyAt)
            {
                Destroy(gameObject);
            }
        }
    }
}
