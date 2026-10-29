using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.LookAt.Tests
{
    /// <summary>Checks the joint-space math of the JointLookDriver without running physics.</summary>
    public sealed class JointDriverTests
    {
        const float Dt = 1f / 60f;

        readonly List<Object> created = new List<Object>();
        LookProfile profile;
        GameObject character;
        Rigidbody chest;
        ConfigurableJoint headJoint;
        Transform target;
        LookAtController controller;
        JointLookDriver driver;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<LookProfile>();
            created.Add(profile);

            // The character is turned and the bones have odd axes, so frames and joint spaces all differ.
            character = new GameObject("Character");
            created.Add(character);
            character.transform.rotation = Quaternion.Euler(0f, 30f, 0f);

            var chestObject = new GameObject("Chest");
            chestObject.transform.SetParent(character.transform, false);
            chestObject.transform.SetPositionAndRotation(new Vector3(0f, 1.3f, 0f), Quaternion.Euler(15f, 70f, -20f));
            chest = chestObject.AddComponent<Rigidbody>();
            chest.isKinematic = true;

            var headObject = new GameObject("Head");
            headObject.transform.SetParent(chestObject.transform, false);
            headObject.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.Euler(-35f, 10f, 50f));
            headObject.AddComponent<Rigidbody>();
            headJoint = headObject.AddComponent<ConfigurableJoint>();
            headJoint.axis = new Vector3(0.3f, 0.9f, 0.1f);
            headJoint.secondaryAxis = new Vector3(0f, 0.2f, 1f);
            headJoint.connectedBody = chest;

            target = new GameObject("Target").transform;
            created.Add(target.gameObject);
            var provider = character.AddComponent<TransformLookTarget>();
            provider.target = target;

            driver = character.AddComponent<JointLookDriver>();
            driver.head = headJoint;

            controller = character.AddComponent<LookAtController>();
            controller.Configure(profile, provider, character.transform, headObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        /// <summary>The world rotation the head would reach if its joint motor fully achieved the target rotation.</summary>
        Quaternion ImpliedHeadRotation(Quaternion startRelative)
        {
            Vector3 right = headJoint.axis.normalized;
            Vector3 forward = Vector3.Cross(right, headJoint.secondaryAxis).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            Quaternion jointToLocal = Quaternion.LookRotation(forward, up);

            Quaternion jointSpace = Quaternion.Inverse(headJoint.targetRotation);
            Quaternion local = jointToLocal * jointSpace * Quaternion.Inverse(jointToLocal);
            return chest.rotation * startRelative * local;
        }

        [Test]
        public void TargetRotation_TurnsTheHeadByTheAccumulatedLook()
        {
            Quaternion restHead = headJoint.transform.rotation;
            Quaternion startRelative = Quaternion.Inverse(chest.rotation) * restHead;
            target.position = character.transform.TransformPoint(new Vector3(3f, 2.4f, 3f));

            for (int i = 0; i < 240; i++) controller.Step(Dt);

            // Neck, chest and torso have no joint here, so the head takes their share as well.
            Quaternion expected = LookDriverMath.WorldDelta(controller.Solver.GetAccumulated(LookSegment.Head), character.transform.rotation, 1f) * restHead;
            Assert.Less(Quaternion.Angle(expected, ImpliedHeadRotation(startRelative)), 0.05f);
            Assert.Greater(Quaternion.Angle(restHead, expected), 10f, "the head really turns");
        }

        [Test]
        public void TargetRotation_IsIdentityWithoutALook()
        {
            controller.weight = 0f;
            target.position = character.transform.TransformPoint(new Vector3(3f, 1.6f, 3f));
            for (int i = 0; i < 60; i++) controller.Step(Dt);

            Assert.Less(Quaternion.Angle(Quaternion.identity, headJoint.targetRotation), 0.01f);
        }

        [Test]
        public void TargetRotation_FollowsTheParentWhenItHasMoved()
        {
            target.position = character.transform.TransformPoint(new Vector3(-3f, 1.6f, 3f));
            for (int i = 0; i < 240; i++) controller.Step(Dt);
            Quaternion before = headJoint.targetRotation;

            // A wobbling chest does not change the target relative to it: the joint keeps the same relative goal.
            chest.transform.rotation = Quaternion.AngleAxis(12f, Vector3.right) * chest.transform.rotation;
            controller.Step(Dt);
            Assert.Less(Quaternion.Angle(before, headJoint.targetRotation), 0.5f);
        }

        [Test]
        public void DisablingTheDriver_ReleasesTheJoint()
        {
            target.position = character.transform.TransformPoint(new Vector3(3f, 1.6f, 3f));
            for (int i = 0; i < 120; i++) controller.Step(Dt);
            Assert.Greater(Quaternion.Angle(Quaternion.identity, headJoint.targetRotation), 5f);

            driver.ResetTargets();
            Assert.Less(Quaternion.Angle(Quaternion.identity, headJoint.targetRotation), 0.01f);
        }
    }
}
