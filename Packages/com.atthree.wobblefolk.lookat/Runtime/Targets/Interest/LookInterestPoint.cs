using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>Marks this object as something characters may look at. Registers itself while enabled.</summary>
    [AddComponentMenu("Wobblefolk/Look At/Look Interest Point")]
    public sealed class LookInterestPoint : MonoBehaviour, IInterestPoint
    {
        [Tooltip("Relative importance. Higher wins over closer but less important points.")]
        [Min(0f)] public float priority = 1f;
        [Tooltip("Characters further away than this ignore the point (m).")]
        [Min(0f)] public float radius = 6f;
        [Tooltip("Look position relative to this object, in its local space.")]
        public Vector3 offset;

        public Vector3 LookPosition => transform.TransformPoint(offset);
        public float Priority => priority;
        public float Radius => radius;
        public bool IsAvailable => this != null && enabled && gameObject.activeInHierarchy;

        void OnEnable() => InterestPointRegistry.Register(this);
        void OnDisable() => InterestPointRegistry.Unregister(this);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(LookPosition, 0.08f);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(LookPosition, radius);
        }
    }
}
