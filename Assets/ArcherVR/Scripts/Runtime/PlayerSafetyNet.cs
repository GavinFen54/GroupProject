using System.Collections;
using UnityEngine;

namespace ArcherVR
{
    /// <summary>
    /// Keeps the player on the tower top. If the XR rig ever drops more than
    /// <see cref="fallDistance"/> below the stand point (falling through geometry,
    /// stepping off the edge), it is put straight back. The stand point is a child of
    /// the tower, so it follows the tower down when it collapses.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PlayerSafetyNet : MonoBehaviour
    {
        public Transform playerRig;
        public Transform standPoint;
        public float fallDistance = 1.5f;

        CharacterController cc;

        IEnumerator Start()
        {
            if (playerRig != null) cc = playerRig.GetComponent<CharacterController>();
            Snap();
            // Tracking can take a few frames to report the real head position; re-centre once it has.
            yield return new WaitForSeconds(0.5f);
            Snap();
        }

        void LateUpdate()
        {
            if (playerRig == null || standPoint == null) return;
            if (playerRig.position.y < standPoint.position.y - fallDistance) Snap();
        }

        public void Snap()
        {
            if (playerRig == null || standPoint == null) return;
            if (cc != null) cc.enabled = false;
            playerRig.position = standPoint.position;
            // Shift the rig so the player's head (not the rig origin) is over the stand point.
            // Otherwise standing off-centre in the room, or the simulator's walk keys,
            // can leave the player hanging off the edge of the tower.
            var cam = Camera.main;
            if (cam != null)
            {
                var offset = standPoint.position - cam.transform.position;
                offset.y = 0f;
                playerRig.position += offset;
            }
            if (cc != null) cc.enabled = true;
        }
    }
}
