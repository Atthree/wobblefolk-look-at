using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>Conversions between directions, yaw / pitch and rotations in a character's local frame (+Z forward, +Y up).</summary>
    public static class LookMath
    {
        const float MinLengthSq = 1e-12f;
        const float MinHorizontal = 1e-6f;

        /// <summary>
        /// Splits a local direction into yaw and pitch. A zero direction returns <paramref name="fallback"/>; a
        /// direction straight up or down keeps the fallback's yaw, because yaw is undefined there.
        /// </summary>
        public static YawPitch ToYawPitch(Vector3 localDirection, YawPitch fallback = default)
        {
            if (localDirection.sqrMagnitude < MinLengthSq)
            {
                return fallback;
            }

            float horizontal = Mathf.Sqrt(localDirection.x * localDirection.x + localDirection.z * localDirection.z);
            float yaw = horizontal > MinHorizontal
                ? Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg
                : fallback.yaw;
            float pitch = Mathf.Atan2(localDirection.y, horizontal) * Mathf.Rad2Deg;
            return new YawPitch(yaw, pitch);
        }

        /// <summary>Unit direction for a yaw / pitch pair.</summary>
        public static Vector3 ToDirection(YawPitch angles)
        {
            float yaw = angles.yaw * Mathf.Deg2Rad;
            float pitch = angles.pitch * Mathf.Deg2Rad;
            float cosPitch = Mathf.Cos(pitch);
            return new Vector3(Mathf.Sin(yaw) * cosPitch, Mathf.Sin(pitch), Mathf.Cos(yaw) * cosPitch);
        }

        /// <summary>Rotation that turns +Z towards <see cref="ToDirection"/> without roll: yaw about up, then pitch about right.</summary>
        public static Quaternion ToRotation(YawPitch angles)
        {
            return Quaternion.AngleAxis(angles.yaw, Vector3.up) * Quaternion.AngleAxis(-angles.pitch, Vector3.right);
        }

        /// <summary>Angle in degrees between the directions of two yaw / pitch pairs.</summary>
        public static float AngleBetween(YawPitch a, YawPitch b)
        {
            return Vector3.Angle(ToDirection(a), ToDirection(b));
        }
    }
}
