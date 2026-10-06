using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ArcherVR
{
    /// <summary>
    /// The tower the player defends. Instead of only a health bar, damage physically
    /// changes the environment (Figma "unique value"): at each threshold the tower
    /// sinks lower and carries the player closer to the battlefield.
    /// </summary>
    public class TowerHealth : MonoBehaviour
    {
        public float maxHealth = 500f;

        [Header("Collapse")]
        [Tooltip("Everything that sinks when the tower collapses (tower pieces + battle platform).")]
        public Transform towerVisualRoot;
        [Tooltip("The XR Origin. Moved down by the same amount so the player rides the tower.")]
        public Transform playerRig;
        public float[] collapseThresholds = { 0.75f, 0.5f, 0.25f };
        public float collapseStepHeight = 3f;
        public float collapseDuration = 1.5f;
        public GameObject rubblePrefab;

        [Header("Feedback")]
        [Tooltip("Short-lived marker spawned where an enemy hits the tower so the player knows where to look.")]
        public GameObject hitMarkerPrefab;
        public AudioClip hitSfx;
        public AudioClip collapseSfx;

        [Header("Events")]
        public UnityEvent<float> onHealthChanged = new UnityEvent<float>();
        public UnityEvent onCollapseStep = new UnityEvent();
        public UnityEvent onDestroyed = new UnityEvent();

        float current;
        int stepsDone;
        float lastMarkerTime;

        public float CurrentHealth => current;
        public float Normalized => maxHealth > 0f ? current / maxHealth : 0f;
        public bool IsDestroyed => current <= 0f;

        void Awake()
        {
            current = maxHealth;
            if (towerVisualRoot == null) towerVisualRoot = transform;
        }

        public void TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDestroyed || amount <= 0f) return;
            current = Mathf.Max(0f, current - amount);
            onHealthChanged.Invoke(Normalized);

            if (hitSfx != null) AudioSource.PlayClipAtPoint(hitSfx, hitPoint, 0.8f);
            if (hitMarkerPrefab != null && Time.time - lastMarkerTime > 0.15f)
            {
                lastMarkerTime = Time.time;
                var m = Instantiate(hitMarkerPrefab, hitPoint, Quaternion.identity);
                Destroy(m, 0.6f);
            }

            while (stepsDone < collapseThresholds.Length && Normalized <= collapseThresholds[stepsDone])
            {
                stepsDone++;
                StartCoroutine(CollapseStep());
            }

            if (IsDestroyed)
            {
                onDestroyed.Invoke();
                if (GameFlow.Instance != null) GameFlow.Instance.OnTowerDestroyed();
            }
        }

        IEnumerator CollapseStep()
        {
            onCollapseStep.Invoke();
            if (collapseSfx != null) AudioSource.PlayClipAtPoint(collapseSfx, transform.position, 1f);

            if (rubblePrefab != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var p = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(3f, 6f);
                    Instantiate(rubblePrefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                }
            }

            var cc = playerRig != null ? playerRig.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;

            Vector3 towerStart = towerVisualRoot.position;
            Vector3 rigStart = playerRig != null ? playerRig.position : Vector3.zero;
            Vector3 drop = Vector3.down * collapseStepHeight;

            for (float t = 0f; t < 1f; t += Time.deltaTime / collapseDuration)
            {
                // Ease-in with a little shake so it feels like stone giving way.
                float e = t * t;
                Vector3 shake = Random.insideUnitSphere * 0.03f * (1f - t);
                towerVisualRoot.position = towerStart + drop * e + shake;
                if (playerRig != null) playerRig.position = rigStart + drop * e;
                yield return null;
            }
            towerVisualRoot.position = towerStart + drop;
            if (playerRig != null) playerRig.position = rigStart + drop;
            if (cc != null) cc.enabled = true;
        }
    }
}
