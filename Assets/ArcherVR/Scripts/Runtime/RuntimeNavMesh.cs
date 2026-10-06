using Unity.AI.Navigation;
using UnityEngine;

namespace ArcherVR
{
    /// <summary>
    /// Bakes the NavMesh when the scene loads so enemies can path around whatever the team
    /// has placed, without anyone needing to remember to re-bake after moving things.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMesh : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<NavMeshSurface>().BuildNavMesh();
        }
    }
}
