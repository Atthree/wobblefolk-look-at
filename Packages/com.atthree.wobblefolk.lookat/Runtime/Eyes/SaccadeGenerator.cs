using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// Pure logic for saccades: small, fast, random eye jumps around the target at random intervals, plus occasional
    /// larger glances away once the gaze has rested for a while. Produces a yaw / pitch offset for the eyes.
    /// Deterministic for a given seed.
    /// </summary>
    public sealed class SaccadeGenerator
    {
        readonly System.Random random;
        float timer;
        float restingTime;
        YawPitch target;
        YawPitch offset;
        YawPitch velocity;

        public SaccadeGenerator(int seed)
        {
            random = new System.Random(seed);
        }

        /// <summary>Current eye offset (degrees).</summary>
        public YawPitch Offset => offset;

        /// <summary>Offset the eyes are jumping towards (degrees).</summary>
        public YawPitch TargetOffset => target;

        public int JumpCount { get; private set; }

        /// <summary>Whether the last jump was a larger idle glance away.</summary>
        public bool LastWasGlance { get; private set; }

        public void Reset()
        {
            timer = 0f;
            restingTime = 0f;
            target = default;
            offset = default;
            velocity = default;
            JumpCount = 0;
        }

        /// <param name="targetSpeed">How fast the look target moves (degrees/s); a moving target is never "resting".</param>
        public YawPitch Update(SaccadeSettings settings, float deltaTime, float targetSpeed)
        {
            float dt = Mathf.Max(0f, deltaTime);
            if (!settings.enabled)
            {
                target = default;
            }
            else
            {
                restingTime = targetSpeed <= settings.restingSpeed ? restingTime + dt : 0f;
                timer -= dt;
                if (timer <= 0f)
                {
                    Jump(settings);
                }
            }

            if (dt > 0f)
            {
                float time = Mathf.Max(settings.jumpTime, 1e-4f);
                offset.yaw = Mathf.SmoothDamp(offset.yaw, target.yaw, ref velocity.yaw, time, Mathf.Infinity, dt);
                offset.pitch = Mathf.SmoothDamp(offset.pitch, target.pitch, ref velocity.pitch, time, Mathf.Infinity, dt);
            }

            return offset;
        }

        void Jump(SaccadeSettings settings)
        {
            LastWasGlance = restingTime >= settings.idleTime && Next() < settings.idleGlanceChance;
            float amplitude = LastWasGlance
                ? settings.idleGlanceAmplitude
                : Mathf.Lerp(settings.minAmplitude, Mathf.Max(settings.minAmplitude, settings.maxAmplitude), Next());

            // Jumps are absolute offsets around the target, so the eyes always come back after a glance away.
            float angle = Next() * Mathf.PI * 2f;
            target = new YawPitch(Mathf.Cos(angle) * amplitude, Mathf.Sin(angle) * amplitude);
            if (LastWasGlance) restingTime = 0f;

            timer = Mathf.Lerp(settings.minInterval, Mathf.Max(settings.minInterval, settings.maxInterval), Next());
            JumpCount++;
        }

        float Next() => (float)random.NextDouble();
    }
}
