using System;
using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>Tuning of one link of the look chain.</summary>
    [Serializable]
    public sealed class LookSegmentSettings
    {
        [Tooltip("Fraction of the remaining look angle this link takes over (0..1). For the eyes: of what the body leaves.")]
        [Range(0f, 1f)] public float weight = 0.5f;
        [Tooltip("The link only joins in for look angles beyond this (degrees), so small glances do not move the body.")]
        [Range(0f, 180f)] public float engageAngle = 0f;

        [Header("Limits (degrees, relative to the link below)")]
        [Range(0f, 180f)] public float maxYaw = 45f;
        [Range(0f, 90f)] public float maxPitchUp = 30f;
        [Range(0f, 90f)] public float maxPitchDown = 30f;

        [Header("Motion")]
        [Tooltip("Time to roughly reach the target (critically damped spring). 0 = instant.")]
        [Min(0f)] public float smoothTime = 0.15f;
        [Tooltip("Speed cap (degrees/s). 0 = unlimited.")]
        [Min(0f)] public float maxSpeed = 0f;
        [Tooltip("Reaction delay: the link reacts to where the target was this long ago (s).")]
        [Min(0f)] public float delay = 0f;

        public LookSegmentSettings Clone() => (LookSegmentSettings)MemberwiseClone();

        internal static LookSegmentSettings Create(float weight, float engage, float yaw, float up, float down,
            float smoothTime, float maxSpeed, float delay)
        {
            return new LookSegmentSettings
            {
                weight = weight,
                engageAngle = engage,
                maxYaw = yaw,
                maxPitchUp = up,
                maxPitchDown = down,
                smoothTime = smoothTime,
                maxSpeed = maxSpeed,
                delay = delay,
            };
        }
    }

    public enum BehindBehaviour
    {
        /// <summary>Turn as far as the limits allow towards the side the body is already turned to.</summary>
        ClampToLimits = 0,
        /// <summary>Give up and return to looking straight ahead.</summary>
        ReturnToNeutral = 1,
    }

    [Serializable]
    public sealed class BehindSettings
    {
        public BehindBehaviour behaviour = BehindBehaviour.ClampToLimits;
        [Tooltip("A target more than this far to the side (yaw, degrees) counts as behind the character.")]
        [Range(0f, 180f)] public float enterAngle = 115f;
        [Tooltip("The target counts as in front again once it is back within this yaw (degrees). Lower than the " +
                 "enter angle so the state does not flicker at the boundary.")]
        [Range(0f, 180f)] public float exitAngle = 100f;
    }

    /// <summary>Settings of the look chain: per-link weights, limits, speeds and delays, and what to do with targets behind.</summary>
    [CreateAssetMenu(menuName = "Wobblefolk/Look Profile", fileName = "LookProfile")]
    public sealed class LookProfile : ScriptableObject
    {
        // Defaults are the "realistic" profile: eyes lead, then head and neck, shoulders and torso catch up last.
        public LookSegmentSettings torso = LookSegmentSettings.Create(0.3f, 45f, 20f, 5f, 10f, 0.45f, 0f, 0.25f);
        public LookSegmentSettings upperChest = LookSegmentSettings.Create(0.35f, 30f, 25f, 10f, 15f, 0.35f, 0f, 0.15f);
        public LookSegmentSettings neck = LookSegmentSettings.Create(0.4f, 10f, 30f, 15f, 20f, 0.2f, 0f, 0.06f);
        public LookSegmentSettings head = LookSegmentSettings.Create(0.6f, 5f, 60f, 35f, 40f, 0.15f, 0f, 0.05f);
        public LookSegmentSettings eyes = LookSegmentSettings.Create(1f, 0f, 35f, 25f, 30f, 0.04f, 0f, 0f);

        [Tooltip("Upper bound of the summed yaw of torso, chest, neck and head (degrees), so the spine never twists " +
                 "further than this even when every link is at its own limit.")]
        [Range(0f, 360f)] public float maxBodyYaw = 120f;

        public BehindSettings behind = new BehindSettings();

        public LookSegmentSettings Get(LookSegment segment)
        {
            switch (segment)
            {
                case LookSegment.Torso: return torso;
                case LookSegment.UpperChest: return upperChest;
                case LookSegment.Neck: return neck;
                case LookSegment.Head: return head;
                default: return eyes;
            }
        }

        /// <summary>Largest yaw the whole chain can reach with every link at its limit (degrees).</summary>
        public float MaxReachYaw
        {
            get
            {
                float body = torso.maxYaw + upperChest.maxYaw + neck.maxYaw + head.maxYaw;
                return Mathf.Min(body, maxBodyYaw) + eyes.maxYaw;
            }
        }
    }
}
