using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.LookAt.Tests
{
    public sealed class LookMathTests
    {
        [Test]
        public void YawPitch_RoundTripsThroughDirection()
        {
            var random = new System.Random(1234);
            for (int i = 0; i < 200; i++)
            {
                var direction = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f,
                    (float)random.NextDouble() * 2f - 1f).normalized;
                if (direction == Vector3.zero) continue;

                Vector3 roundTrip = LookMath.ToDirection(LookMath.ToYawPitch(direction));
                Assert.Less(Vector3.Angle(direction, roundTrip), 0.01f, direction.ToString("F3"));
            }
        }

        [Test]
        public void ToRotation_TurnsForwardOntoDirection()
        {
            var angles = new YawPitch(37f, -21f);
            Vector3 rotated = LookMath.ToRotation(angles) * Vector3.forward;
            Assert.Less(Vector3.Angle(rotated, LookMath.ToDirection(angles)), 0.01f);
        }

        [Test]
        public void ToRotation_HasNoRoll()
        {
            Quaternion rotation = LookMath.ToRotation(new YawPitch(70f, 40f));
            Vector3 right = rotation * Vector3.right;
            Assert.AreEqual(0f, right.y, 1e-4f, "the right axis stays horizontal");
        }

        [Test]
        public void Signs_RightAndUpArePositive()
        {
            Assert.Greater(LookMath.ToYawPitch(new Vector3(1f, 0f, 1f)).yaw, 0f);
            Assert.Greater(LookMath.ToYawPitch(new Vector3(0f, 1f, 1f)).pitch, 0f);
        }

        [Test]
        public void VerticalDirection_KeepsFallbackYaw()
        {
            YawPitch result = LookMath.ToYawPitch(Vector3.down, new YawPitch(25f, 0f));
            Assert.AreEqual(25f, result.yaw);
            Assert.AreEqual(-90f, result.pitch, 1e-3f);
        }

        [Test]
        public void ZeroDirection_ReturnsFallback()
        {
            var fallback = new YawPitch(12f, 3f);
            Assert.AreEqual(fallback, LookMath.ToYawPitch(Vector3.zero, fallback));
        }

        [Test]
        public void History_InterpolatesAndClamps()
        {
            var history = new LookHistory(4);
            history.Push(0f, new YawPitch(0f, 0f));
            history.Push(1f, new YawPitch(10f, 20f));

            Assert.AreEqual(5f, history.Sample(0.5f).yaw, 1e-4f);
            Assert.AreEqual(10f, history.Sample(2f).yaw, "clamped to the newest");
            Assert.AreEqual(0f, history.Sample(-1f).yaw, "clamped to the oldest");

            for (int i = 2; i < 10; i++) history.Push(i, new YawPitch(i * 10f, 0f));
            Assert.AreEqual(4, history.Count, "capacity is fixed");
            Assert.AreEqual(60f, history.Sample(0f).yaw, "oldest surviving sample");
        }
    }
}
