using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>Everything a driver needs besides the solver for one look update.</summary>
    public readonly struct LookFrame
    {
        /// <summary>World rotation of the character frame the solver's angles are relative to.</summary>
        public readonly Quaternion characterRotation;
        /// <summary>World point being looked at. Only meaningful when <see cref="hasTarget"/>.</summary>
        public readonly Vector3 target;
        public readonly bool hasTarget;
        /// <summary>Blend with the unmodified pose: 0 = no effect, 1 = full look.</summary>
        public readonly float weight;
        /// <summary>Extra eye rotation on top of the solved gaze, e.g. saccades (degrees).</summary>
        public readonly YawPitch eyeOffset;
        /// <summary>False when the eyes are skipped, e.g. by distance LOD.</summary>
        public readonly bool driveEyes;

        public LookFrame(Quaternion characterRotation, Vector3 target, bool hasTarget, float weight, YawPitch eyeOffset, bool driveEyes)
        {
            this.characterRotation = characterRotation;
            this.target = target;
            this.hasTarget = hasTarget;
            this.weight = weight;
            this.eyeOffset = eyeOffset;
            this.driveEyes = driveEyes;
        }
    }

    /// <summary>
    /// Applies the solver's output to a character. Several drivers can work together on one character, e.g. a joint
    /// driver for the head of an active ragdoll and a transform driver for its eyes.
    /// </summary>
    public interface ILookDriver
    {
        void ApplyLook(LookSolver solver, in LookFrame frame);
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

        /// <summary>
        /// Angles one eye should have relative to the character: the solved gaze plus offsets, converged towards the
        /// target as seen from the eye itself, then clamped relative to the head with separate inner / outer limits.
        /// </summary>
        /// <param name="isLeft">Left eye: turning right (positive yaw) is towards the nose.</param>
        public static YawPitch EyeAngles(LookSolver solver, in LookFrame frame, Vector3 eyePosition, bool isLeft)
        {
            LookProfile profile = solver.Profile;
            YawPitch angles = solver.Gaze + frame.eyeOffset;

            float convergence = profile.eyeDetails.convergence;
            if (frame.hasTarget && convergence > 0f)
            {
                Vector3 fromEye = Quaternion.Inverse(frame.characterRotation) * (frame.target - eyePosition);
                YawPitch own = LookMath.ToYawPitch(fromEye, solver.RequestedTarget);
                YawPitch shared = solver.RequestedTarget;
                angles.yaw += convergence * Mathf.DeltaAngle(shared.yaw, own.yaw);
                angles.pitch += convergence * (own.pitch - shared.pitch);
            }

            YawPitch head = solver.GetAccumulated(LookSegment.Head);
            YawPitch relative = angles - head;
            EyeSettings eyes = profile.eyeDetails;
            // Positive yaw is to the character's right: towards the nose for the left eye, away from it for the right.
            float maxRight = isLeft ? eyes.maxInnerYaw : eyes.maxOuterYaw;
            float maxLeft = isLeft ? eyes.maxOuterYaw : eyes.maxInnerYaw;
            relative.yaw = Mathf.Clamp(relative.yaw, -maxLeft, maxRight);
            relative.pitch = Mathf.Clamp(relative.pitch, -profile.eyes.maxPitchDown, profile.eyes.maxPitchUp);
            return head + relative;
        }
    }
}
