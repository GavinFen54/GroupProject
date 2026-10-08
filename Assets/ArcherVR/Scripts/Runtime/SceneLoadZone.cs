using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcherVR
{
    /// <summary>
    /// Loads a scene when the player's head comes within range. Used in S1 at the foot of
    /// the tower as a stand-in for climbing the stairs (story map: "Design decision needed:
    /// physically climb, teleport, or transition").
    /// </summary>
    public class SceneLoadZone : MonoBehaviour
    {
        public string sceneName = GameFlow.SceneBattle;
        public float radius = 1.5f;
        public float verticalTolerance = 3f;
        [Tooltip("Ignore the zone for this long after the scene starts.")]
        public float armDelay = 1f;

        Transform head;
        Transform rig;
        bool loading;

        // Look the player up lazily: after a scene change (e.g. arriving from the S0 prologue)
        // the camera may not be ready yet in Start, so keep trying until it is.
        void FindPlayer()
        {
            if (rig == null)
            {
                var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (origin != null)
                {
                    rig = origin.transform;
                    if (origin.Camera != null) head = origin.Camera.transform;
                }
            }
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }

        bool Inside(Transform t)
        {
            if (t == null) return false;
            var d = t.position - transform.position;
            if (Mathf.Abs(d.y) > verticalTolerance) return false;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        void Update()
        {
            if (loading || Time.timeSinceLevelLoad < armDelay) return;
            if (head == null || rig == null) FindPlayer();
            // Either the player's head or their feet (rig) on the circle counts.
            if (Inside(head) || Inside(rig))
            {
                loading = true;
                SceneManager.LoadScene(sceneName);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
