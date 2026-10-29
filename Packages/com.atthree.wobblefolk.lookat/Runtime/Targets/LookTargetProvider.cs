using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>What a target provider knows about the looker when asked for a target.</summary>
    public readonly struct LookContext
    {
        /// <summary>World position the look is measured from, usually between the eyes.</summary>
        public readonly Vector3 origin;
        /// <summary>World forward of the character, used to judge what is in view.</summary>
        public readonly Vector3 forward;
        /// <summary>Root of the looking character; its own colliders are ignored by ray-based providers.</summary>
        public readonly Transform root;

        public LookContext(Vector3 origin, Vector3 forward, Transform root)
        {
            this.origin = origin;
            this.forward = forward;
            this.root = root;
        }
    }

    /// <summary>Answers "where should the character look?" with a world point, or no target at all.</summary>
    public interface ILookTargetProvider
    {
        bool TryGetTarget(in LookContext context, out Vector3 worldPoint);
    }

    /// <summary>Base for target provider components. Remembers the last answer for gizmos and debugging.</summary>
    public abstract class LookTargetProvider : MonoBehaviour, ILookTargetProvider
    {
        [Tooltip("Gizmo colour of the last target point.")]
        [SerializeField] Color gizmoColor = new Color(1f, 0.6f, 0.1f);

        public bool HasLastTarget { get; private set; }
        public Vector3 LastTarget { get; private set; }

        public bool TryGetTarget(in LookContext context, out Vector3 worldPoint)
        {
            // enabled + activeInHierarchy instead of isActiveAndEnabled, which stays false in Edit mode previews.
            worldPoint = default;
            bool found = enabled && gameObject.activeInHierarchy && Resolve(context, out worldPoint);
            if (!found) worldPoint = default;
            HasLastTarget = found;
            LastTarget = worldPoint;
            return found;
        }

        protected abstract bool Resolve(in LookContext context, out Vector3 worldPoint);

        protected virtual void OnDrawGizmosSelected()
        {
            if (!HasLastTarget)
            {
                return;
            }

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(LastTarget, 0.1f);
        }
    }
}
