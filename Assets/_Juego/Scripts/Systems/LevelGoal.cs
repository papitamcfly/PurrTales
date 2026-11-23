using System;
using UnityEngine;

namespace PurrTales
{
    /// <summary>Meta del nivel: al tocarla registra el tiempo y avisa al HUD.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelGoal : MonoBehaviour
    {
        public bool Reached { get; private set; }
        public float ReachedAtTime { get; private set; }
        public event Action<LevelGoal> GoalReached;

        void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (Reached) return;
            Rigidbody2D body = other.attachedRigidbody;
            if (body == null || body.GetComponent<PlayerDeathHandler>() == null) return;
            Reached = true;
            ReachedAtTime = Time.timeSinceLevelLoad;
            if (GoalReached != null) GoalReached(this);
        }
    }
}
