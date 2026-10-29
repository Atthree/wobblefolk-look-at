using System;
using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// A look direction as yaw (around the character's up axis, positive = right) and pitch (positive = up), in
    /// degrees. Yaw is applied first, then pitch, so there is no gimbal lock as long as pitch stays within ±90°.
    /// </summary>
    [Serializable]
    public struct YawPitch : IEquatable<YawPitch>
    {
        public float yaw;
        public float pitch;

        public YawPitch(float yaw, float pitch)
        {
            this.yaw = yaw;
            this.pitch = pitch;
        }

        public static YawPitch Zero => default;

        public static YawPitch operator +(YawPitch a, YawPitch b) => new YawPitch(a.yaw + b.yaw, a.pitch + b.pitch);
        public static YawPitch operator -(YawPitch a, YawPitch b) => new YawPitch(a.yaw - b.yaw, a.pitch - b.pitch);
        public static YawPitch operator *(YawPitch a, float s) => new YawPitch(a.yaw * s, a.pitch * s);

        public static YawPitch Lerp(YawPitch a, YawPitch b, float t) =>
            new YawPitch(Mathf.LerpUnclamped(a.yaw, b.yaw, t), Mathf.LerpUnclamped(a.pitch, b.pitch, t));

        public bool Equals(YawPitch other) => yaw.Equals(other.yaw) && pitch.Equals(other.pitch);
        public override bool Equals(object obj) => obj is YawPitch other && Equals(other);
        public override int GetHashCode() => (yaw.GetHashCode() * 397) ^ pitch.GetHashCode();
        public override string ToString() => $"(yaw {yaw:F1}°, pitch {pitch:F1}°)";
    }
}
