using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.LookAt.Tests
{
    public sealed class LookSolverTests
    {
        const float Dt = 1f / 60f;
        const float Tolerance = 0.05f;

        LookProfile profile;
        LookSolver solver;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<LookProfile>();
            solver = new LookSolver(profile);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        void Run(YawPitch target, float seconds, bool hasTarget = true)
        {
            int steps = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                solver.Update(target, hasTarget, Dt);
            }
        }

        static YawPitch Yaw(float yaw) => new YawPitch(yaw, 0f);

        float BodyYaw => solver.GetAccumulated(LookSegment.Head).yaw;

        // ---------------------------------------------------------------- distribution and limits

        [Test]
        public void SmallGlance_MovesOnlyEyesAndHead()
        {
            Run(Yaw(10f), 3f);

            Assert.AreEqual(0f, solver.GetLocal(LookSegment.Torso).yaw, Tolerance, "torso stays below its engage angle");
            Assert.AreEqual(0f, solver.GetLocal(LookSegment.UpperChest).yaw, Tolerance, "chest stays below its engage angle");
            Assert.AreEqual(0f, solver.GetLocal(LookSegment.Neck).yaw, Tolerance, "neck stays below its engage angle");
            Assert.AreEqual(3f, solver.GetLocal(LookSegment.Head).yaw, 0.1f, "head takes 60% of (10° - 5°)");
            Assert.AreEqual(10f, solver.Gaze.yaw, 0.1f, "eyes cover the rest");
        }

        [Test]
        public void ReachableTarget_EveryLinkWithinItsLimitAndGazeOnTarget()
        {
            Run(Yaw(80f), 4f);

            for (int i = 0; i < LookSegments.Count; i++)
            {
                var segment = (LookSegment)i;
                Assert.LessOrEqual(Mathf.Abs(solver.GetLocal(segment).yaw), profile.Get(segment).maxYaw + Tolerance, segment.ToString());
                Assert.GreaterOrEqual(solver.GetLocal(segment).yaw, -Tolerance, $"{segment} turns towards the target side");
            }

            Assert.AreEqual(80f, solver.Gaze.yaw, 0.2f);
            Assert.Less(solver.GazeError, 0.5f);
        }

        [Test]
        public void PitchLimits_AreAsymmetricAndRespected()
        {
            Run(new YawPitch(0f, 80f), 4f);
            for (int i = 0; i < LookSegments.Count; i++)
            {
                var segment = (LookSegment)i;
                Assert.LessOrEqual(solver.GetLocal(segment).pitch, profile.Get(segment).maxPitchUp + Tolerance, $"{segment} up");
            }

            solver.Reset();
            Run(new YawPitch(0f, -80f), 4f);
            for (int i = 0; i < LookSegments.Count; i++)
            {
                var segment = (LookSegment)i;
                Assert.GreaterOrEqual(solver.GetLocal(segment).pitch, -profile.Get(segment).maxPitchDown - Tolerance, $"{segment} down");
            }
        }

        // ---------------------------------------------------------------- accumulation along the spine

        [Test]
        public void BodyYaw_NeverExceedsTargetWhileTurning()
        {
            int steps = Mathf.CeilToInt(3f / Dt);
            for (int i = 0; i < steps; i++)
            {
                solver.Update(Yaw(80f), true, Dt);
                Assert.LessOrEqual(BodyYaw, 80f + Tolerance, $"frame {i}");
                Assert.GreaterOrEqual(BodyYaw, -Tolerance, $"frame {i}");
            }
        }

        [Test]
        public void BodyYaw_NeverExceedsBodyBudget()
        {
            profile.maxBodyYaw = 50f;
            profile.behind.enterAngle = 180f; // keep the target "in front"
            Run(Yaw(170f), 5f);

            Assert.LessOrEqual(Mathf.Abs(BodyYaw), 50f + Tolerance);
            Assert.AreEqual(50f + profile.eyes.maxYaw, solver.Gaze.yaw, 0.2f, "eyes still add their own range");
        }

        [Test]
        public void SummedLimitsCapTheTotalTurn()
        {
            profile.behind.enterAngle = 180f;
            profile.maxBodyYaw = 360f;
            Run(Yaw(179f), 5f);

            float limits = 0f;
            for (int i = 0; i < LookSegments.Count; i++)
            {
                limits += profile.Get((LookSegment)i).maxYaw;
            }

            Assert.LessOrEqual(solver.Gaze.yaw, limits + Tolerance);
        }

        // ---------------------------------------------------------------- targets behind

        [Test]
        public void TargetBehind_ClampToLimits_TurnsOnlyAsFarAsTheChainReaches()
        {
            profile.behind.behaviour = BehindBehaviour.ClampToLimits;
            Run(Yaw(170f), 5f);

            Assert.IsTrue(solver.IsBehind);
            Assert.AreEqual(profile.MaxReachYaw, solver.Gaze.yaw, 0.3f);
            Assert.LessOrEqual(Mathf.Abs(BodyYaw), profile.maxBodyYaw + Tolerance);
            Assert.AreEqual(170f - profile.MaxReachYaw, solver.GazeError, 0.5f);
        }

        [Test]
        public void TargetBehind_ReturnToNeutral_LooksStraightAhead()
        {
            profile.behind.behaviour = BehindBehaviour.ReturnToNeutral;
            Run(Yaw(60f), 3f);
            Run(Yaw(170f), 5f);

            Assert.IsTrue(solver.IsBehind);
            Assert.AreEqual(0f, solver.Gaze.yaw, 0.1f);
            Assert.AreEqual(0f, BodyYaw, 0.1f);
        }

        [Test]
        public void TargetBehind_UsesHysteresis()
        {
            Run(Yaw(120f), 0.5f);
            Assert.IsTrue(solver.IsBehind, "enters past the enter angle");

            Run(Yaw(108f), 0.5f);
            Assert.IsTrue(solver.IsBehind, "stays behind between exit and enter angle");

            Run(Yaw(95f), 0.5f);
            Assert.IsFalse(solver.IsBehind, "leaves below the exit angle");
        }

        [Test]
        public void TargetPassingStraightBehind_DoesNotFlipSides()
        {
            Run(Yaw(100f), 3f);
            Run(Yaw(170f), 2f);
            Assert.Greater(solver.Gaze.yaw, 0f);

            // The same target seen from the other side of 180°.
            Run(Yaw(-170f), 2f);
            Assert.Greater(solver.Gaze.yaw, 0f, "keeps looking over the right shoulder");
            Assert.Greater(BodyYaw, 0f);
        }

        // ---------------------------------------------------------------- timing

        [Test]
        public void StepTarget_EyesLeadHeadFollowsBodyLast()
        {
            const float target = 60f;
            float FinalShare(LookSegment s)
            {
                var reference = new LookSolver(profile);
                for (int i = 0; i < 600; i++) reference.Update(Yaw(target), true, Dt);
                return reference.GetLocal(s).yaw;
            }

            float headFinal = FinalShare(LookSegment.Head);
            float neckFinal = FinalShare(LookSegment.Neck);

            Run(Yaw(target), 0.1f);
            float gazeProgress = solver.Gaze.yaw / target;
            float headProgress = solver.GetLocal(LookSegment.Head).yaw / headFinal;
            float neckProgress = solver.GetLocal(LookSegment.Neck).yaw / neckFinal;

            Assert.Greater(gazeProgress, headProgress, "gaze (eyes) leads the head");
            Assert.Greater(headProgress, neckProgress, "head leads the neck");
            Assert.Greater(neckProgress, 0f, "neck has started");
            Assert.AreEqual(0f, solver.GetLocal(LookSegment.UpperChest).yaw, Tolerance, "chest still within its reaction delay");
            Assert.AreEqual(0f, solver.GetLocal(LookSegment.Torso).yaw, Tolerance, "torso still within its reaction delay");

            Run(Yaw(target), 0.12f);
            Assert.Greater(solver.GetLocal(LookSegment.UpperChest).yaw, 0f, "chest has started");
            Assert.AreEqual(0f, solver.GetLocal(LookSegment.Torso).yaw, Tolerance, "torso reacts last");
        }

        [Test]
        public void EyesLeadThenDriftBackAsTheBodyCatchesUp()
        {
            Run(Yaw(60f), 0.15f);
            float earlyEyes = solver.GetLocal(LookSegment.Eyes).yaw;

            Run(Yaw(60f), 3f);
            float lateEyes = solver.GetLocal(LookSegment.Eyes).yaw;

            Assert.Greater(earlyEyes, lateEyes + 5f);
            Assert.AreEqual(60f, solver.Gaze.yaw, 0.2f, "the gaze stays on target meanwhile");
        }

        [Test]
        public void NoSnap_OnFirstFrame()
        {
            solver.Update(Yaw(60f), true, Dt);

            Assert.Less(solver.GetLocal(LookSegment.Head).yaw, 2f);
            Assert.Less(solver.Gaze.yaw, 60f);
        }

        [Test]
        public void MaxSpeed_LimitsAngularSpeed()
        {
            profile.eyes.maxSpeed = 120f;
            profile.eyes.smoothTime = 0.01f;
            Run(Yaw(30f), 0.1f);

            Assert.LessOrEqual(solver.GetLocal(LookSegment.Eyes).yaw, 120f * solver.Time + 0.5f);
        }

        [Test]
        public void LosingTheTarget_ReturnsToNeutral()
        {
            Run(Yaw(70f), 3f);
            Run(Yaw(70f), 5f, hasTarget: false);

            Assert.AreEqual(0f, solver.Gaze.yaw, 0.1f);
            Assert.AreEqual(0f, solver.GazeError, Tolerance, "no error without a target");
        }

        [Test]
        public void StraightUpTarget_StaysFinite()
        {
            for (int i = 0; i < 120; i++)
            {
                solver.Update(Vector3.up, true, Dt);
            }

            YawPitch gaze = solver.Gaze;
            Assert.IsFalse(float.IsNaN(gaze.yaw) || float.IsNaN(gaze.pitch));
            Assert.Greater(gaze.pitch, 0f);
        }

        [Test]
        public void ZeroDeltaTime_DoesNotMove()
        {
            solver.Update(Yaw(60f), true, 0f);
            Assert.AreEqual(0f, solver.Gaze.yaw);
        }
    }
}
