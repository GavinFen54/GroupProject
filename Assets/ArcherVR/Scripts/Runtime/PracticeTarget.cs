using System.Collections;
using UnityEngine;

namespace ArcherVR
{
    /// <summary>
    /// Warm-up target that appears in front of the player once they pick up the bow.
    /// The WaveManager holds the first wave until it has been hit <see cref="hitsRequired"/> times.
    /// Arrows find it through its Health component, like any enemy.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PracticeTarget : MonoBehaviour
    {
        [Tooltip("How many arrows must land before the battle starts.")]
        public int hitsRequired = 1;
        [Tooltip("Gentle up/down drift so it reads as a target, not part of the scenery.")]
        public float bobHeight = 0.08f;
        public AudioClip hitSfx;

        public bool Done { get; private set; }
        public int Hits { get; private set; }

        Health health;
        Vector3 basePos;
        Vector3 baseScale;

        void Awake()
        {
            health = GetComponent<Health>();
            health.maxHealth = 1f;
            health.onDamaged.AddListener(OnHit);
            baseScale = transform.localScale;
            gameObject.SetActive(false);
        }

        /// <summary>Called by the WaveManager when the player has the bow.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            basePos = transform.position;
            // Count hits ourselves; keep health high so every arrow registers.
            health.ResetHealth(1000000f);
            StartCoroutine(PopIn());
        }

        void Update()
        {
            if (!Done) transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 1.6f) * bobHeight;
        }

        void OnHit(float amount)
        {
            Hits++;
            if (hitSfx != null) AudioSource.PlayClipAtPoint(hitSfx, transform.position);
            if (Hits >= hitsRequired && !Done)
            {
                Done = true;
                StartCoroutine(Break());
            }
            else
            {
                StartCoroutine(Wobble());
            }
        }

        IEnumerator PopIn()
        {
            for (float t = 0; t < 1f; t += Time.deltaTime * 3f)
            {
                transform.localScale = baseScale * Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
            transform.localScale = baseScale;
        }

        IEnumerator Wobble()
        {
            for (float t = 0; t < 1f; t += Time.deltaTime * 4f)
            {
                transform.localScale = baseScale * (1f + 0.15f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            transform.localScale = baseScale;
        }

        IEnumerator Break()
        {
            // A satisfying "hit!" pulse, then shrink away.
            for (float t = 0; t < 1f; t += Time.deltaTime * 5f)
            {
                transform.localScale = baseScale * (1f + 0.3f * t);
                yield return null;
            }
            for (float t = 0; t < 1f; t += Time.deltaTime * 2.5f)
            {
                transform.localScale = baseScale * 1.3f * (1f - t);
                transform.Rotate(0f, 0f, 540f * Time.deltaTime, Space.Self);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
