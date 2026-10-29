using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// Fixed-size, allocation-free history of look goals with timestamps, sampled with linear interpolation. Lets each
    /// link of the chain react to where the target was a little while ago.
    /// </summary>
    public sealed class LookHistory
    {
        readonly float[] times;
        readonly YawPitch[] values;
        int newest = -1;
        int count;

        public LookHistory(int capacity)
        {
            capacity = Mathf.Max(2, capacity);
            times = new float[capacity];
            values = new YawPitch[capacity];
        }

        public int Count => count;
        public int Capacity => times.Length;

        public void Clear()
        {
            newest = -1;
            count = 0;
        }

        public void Push(float time, YawPitch value)
        {
            newest = (newest + 1) % times.Length;
            times[newest] = time;
            values[newest] = value;
            count = Mathf.Min(count + 1, times.Length);
        }

        /// <summary>Value at <paramref name="time"/>, clamped to the newest and oldest stored samples.</summary>
        public YawPitch Sample(float time)
        {
            if (count == 0)
            {
                return default;
            }

            int index = newest;
            if (time >= times[index])
            {
                return values[index];
            }

            // Walk back from the newest sample; delays are short, so this usually takes only a few steps.
            for (int step = 1; step < count; step++)
            {
                int older = (index - 1 + times.Length) % times.Length;
                if (time >= times[older])
                {
                    float span = times[index] - times[older];
                    float t = span > 0f ? (time - times[older]) / span : 1f;
                    return YawPitch.Lerp(values[older], values[index], t);
                }

                index = older;
            }

            return values[index];
        }
    }
}
