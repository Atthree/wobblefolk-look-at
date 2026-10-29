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

    [Serializable]
    public sealed class EyeSettings
    {
        [Tooltip("How much each eye aims at the target from its own position (0 = both eyes parallel, 1 = fully " +
                 "converged). Makes eyes cross slightly on near targets.")]
        [Range(0f, 1f)] public float convergence = 1f;
        [Tooltip("Furthest an eye turns towards the nose, relative to the head (degrees).")]
        [Range(0f, 90f)] public float maxInnerYaw = 25f;
        [Tooltip("Furthest an eye turns away from the nose, relative to the head (degrees).")]
        [Range(0f, 90f)] public float maxOuterYaw = 35f;
    }

    [Serializable]
    public sealed class SaccadeSettings
    {
        public bool enabled = true;
        [Tooltip("Random time between small eye jumps (s).")]
        [Min(0.01f)] public float minInterval = 0.4f;
        [Min(0.01f)] public float maxInterval = 1.8f;
        [Tooltip("Size of the small jumps around the target (degrees).")]
        [Min(0f)] public float minAmplitude = 0.5f;
        [Min(0f)] public float maxAmplitude = 2.5f;
        [Tooltip("Once the gaze has rested this long, larger glances away become possible (s).")]
        [Min(0f)] public float idleTime = 2f;
        [Tooltip("Chance that a jump is a larger glance away while idle (0..1).")]
        [Range(0f, 1f)] public float idleGlanceChance = 0.2f;
        [Tooltip("Size of an idle glance away (degrees).")]
        [Min(0f)] public float idleGlanceAmplitude = 10f;
        [Tooltip("How quickly the eyes jump to a new offset (s). Saccades are very fast.")]
        [Min(0f)] public float jumpTime = 0.02f;
        [Tooltip("A target moving faster than this (degrees/s) counts as not resting.")]
        [Min(0f)] public float restingSpeed = 15f;
    }

    [Serializable]
    public sealed class BlinkSettings
    {
        public bool enabled = true;
        [Tooltip("Random time between blinks (s).")]
        [Min(0.05f)] public float minInterval = 2.5f;
        [Min(0.05f)] public float maxInterval = 6f;
        [Tooltip("Time to close the lids (s).")]
        [Min(0.01f)] public float closeTime = 0.06f;
        [Tooltip("Time the lids stay closed (s).")]
        [Min(0f)] public float holdTime = 0.03f;
        [Tooltip("Time to open the lids again (s).")]
        [Min(0.01f)] public float openTime = 0.1f;
        [Tooltip("Chance that a blink is immediately followed by a second one (0..1).")]
        [Range(0f, 1f)] public float doubleBlinkChance = 0.1f;
        [Tooltip("A gaze jump larger than this triggers a blink (degrees). 0 = off.")]
        [Min(0f)] public float blinkOnGazeShift = 35f;
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

        [Header("Eyes")]
        public EyeSettings eyeDetails = new EyeSettings();
        [Tooltip("Small random eye jumps. Needs an EyeBehaviour on the character.")]
        public SaccadeSettings saccades = new SaccadeSettings();
        [Tooltip("Blinking. Needs an EyeBehaviour with eyelids on the character.")]
        public BlinkSettings blinks = new BlinkSettings();

        [ContextMenu("Apply Preset/Realistic")]
        void ApplyRealistic() => LookProfilePresets.Apply(this, LookPreset.Realistic);

        [ContextMenu("Apply Preset/Robotic Snappy")]
        void ApplyRoboticSnappy() => LookProfilePresets.Apply(this, LookPreset.RoboticSnappy);

        [ContextMenu("Apply Preset/Sluggish")]
        void ApplySluggish() => LookProfilePresets.Apply(this, LookPreset.Sluggish);

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
