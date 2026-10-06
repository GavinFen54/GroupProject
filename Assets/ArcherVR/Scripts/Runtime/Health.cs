using UnityEngine;
using UnityEngine.Events;

namespace ArcherVR
{
    /// <summary>
    /// Generic hit points for anything arrows can hurt (enemies, bosses, targets).
    /// The tower uses its own TowerHealth so arrows never damage it.
    /// </summary>
    public class Health : MonoBehaviour
    {
        public float maxHealth = 30f;

        [Tooltip("Fired with the damage amount every time this takes a hit.")]
        public UnityEvent<float> onDamaged = new UnityEvent<float>();

        [Tooltip("Fired once when health reaches zero.")]
        public UnityEvent onDied = new UnityEvent();

        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;
        public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

        void Awake()
        {
            CurrentHealth = maxHealth;
        }

        /// <summary>Sets a new max and refills. Used by the wave manager to scale enemies per wave.</summary>
        public void ResetHealth(float newMax)
        {
            maxHealth = newMax;
            CurrentHealth = newMax;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            onDamaged.Invoke(amount);
            if (IsDead) onDied.Invoke();
        }
    }
}
