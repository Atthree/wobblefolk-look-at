using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.LookAt.Tests
{
    public sealed class EyeTests
    {
        const float Dt = 1f / 60f;

        // ---------------------------------------------------------------- saccades

        [Test]
        public void Saccades_StayWithinAmplitudeAndInterval()
        {
            var settings = new SaccadeSettings { idleGlanceChance = 0f };
            var generator = new SaccadeGenerator(42);
            float maxOffset = 0f;

            for (int i = 0; i < 60 * 30; i++)
            {
                YawPitch offset = generator.Update(settings, Dt, 100f);
                maxOffset = Mathf.Max(maxOffset, Mathf.Sqrt(offset.yaw * offset.yaw + offset.pitch * offset.pitch));
            }

            Assert.LessOrEqual(maxOffset, settings.maxAmplitude + 0.01f);
            Assert.GreaterOrEqual(generator.JumpCount, Mathf.FloorToInt(30f / settings.maxInterval));
            Assert.LessOrEqual(generator.JumpCount, Mathf.CeilToInt(30f / settings.minInterval) + 1);
        }

        [Test]
        public void Saccades_GlanceAwayOnlyAfterResting()
        {
            var settings = new SaccadeSettings { idleGlanceChance = 1f, idleTime = 1f };
            var moving = new SaccadeGenerator(1);
            for (int i = 0; i < 60 * 10; i++)
            {
                moving.Update(settings, Dt, 100f);
                Assert.IsFalse(moving.LastWasGlance, "a moving target never rests");
            }

            var resting = new SaccadeGenerator(1);
            bool glanced = false;
            for (int i = 0; i < 60 * 10; i++)
            {
                resting.Update(settings, Dt, 0f);
                glanced |= resting.LastWasGlance;
            }

            Assert.IsTrue(glanced);
        }

        [Test]
        public void Saccades_DisabledDecayToZero()
        {
            var settings = new SaccadeSettings();
            var generator = new SaccadeGenerator(3);
            for (int i = 0; i < 120; i++) generator.Update(settings, Dt, 0f);

            settings.enabled = false;
            for (int i = 0; i < 60; i++) generator.Update(settings, Dt, 0f);
            Assert.AreEqual(0f, generator.Offset.yaw, 1e-3f);
            Assert.AreEqual(0f, generator.Offset.pitch, 1e-3f);
        }

        [Test]
        public void Saccades_AreDeterministicForASeed()
        {
            var settings = new SaccadeSettings();
            var a = new SaccadeGenerator(99);
            var b = new SaccadeGenerator(99);
            for (int i = 0; i < 300; i++)
            {
                Assert.AreEqual(a.Update(settings, Dt, 0f), b.Update(settings, Dt, 0f));
            }
        }

        // ---------------------------------------------------------------- blinking

        [Test]
        public void Blinks_HappenAtTheConfiguredRate()
        {
            var settings = new BlinkSettings { doubleBlinkChance = 0f, blinkOnGazeShift = 0f };
            var timer = new BlinkTimer(7);
            timer.Reset(settings);

            for (int i = 0; i < 60 * 60; i++)
            {
                float closed = timer.Update(settings, Dt);
                Assert.That(closed, Is.InRange(0f, 1f));
            }

            float blinkLength = settings.closeTime + settings.holdTime + settings.openTime;
            Assert.GreaterOrEqual(timer.BlinkCount, Mathf.FloorToInt(60f / (settings.maxInterval + blinkLength)));
            Assert.LessOrEqual(timer.BlinkCount, Mathf.CeilToInt(60f / settings.minInterval));
        }

        [Test]
        public void Blinks_ClosesFullyMidBlink()
        {
            var settings = new BlinkSettings();
            var timer = new BlinkTimer(1);
            timer.Reset(settings);
            timer.Blink();

            float peak = 0f;
            for (int i = 0; i < 30; i++) peak = Mathf.Max(peak, timer.Update(settings, 0.005f));
            Assert.AreEqual(1f, peak, 1e-3f);

            for (int i = 0; i < 60; i++) timer.Update(settings, 0.005f);
            Assert.AreEqual(0f, timer.Closedness, "open again afterwards");
        }

        [Test]
        public void Blinks_LargeGazeShiftTriggersABlink()
        {
            var settings = new BlinkSettings { minInterval = 100f, maxInterval = 100f };
            var timer = new BlinkTimer(1);
            timer.Reset(settings);

            timer.Update(settings, Dt, settings.blinkOnGazeShift * 0.5f);
            Assert.IsFalse(timer.IsBlinking, "small shifts do not blink");

            timer.Update(settings, Dt, settings.blinkOnGazeShift + 1f);
            Assert.IsTrue(timer.IsBlinking);
        }

        [Test]
        public void Blinks_DoubleBlinkFollowsQuickly()
        {
            var settings = new BlinkSettings { doubleBlinkChance = 1f, minInterval = 100f, maxInterval = 100f, blinkOnGazeShift = 10f };
            var timer = new BlinkTimer(1);
            timer.Reset(settings);
            timer.Update(settings, Dt, 20f); // a gaze jump starts the blink; the double is rolled there

            for (int i = 0; i < 60; i++) timer.Update(settings, Dt);
            Assert.AreEqual(2, timer.BlinkCount, "a second blink within a second");

            for (int i = 0; i < 120; i++) timer.Update(settings, Dt);
            Assert.AreEqual(2, timer.BlinkCount, "no endless chain of doubles");
        }

        [Test]
        public void Blinks_Disabled_StayOpen()
        {
            var settings = new BlinkSettings { enabled = false };
            var timer = new BlinkTimer(1);
            timer.Blink();
            Assert.AreEqual(0f, timer.Update(settings, Dt));
        }

        // ---------------------------------------------------------------- eyes on a character

        sealed class Rig
        {
            public GameObject root;
            public Transform head, leftEye, rightEye, target;
            public LookAtController controller;
            public TransformLookDriver driver;
            public LookProfile profile;
        }

        readonly List<Object> created = new List<Object>();

        Rig CreateRig()
        {
            var rig = new Rig { profile = ScriptableObject.CreateInstance<LookProfile>() };
            created.Add(rig.profile);
            rig.root = new GameObject("Character");
            created.Add(rig.root);

            rig.head = new GameObject("Head").transform;
            rig.head.SetParent(rig.root.transform, false);
            rig.head.position = new Vector3(0f, 1.7f, 0f);
            rig.leftEye = new GameObject("LeftEye").transform;
            rig.leftEye.SetParent(rig.head, false);
            rig.leftEye.position = new Vector3(-0.035f, 1.75f, 0.09f);
            rig.rightEye = new GameObject("RightEye").transform;
            rig.rightEye.SetParent(rig.head, false);
            rig.rightEye.position = new Vector3(0.035f, 1.75f, 0.09f);

            rig.target = new GameObject("Target").transform;
            created.Add(rig.target.gameObject);
            var provider = rig.root.AddComponent<TransformLookTarget>();
            provider.target = rig.target;

            rig.driver = rig.root.AddComponent<TransformLookDriver>();
            rig.driver.head = rig.head;
            rig.driver.leftEye = rig.leftEye;
            rig.driver.rightEye = rig.rightEye;

            var origin = new GameObject("Origin").transform;
            origin.SetParent(rig.head, false);
            origin.position = new Vector3(0f, 1.75f, 0.09f);

            rig.controller = rig.root.AddComponent<LookAtController>();
            rig.controller.Configure(rig.profile, provider, rig.root.transform, origin);
            return rig;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        /// <summary>Only the eyes move: every body link takes no share.</summary>
        static void StillBody(LookProfile profile)
        {
            profile.torso.weight = 0f;
            profile.upperChest.weight = 0f;
            profile.neck.weight = 0f;
            profile.head.weight = 0f;
        }

        static void Run(Rig rig, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) rig.controller.Step(Dt);
        }

        static float EyeYaw(Transform eye) => Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(eye.forward, Vector3.up), Vector3.up);

        [Test]
        public void NearTarget_EyesConverge()
        {
            Rig rig = CreateRig();
            rig.target.position = new Vector3(0f, 1.75f, 0.35f);
            Run(rig, 2f);

            Assert.Greater(EyeYaw(rig.leftEye), 2f, "left eye turns right, towards the nose");
            Assert.Less(EyeYaw(rig.rightEye), -2f, "right eye turns left, towards the nose");
            Assert.Less(Vector3.Angle(rig.leftEye.forward, rig.target.position - rig.leftEye.position), 1f);
        }

        [Test]
        public void NoConvergence_EyesStayParallel()
        {
            Rig rig = CreateRig();
            rig.profile.eyeDetails.convergence = 0f;
            rig.target.position = new Vector3(0f, 1.75f, 0.35f);
            Run(rig, 2f);

            Assert.Less(Vector3.Angle(rig.leftEye.forward, rig.rightEye.forward), 0.01f);
        }

        [Test]
        public void InnerLimit_ClampsTheEyeTurningTowardsTheNose()
        {
            Rig rig = CreateRig();
            rig.profile.eyeDetails.maxInnerYaw = 5f;
            StillBody(rig.profile);
            rig.target.position = new Vector3(3f, 1.75f, 3f);
            Run(rig, 2f);

            Assert.LessOrEqual(EyeYaw(rig.leftEye), 5f + 0.1f, "left eye looking right is its inner direction");
            Assert.Greater(EyeYaw(rig.rightEye), 20f, "right eye looking right is its outer direction");
        }

        [Test]
        public void EyeLod_SkipsEyesWhenTheCameraIsFar()
        {
            Rig rig = CreateRig();
            Camera cam = new GameObject("Camera").AddComponent<Camera>();
            created.Add(cam.gameObject);
            cam.transform.position = new Vector3(0f, 1.7f, 50f);
            rig.controller.lodCamera = cam;
            rig.controller.eyeLodDistance = 10f;
            StillBody(rig.profile);

            Quaternion restRelative = Quaternion.Inverse(rig.head.rotation) * rig.leftEye.rotation;
            rig.target.position = new Vector3(3f, 1.75f, 3f);
            Run(rig, 1f);

            Assert.IsFalse(rig.controller.EyesActive);
            Assert.Less(Quaternion.Angle(restRelative, Quaternion.Inverse(rig.head.rotation) * rig.leftEye.rotation), 0.01f);

            cam.transform.position = new Vector3(0f, 1.7f, 3f);
            Run(rig, 1f);
            Assert.IsTrue(rig.controller.EyesActive);
            Assert.Greater(EyeYaw(rig.leftEye), 10f);
        }

        [Test]
        public void Saccades_MoveTheEyesSlightlyAroundTheTarget()
        {
            Rig rig = CreateRig();
            rig.root.AddComponent<EyeBehaviour>();
            rig.target.position = new Vector3(0f, 1.75f, 5f);

            float maxError = 0f;
            for (float t = 0f; t < 4f; t += Dt)
            {
                rig.controller.Step(Dt);
                if (t > 1f) maxError = Mathf.Max(maxError, Vector3.Angle(rig.leftEye.forward, rig.target.position - rig.leftEye.position));
            }

            Assert.Greater(maxError, 0.2f, "the eyes do move");
            Assert.Less(maxError, rig.profile.saccades.idleGlanceAmplitude + 1f, "but stay within the glance amplitude");
        }

        [Test]
        public void ScaleLids_CloseDuringABlink()
        {
            Rig rig = CreateRig();
            var lid = new GameObject("Lid").transform;
            lid.SetParent(rig.head, false);
            var eyes = rig.root.AddComponent<EyeBehaviour>();
            eyes.eyelidMode = EyelidMode.Scale;
            eyes.lids = new[] { lid };

            rig.controller.Step(Dt);
            Assert.AreEqual(eyes.openScale, lid.localScale);

            eyes.Blink();
            float peak = 0f;
            for (int i = 0; i < 12; i++)
            {
                rig.controller.Step(1f / 120f);
                peak = Mathf.Max(peak, lid.localScale.y);
            }

            Assert.AreEqual(eyes.closedScale.y, peak, 0.01f);
        }
    }
}
