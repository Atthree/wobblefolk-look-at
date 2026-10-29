using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>Looks at a specific transform, e.g. another character's head. No target while it is missing or inactive.</summary>
    [AddComponentMenu("Wobblefolk/Look At/Transform Look Target")]
    public sealed class TransformLookTarget : LookTargetProvider
    {
        public Transform target;
        [Tooltip("Offset from the target's pivot (m).")]
        public Vector3 offset;
        [Tooltip("Interpret the offset in the target's local space instead of world space.")]
        public bool localOffset = true;

        protected override bool Resolve(in LookContext context, out Vector3 worldPoint)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                worldPoint = default;
                return false;
            }

            worldPoint = localOffset ? target.TransformPoint(offset) : target.position + offset;
            return true;
        }
    }
}
