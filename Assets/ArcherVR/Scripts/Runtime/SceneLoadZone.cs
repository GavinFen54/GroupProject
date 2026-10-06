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
        bool loading;

        void Start()
        {
            var cam = Camera.main;
            if (cam != null) head = cam.transform;
        }

        void Update()
        {
            if (loading || head == null || Time.timeSinceLevelLoad < armDelay) return;
            var d = head.position - transform.position;
            if (Mathf.Abs(d.y) > verticalTolerance) return;
            d.y = 0f;
            if (d.sqrMagnitude <= radius * radius)
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
