using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// Pure look-at logic, independent of transforms and physics. Each update it takes the target as yaw / pitch in the
    /// character's frame and produces a yaw / pitch for every link of the chain (torso → chest → neck → head → eyes).
    /// <para>
    /// The target angle is shared out from the base of the spine upwards: every link takes its weighted part of what
    /// is left above its engage angle, clamped to its own limits and to the body's total yaw budget, so the summed
    /// rotation never exceeds the target or the limits. Each body link sees the target with its own reaction delay and
    /// moves with a critically damped spring; the eyes cover whatever the body has not turned yet, so the gaze lands
    /// first and the eyes drift back to centre as the head and shoulders catch up.
    /// </para>
    /// </summary>
    public sealed class LookSolver
    {
        public const int DefaultHistoryCapacity = 256;

        struct SegmentState
        {
            public YawPitch angles;
            public YawPitch velocity;
        }

        readonly SegmentState[] states = new SegmentState[LookSegments.Count];
        readonly LookHistory history;
        float time;
        bool behind;
        float behindSide = 1f;

        public LookSolver(LookProfile profile, int historyCapacity = DefaultHistoryCapacity)
        {
            Profile = profile;
            history = new LookHistory(historyCapacity);
            Reset();
        }

        public LookProfile Profile { get; set; }

        /// <summary>Solver time, the sum of all update steps (s).</summary>
        public float Time => time;

        public bool HasTarget { get; private set; }

        /// <summary>True while the target is behind the character (see <see cref="BehindSettings"/>).</summary>
        public bool IsBehind => behind;

        /// <summary>The requested look direction of the last update, before limits and behind handling.</summary>
        public YawPitch RequestedTarget { get; private set; }

        /// <summary>The look direction the chain currently aims for, after behind handling.</summary>
        public YawPitch Goal { get; private set; }

        /// <summary>Rotation of one link relative to the link below it (degrees).</summary>
        public YawPitch GetLocal(LookSegment segment) => states[(int)segment].angles;

        /// <summary>Summed rotation from the torso up to and including <paramref name="segment"/>, relative to the character.</summary>
        public YawPitch GetAccumulated(LookSegment segment)
        {
            YawPitch sum = default;
            for (int i = 0; i <= (int)segment; i++)
            {
                sum += states[i].angles;
            }

            return sum;
        }

        /// <summary>Where the eyes currently point, relative to the character.</summary>
        public YawPitch Gaze => GetAccumulated(LookSegment.Eyes);

        /// <summary>Angle between the gaze and the requested target (degrees). 0 without a target.</summary>
        public float GazeError => HasTarget ? LookMath.AngleBetween(Gaze, RequestedTarget) : 0f;

        public void Reset()
        {
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = default;
            }

            // Start from a neutral sample, so delayed links do not see the first target before their delay has passed.
            history.Clear();
            history.Push(0f, default);
            time = 0f;
            behind = false;
            behindSide = 1f;
            HasTarget = false;
            RequestedTarget = default;
            Goal = default;
        }

        /// <summary>Advances the solver towards a target given as a direction in the character's local frame.</summary>
        public void Update(Vector3 localDirection, bool hasTarget, float deltaTime)
        {
            Update(LookMath.ToYawPitch(localDirection, RequestedTarget), hasTarget, deltaTime);
        }

        /// <summary>Advances the solver towards a target given as yaw / pitch relative to the character.</summary>
        public void Update(YawPitch target, bool hasTarget, float deltaTime)
        {
            if (Profile == null)
            {
                return;
            }

            float dt = Mathf.Max(0f, deltaTime);
            time += dt;
            HasTarget = hasTarget;
            RequestedTarget = target;
            target.yaw = Mathf.DeltaAngle(0f, target.yaw);

            Goal = hasTarget ? HandleBehind(target) : default;
            history.Push(time, Goal);

            for (int i = 0; i < LookSegments.BodyCount; i++)
            {
                LookSegmentSettings settings = Profile.Get((LookSegment)i);
                YawPitch seen = history.Sample(time - settings.delay);
                Step(ref states[i], ShareOf(i, seen), settings, dt);
            }

            // The eyes aim at whatever the body has not covered yet, so the gaze reaches the target first.
            LookSegmentSettings eyes = Profile.eyes;
            YawPitch eyeTarget = history.Sample(time - eyes.delay) - GetAccumulated(LookSegment.Head);
            Step(ref states[(int)LookSegment.Eyes], Clamp(eyeTarget * eyes.weight, eyes), eyes, dt);
        }

        YawPitch HandleBehind(YawPitch target)
        {
            BehindSettings settings = Profile.behind;
            float absYaw = Mathf.Abs(target.yaw);

            if (!behind && absYaw > settings.enterAngle)
            {
                behind = true;
                // Stay on the side the body is already turned to, so a target passing straight behind does not
                // make the head whip from one shoulder to the other.
                float bodyYaw = GetAccumulated(LookSegment.Head).yaw;
                behindSide = Mathf.Abs(bodyYaw) > 1f ? Mathf.Sign(bodyYaw) : Mathf.Sign(target.yaw);
            }
            else if (behind && absYaw < settings.exitAngle)
            {
                behind = false;
            }

            if (!behind)
            {
                return target;
            }

            if (settings.behaviour == BehindBehaviour.ReturnToNeutral)
            {
                return default;
            }

            return new YawPitch(behindSide * Mathf.Min(Profile.MaxReachYaw, absYaw), target.pitch);
        }

        /// <summary>The part of <paramref name="total"/> body link <paramref name="index"/> takes in the base-to-head distribution.</summary>
        YawPitch ShareOf(int index, YawPitch total)
        {
            YawPitch remaining = total;
            float bodyYaw = 0f;
            YawPitch share = default;

            for (int i = 0; i <= index; i++)
            {
                LookSegmentSettings settings = Profile.Get((LookSegment)i);
                share = Clamp(new YawPitch(Take(remaining.yaw, settings), Take(remaining.pitch, settings)), settings);

                // The spine as a whole may not twist further than the body yaw budget.
                float budget = Mathf.Max(0f, Profile.maxBodyYaw - Mathf.Abs(bodyYaw));
                share.yaw = Mathf.Clamp(share.yaw, -budget, budget);

                bodyYaw += share.yaw;
                remaining -= share;
            }

            return share;
        }

        static float Take(float remaining, LookSegmentSettings settings)
        {
            float beyond = Mathf.Max(0f, Mathf.Abs(remaining) - settings.engageAngle);
            return Mathf.Sign(remaining) * beyond * settings.weight;
        }

        static YawPitch Clamp(YawPitch angles, LookSegmentSettings settings)
        {
            return new YawPitch(
                Mathf.Clamp(angles.yaw, -settings.maxYaw, settings.maxYaw),
                Mathf.Clamp(angles.pitch, -settings.maxPitchDown, settings.maxPitchUp));
        }

        static void Step(ref SegmentState state, YawPitch target, LookSegmentSettings settings, float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            float maxSpeed = settings.maxSpeed > 0f ? settings.maxSpeed : Mathf.Infinity;
            if (settings.smoothTime <= 0f)
            {
                state.angles = target;
                state.velocity = default;
                return;
            }

            state.angles.yaw = Mathf.SmoothDamp(state.angles.yaw, target.yaw, ref state.velocity.yaw, settings.smoothTime, maxSpeed, dt);
            state.angles.pitch = Mathf.SmoothDamp(state.angles.pitch, target.pitch, ref state.velocity.pitch, settings.smoothTime, maxSpeed, dt);
        }
    }
}
