using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcherVR
{
    /// <summary>
    /// PLACEHOLDER bow so the waves are playable today: grab it (grip) and pull the
    /// trigger to fire along where the bow points. Shows an aim arc while held.
    /// Swap for a real nock-and-draw bow later; enemies only care about Arrow + Health.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Bow : MonoBehaviour
    {
        public Arrow arrowPrefab;
        public Transform muzzle;
        public float arrowSpeed = 45f;
        public float cooldown = 0.35f;

        [Header("Aim arc")]
        public LineRenderer aimLine;
        public int aimPoints = 30;
        public float aimTimeStep = 0.05f;

        [Header("Return to rack when dropped")]
        public float returnDelay = 1.5f;

        /// <summary>True once the player has picked up a bow in the current scene.</summary>
        public static bool EverGrabbed { get; private set; }

        XRGrabInteractable grab;
        Rigidbody rb;
        float nextShot;
        Vector3 homePos;
        Quaternion homeRot;
        Collider[] ignore;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            rb = GetComponent<Rigidbody>();
            if (muzzle == null) muzzle = transform;
            homePos = transform.position;
            homeRot = transform.rotation;

            grab.activated.AddListener(_ => Fire());
            EverGrabbed = false;
            grab.selectEntered.AddListener(_ => { EverGrabbed = true; StopAllCoroutines(); });
            grab.selectExited.AddListener(_ => StartCoroutine(ReturnHome()));
            if (aimLine != null) aimLine.enabled = false;
        }

        void Start()
        {
            var cols = new System.Collections.Generic.List<Collider>(GetComponentsInChildren<Collider>());
            var player = FindFirstObjectByType<CharacterController>();
            if (player != null) cols.Add(player);
            ignore = cols.ToArray();
        }

        public void Fire()
        {
            if (arrowPrefab == null || Time.time < nextShot) return;
            nextShot = Time.time + cooldown;
            var arrow = Instantiate(arrowPrefab, muzzle.position, muzzle.rotation);
            arrow.Launch(muzzle.forward * arrowSpeed, ignore);
        }

        void Update()
        {
            if (aimLine == null) return;
            bool show = grab.isSelected;
            aimLine.enabled = show;
            if (!show) return;

            aimLine.positionCount = aimPoints;
            Vector3 p = muzzle.position;
            Vector3 v = muzzle.forward * arrowSpeed;
            for (int i = 0; i < aimPoints; i++)
            {
                aimLine.SetPosition(i, p);
                p += v * aimTimeStep;
                v += Physics.gravity * aimTimeStep;
            }
        }

        IEnumerator ReturnHome()
        {
            yield return new WaitForSeconds(returnDelay);
            if (grab.isSelected) yield break;
            if (rb != null)
            {
                // Velocity can only be set on a non-kinematic body, so clear it first.
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }
            transform.SetPositionAndRotation(homePos, homeRot);
        }
    }
}
