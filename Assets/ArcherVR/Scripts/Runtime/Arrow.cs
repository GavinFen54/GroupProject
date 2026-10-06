using UnityEngine;

namespace ArcherVR
{
    /// <summary>Physics arrow: flies nose-first, damages anything with Health, then sticks.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Arrow : MonoBehaviour
    {
        public float damage = 25f;
        public float lifetime = 8f;

        Rigidbody rb;
        bool stuck;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Launch(Vector3 velocity, Collider[] ignore)
        {
            var myCols = GetComponentsInChildren<Collider>();
            if (ignore != null)
                foreach (var other in ignore)
                    if (other != null)
                        foreach (var mine in myCols) Physics.IgnoreCollision(mine, other);

            rb.isKinematic = false;
            rb.linearVelocity = velocity;
            Destroy(gameObject, lifetime);
        }

        void FixedUpdate()
        {
            if (stuck) return;
            var v = rb.linearVelocity;
            if (v.sqrMagnitude > 0.5f) rb.MoveRotation(Quaternion.LookRotation(v));
        }

        void OnCollisionEnter(Collision collision)
        {
            if (stuck) return;
            stuck = true;

            var health = collision.collider.GetComponentInParent<Health>();
            if (health != null) health.TakeDamage(damage);

            rb.isKinematic = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            transform.SetParent(collision.transform, true);
        }
    }
}
