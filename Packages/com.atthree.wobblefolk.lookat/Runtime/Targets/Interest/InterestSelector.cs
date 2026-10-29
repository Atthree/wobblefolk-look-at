using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.LookAt
{
    [Serializable]
    public sealed class InterestSettings
    {
        [Tooltip("Points further than this from the looker's forward direction are out of view (degrees).")]
        [Range(0f, 180f)] public float maxViewAngle = 100f;
        [Tooltip("How strongly distance lowers a point's score: 0 = ignored, 1 = linear falloff to zero at its radius.")]
        [Range(0f, 1f)] public float distanceFalloff = 1f;
        [Tooltip("How strongly the view angle lowers a point's score: 0 = ignored, 1 = linear falloff to zero at the edge.")]
        [Range(0f, 1f)] public float angleFalloff = 0.5f;
        [Tooltip("Points scoring below this are ignored.")]
        [Min(0f)] public float minScore = 0.05f;
        [Tooltip("The current point's score is multiplied by this, so attention does not flicker between similar points.")]
        [Min(1f)] public float stickiness = 1.25f;
    }

    /// <summary>
    /// Picks the most interesting point for a looker. Pure logic: score = priority × distance factor × angle factor,
    /// where points beyond their radius or outside the view angle score zero, and the currently watched point gets a
    /// stickiness bonus.
    /// </summary>
    public static class InterestSelector
    {
        public static float Score(IInterestPoint point, Vector3 origin, Vector3 forward, InterestSettings settings)
        {
            if (point == null || !point.IsAvailable || point.Radius <= 0f)
            {
                return 0f;
            }

            Vector3 offset = point.LookPosition - origin;
            float distance = offset.magnitude;
            if (distance > point.Radius)
            {
                return 0f;
            }

            float angle = distance > 1e-5f && forward.sqrMagnitude > 1e-8f ? Vector3.Angle(forward, offset) : 0f;
            if (angle > settings.maxViewAngle)
            {
                return 0f;
            }

            float distanceFactor = 1f - settings.distanceFalloff * (distance / point.Radius);
            float angleFactor = settings.maxViewAngle > 0f ? 1f - settings.angleFalloff * (angle / settings.maxViewAngle) : 1f;
            return point.Priority * distanceFactor * angleFactor;
        }

        /// <summary>The best point, or null when none scores at least the minimum.</summary>
        public static IInterestPoint Select(IReadOnlyList<IInterestPoint> points, Vector3 origin, Vector3 forward,
            InterestSettings settings, IInterestPoint current, out float bestScore)
        {
            IInterestPoint best = null;
            bestScore = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                IInterestPoint point = points[i];
                float score = Score(point, origin, forward, settings);
                if (point == current) score *= settings.stickiness;

                if (score >= settings.minScore && score > bestScore)
                {
                    best = point;
                    bestScore = score;
                }
            }

            return best;
        }
    }
}
