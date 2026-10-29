using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// Pure logic for blinking: random intervals, close / hold / open timing, occasional double blinks and blinks
    /// triggered by large gaze jumps. Produces a lid closedness from 0 (open) to 1 (closed). Deterministic for a seed.
    /// </summary>
    public sealed class BlinkTimer
    {
        const float NotBlinking = -1f;

        readonly System.Random random;
        float untilNext;
        float phase = NotBlinking;
        bool doublePending;
        bool isSecondOfDouble;

        public BlinkTimer(int seed)
        {
            random = new System.Random(seed);
            untilNext = 1f;
        }

        /// <summary>0 = open, 1 = closed.</summary>
        public float Closedness { get; private set; }

        public bool IsBlinking => phase >= 0f;
        public int BlinkCount { get; private set; }

        public void Reset(BlinkSettings settings)
        {
            phase = NotBlinking;
            Closedness = 0f;
            doublePending = false;
            isSecondOfDouble = false;
            BlinkCount = 0;
            untilNext = NextInterval(settings);
        }

        /// <summary>Starts a blink now, unless one is already running.</summary>
        public void Blink()
        {
            if (IsBlinking) return;
            phase = 0f;
            BlinkCount++;
        }

        /// <param name="gazeShift">How far the look target jumped this frame (degrees).</param>
        public float Update(BlinkSettings settings, float deltaTime, float gazeShift = 0f)
        {
            float dt = Mathf.Max(0f, deltaTime);
            if (!settings.enabled)
            {
                phase = NotBlinking;
                Closedness = 0f;
                return 0f;
            }

            if (!IsBlinking)
            {
                untilNext -= dt;
                bool shifted = settings.blinkOnGazeShift > 0f && gazeShift >= settings.blinkOnGazeShift;
                if (untilNext <= 0f || shifted)
                {
                    doublePending = !isSecondOfDouble && Next() < settings.doubleBlinkChance;
                    Blink();
                }
            }

            if (IsBlinking)
            {
                phase += dt;
                float close = settings.closeTime;
                float hold = settings.holdTime;
                float open = settings.openTime;

                if (phase < close) Closedness = phase / close;
                else if (phase < close + hold) Closedness = 1f;
                else Closedness = Mathf.Clamp01(1f - (phase - close - hold) / open);

                if (phase >= close + hold + open)
                {
                    phase = NotBlinking;
                    Closedness = 0f;
                    isSecondOfDouble = doublePending;
                    // A double blink follows after a gap as short as the opening; otherwise wait a random interval.
                    untilNext = doublePending ? open : NextInterval(settings);
                    doublePending = false;
                }
            }

            return Closedness;
        }

        float NextInterval(BlinkSettings settings) =>
            Mathf.Lerp(settings.minInterval, Mathf.Max(settings.minInterval, settings.maxInterval), Next());

        float Next() => (float)random.NextDouble();
    }
}
