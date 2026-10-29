using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// Base for providers that cast a ray from a camera. The first surface hit becomes the target; without a hit the
    /// target is a point far along the ray. The looking character's own colliders are ignored.
    /// </summary>
    public abstract class RayLookTarget : LookTargetProvider
    {
        const int MaxHits = 16;

        [Tooltip("Camera the ray starts from. Empty = the main camera.")]
        [SerializeField] protected Camera targetCamera;

        [Header("Ray")]
        [Tooltip("Cast the ray against colliders. Off = always use the fallback distance.")]
        public bool raycast = true;
        public LayerMask layers = ~0;
        [Min(0f)] public float maxDistance = 50f;
        [Tooltip("Distance along the ray used when nothing is hit (m).")]
        [Min(0f)] public float fallbackDistance = 20f;
        [Tooltip("Hits closer than this to the camera are skipped (m), e.g. the camera's own near clutter.")]
        [Min(0f)] public float minDistance = 0.3f;

        readonly RaycastHit[] hits = new RaycastHit[MaxHits];

        public Camera Camera
        {
            get => targetCamera != null ? targetCamera : Camera.main;
            set => targetCamera = value;
        }

        /// <summary>True when the last answer came from a surface hit rather than the fallback distance.</summary>
        public bool LastHitSurface { get; private set; }

        protected sealed override bool Resolve(in LookContext context, out Vector3 worldPoint)
        {
            Camera cam = Camera;
            if (cam == null)
            {
                worldPoint = default;
                return false;
            }

            Ray ray = BuildRay(cam);
            worldPoint = default;
            LastHitSurface = raycast && ResolveRay(ray, maxDistance, minDistance, layers, context.root, hits, out worldPoint);
            if (!LastHitSurface)
            {
                worldPoint = ray.GetPoint(fallbackDistance);
            }

            return true;
        }

        protected abstract Ray BuildRay(Camera cam);

        /// <summary>
        /// Nearest hit along <paramref name="ray"/> between the min and max distance that does not belong to
        /// <paramref name="ignore"/>. Allocation-free when given a buffer.
        /// </summary>
        public static bool ResolveRay(Ray ray, float maxDistance, float minDistance, LayerMask layers, Transform ignore,
            RaycastHit[] buffer, out Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(ray, buffer, maxDistance, layers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            point = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = buffer[i];
                if (hit.distance < minDistance || hit.distance >= best)
                {
                    continue;
                }

                if (ignore != null && hit.transform.IsChildOf(ignore))
                {
                    continue;
                }

                best = hit.distance;
                point = hit.point;
            }

            return best < float.MaxValue;
        }
    }
}
