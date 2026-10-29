using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// Lets interest points pull the gaze. Picks the best <see cref="IInterestPoint"/> around the looker and turns the
    /// look towards it by <see cref="pull"/>; with no interesting point it passes the fallback provider's target
    /// through unchanged. Put it in front of a mouse provider for a player, or use it alone for an idle NPC.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Look At/Interest Look Target")]
    public sealed class InterestLookTarget : LookTargetProvider
    {
        [Tooltip("Provider used when no interest point is attractive enough, e.g. the mouse. Optional.")]
        public LookTargetProvider fallback;
        [Tooltip("How far the gaze turns from the fallback target towards the interest point: 1 = all the way.")]
        [Range(0f, 1f)] public float pull = 1f;
        [Tooltip("Seconds between re-evaluating which point is the most interesting.")]
        [Min(0f)] public float reevaluateInterval = 0.25f;
        public InterestSettings settings = new InterestSettings();

        IInterestPoint current;
        float nextEvaluation;

        /// <summary>The interest point currently attracting the gaze, if any.</summary>
        public IInterestPoint Current => current;

        protected override bool Resolve(in LookContext context, out Vector3 worldPoint)
        {
            if (Time.time >= nextEvaluation || (current != null && !current.IsAvailable))
            {
                nextEvaluation = Time.time + reevaluateInterval;
                current = InterestSelector.Select(InterestPointRegistry.Points, context.origin, context.forward, settings,
                    current, out _);
            }

            Vector3 fallbackPoint = default;
            bool hasFallback = fallback != null && fallback.TryGetTarget(context, out fallbackPoint);
            if (current == null)
            {
                worldPoint = hasFallback ? fallbackPoint : default;
                return hasFallback;
            }

            Vector3 interest = current.LookPosition;
            if (!hasFallback || pull >= 1f)
            {
                worldPoint = interest;
                return true;
            }

            // Blend directions, not points, so targets at very different distances blend by angle.
            Vector3 toFallback = fallbackPoint - context.origin;
            Vector3 toInterest = interest - context.origin;
            Vector3 direction = Vector3.Slerp(toFallback.normalized, toInterest.normalized, pull);
            float distance = Mathf.Lerp(toFallback.magnitude, toInterest.magnitude, pull);
            worldPoint = context.origin + direction * distance;
            return true;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (current != null)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f);
                Gizmos.DrawWireSphere(current.LookPosition, 0.12f);
            }
        }
    }
}
