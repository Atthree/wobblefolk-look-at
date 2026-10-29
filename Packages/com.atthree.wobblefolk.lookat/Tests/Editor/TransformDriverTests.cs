using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.LookAt.Tests
{
    /// <summary>End-to-end tests of LookAtController + TransformLookDriver on a primitive skeleton with odd bone axes.</summary>
    public sealed class TransformDriverTests
    {
        const float Dt = 1f / 60f;

        readonly List<Object> created = new List<Object>();
        LookProfile profile;
        GameObject character;
        Transform torso, chest, neck, head, leftEye, rightEye, target;
        TransformLookDriver driver;
        LookAtController controller;
        TransformLookTarget provider;

        Transform Bone(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);
            return t;
        }

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<LookProfile>();
            created.Add(profile);

            character = new GameObject("Character");
            created.Add(character);
            Transform root = character.transform;

            // Deliberately inconsistent bone axes, like many rigs.
            torso = Bone("Torso", root, new Vector3(0, 1.0f, 0), Quaternion.Euler(10, 20, 30));
            chest = Bone("Chest", torso, new Vector3(0, 1.3f, 0), Quaternion.Euler(-40, 5, 70));
            neck = Bone("Neck", chest, new Vector3(0, 1.5f, 0), Quaternion.Euler(90, 0, 0));
            head = Bone("Head", neck, new Vector3(0, 1.65f, 0), Quaternion.Euler(0, -90, 15));
            // Eyes: +Z is their look axis in the rest pose.
            leftEye = Bone("LeftEye", head, new Vector3(-0.04f, 1.7f, 0.1f), Quaternion.identity);
            rightEye = Bone("RightEye", head, new Vector3(0.04f, 1.7f, 0.1f), Quaternion.identity);

            target = new GameObject("Target").transform;
            created.Add(target.gameObject);

            provider = character.AddComponent<TransformLookTarget>();
            provider.target = target;

            driver = character.AddComponent<TransformLookDriver>();
            driver.torso = torso;
            driver.upperChest = chest;
            driver.neck = neck;
            driver.head = head;
            driver.leftEye = leftEye;
            driver.rightEye = rightEye;

            controller = character.AddComponent<LookAtController>();
            controller.Configure(profile, provider, root, head);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        void Run(float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) controller.Step(Dt);
        }

        float EyeError(Transform eye) => Vector3.Angle(eye.forward, target.position - eye.position);

        [Test]
        public void EyesEndUpOnTheTarget()
        {
            target.position = new Vector3(8f, 3f, 8f);
            Run(4f);

            Assert.Less(EyeError(leftEye), 1f);
            Assert.Less(EyeError(rightEye), 1f);
            Assert.IsTrue(controller.IsOnTarget);
        }

        [Test]
        public void BodyTurnsByItsShareOnly()
        {
            target.position = new Vector3(10f, 1.65f, 0.01f); // ~90° to the right
            Quaternion torsoRest = torso.rotation;
            Run(4f);

            float expected = controller.Solver.GetAccumulated(LookSegment.Torso).yaw;
            // Measure the torso's turn about the vertical through a horizontal reference axis.
            Vector3 restAxis = Vector3.ProjectOnPlane(torsoRest * Vector3.right, Vector3.up);
            Vector3 nowAxis = Vector3.ProjectOnPlane(torso.rotation * Vector3.right, Vector3.up);
            float actual = Vector3.SignedAngle(restAxis, nowAxis, Vector3.up);

            Assert.Greater(expected, 1f, "the torso takes part at 90°");
            Assert.AreEqual(expected, actual, 0.5f);
        }

        [Test]
        public void WithoutAnimator_OffsetsDoNotPileUp()
        {
            target.position = new Vector3(5f, 2f, 5f);
            Run(4f);
            Quaternion settled = head.rotation;

            Run(2f);
            Assert.Less(Quaternion.Angle(settled, head.rotation), 0.01f, "a steady target gives a steady pose");
        }

        [Test]
        public void ReturnsToRestPoseWithoutTarget()
        {
            Quaternion headRest = head.rotation;
            Quaternion eyeRest = leftEye.rotation;

            target.position = new Vector3(5f, 2f, 5f);
            Run(3f);
            provider.target = null;
            Run(5f);

            Assert.Less(Quaternion.Angle(headRest, head.rotation), 0.1f);
            Assert.Less(Quaternion.Angle(eyeRest, leftEye.rotation), 0.1f);
        }

        [Test]
        public void WithAnimator_UsesTheAnimatedPoseAsBase()
        {
            // Simulate an Animator writing the same pose every frame before LateUpdate.
            Quaternion animatedNeck = neck.localRotation;
            target.position = new Vector3(5f, 2f, 5f);
            for (int i = 0; i < 240; i++)
            {
                neck.localRotation = animatedNeck;
                controller.Step(Dt);
            }

            Assert.Less(EyeError(leftEye), 1f, "no double application on animated bones");
        }

        [Test]
        public void ZeroWeight_LeavesThePoseUntouched()
        {
            Quaternion headRest = head.rotation;
            controller.weight = 0f;
            target.position = new Vector3(5f, 2f, 5f);
            Run(2f);

            Assert.Less(Quaternion.Angle(headRest, head.rotation), 0.01f);
        }

        [Test]
        public void MissingBones_NextBoneTakesTheirShare()
        {
            driver.upperChest = null;
            driver.neck = null;
            target.position = new Vector3(8f, 1.65f, 4f);
            Run(4f);

            Assert.Less(EyeError(leftEye), 1f);
        }

        [Test]
        public void LookAtOverride_WinsUntilItsTransformDisappears()
        {
            target.position = new Vector3(-6f, 1.65f, 6f);
            var other = new GameObject("Other").transform;
            other.position = new Vector3(6f, 1.65f, 6f);

            controller.LookAt(other);
            Run(3f);
            Assert.Less(Vector3.Angle(leftEye.forward, other.position - leftEye.position), 1.5f);
            Assert.IsTrue(controller.HasOverride);

            Object.DestroyImmediate(other.gameObject);
            Run(3f);
            Assert.IsFalse(controller.HasOverride);
            Assert.Less(EyeError(leftEye), 1.5f, "back on the provider's target");
        }

        [Test]
        public void Events_FireOnAcquireAndLoss()
        {
            int acquired = 0, lost = 0;
            controller.Events.lookedAtTarget.AddListener(() => acquired++);
            controller.Events.lostTarget.AddListener(() => lost++);

            target.position = new Vector3(4f, 2f, 6f);
            Run(3f);
            Assert.AreEqual(1, acquired);

            target.position = new Vector3(-6f, 2f, 4f);
            controller.Step(Dt);
            Assert.AreEqual(1, lost, "a jump of the target loses it");

            Run(3f);
            Assert.AreEqual(2, acquired);
        }
    }
}
