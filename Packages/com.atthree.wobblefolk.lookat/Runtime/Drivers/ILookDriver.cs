using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// Applies the solver's output to a character. Several drivers can work together on one character, e.g. a joint
    /// driver for the head of an active ragdoll and a transform driver for its eyes.
    /// </summary>
    public interface ILookDriver
    {
        /// <param name="solver">Solved angles per link of the chain.</param>
        /// <param name="characterRotation">World rotation of the character frame the angles are relative to.</param>
        /// <param name="weight">Blend with the unmodified pose: 0 = no effect, 1 = full look.</param>
        void ApplyLook(LookSolver solver, Quaternion characterRotation, float weight);
    }

    public static class LookDriverMath
    {
        /// <summary>
        /// World-space rotation that turns a bone by the accumulated look angles of its link, about the character's
        /// own axes, blended by <paramref name="weight"/>.
        /// </summary>
        public static Quaternion WorldDelta(YawPitch accumulated, Quaternion characterRotation, float weight)
        {
            Quaternion local = LookMath.ToRotation(accumulated);
            Quaternion world = characterRotation * local * Quaternion.Inverse(characterRotation);
            return weight >= 1f ? world : Quaternion.Slerp(Quaternion.identity, world, Mathf.Clamp01(weight));
        }
    }
}
