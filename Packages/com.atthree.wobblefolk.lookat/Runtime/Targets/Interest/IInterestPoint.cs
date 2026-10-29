using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>Something in the scene that can attract a character's gaze: a door, a box, another character.</summary>
    public interface IInterestPoint
    {
        /// <summary>World point to look at.</summary>
        Vector3 LookPosition { get; }

        /// <summary>Relative importance. Points with higher priority win over closer, less important ones.</summary>
        float Priority { get; }

        /// <summary>The point only attracts lookers within this distance (m).</summary>
        float Radius { get; }

        /// <summary>False while the point should be ignored, e.g. its object is disabled.</summary>
        bool IsAvailable { get; }
    }

    /// <summary>
    /// Scene-wide list of interest points. Points register themselves while enabled, so selectors never have to
    /// search the scene. Registering and iterating do not allocate.
    /// </summary>
    public static class InterestPointRegistry
    {
        static readonly List<IInterestPoint> points = new List<IInterestPoint>();

        public static IReadOnlyList<IInterestPoint> Points => points;

        public static void Register(IInterestPoint point)
        {
            if (point != null && !points.Contains(point)) points.Add(point);
        }

        public static void Unregister(IInterestPoint point)
        {
            points.Remove(point);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearOnLoad()
        {
            // Supports entering Play mode with domain reload disabled.
            points.Clear();
        }
    }
}
