using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wobblefolk.Ragdoll;

namespace Wobblefolk.LookAt.Tests
{
    /// <summary>
    /// Play mode tests with real physics: a Wobblefolk Active Ragdoll turns its head towards a target through its
    /// joint motor while it keeps standing. Compiled only when the active ragdoll package is installed.
    /// </summary>
    public sealed class ActiveRagdollLookTests
    {
        readonly List<Object> created = new List<Object>();
        float savedFixedDeltaTime;

        [SetUp]
        public void SetUp()
        {
            savedFixedDeltaTime = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 0.01f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.Destroy(o);
            created.Clear();
            Time.fixedDeltaTime = savedFixedDeltaTime;
        }

        T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        ActiveRagdoll BuildRagdoll(out Transform leftEye, out Transform rightEye)
        {
            GameObject ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            var profiles = new RagdollProfileSet
            {
                ragdoll = Track(ScriptableObject.CreateInstance<RagdollProfile>()),
                balance = Track(ScriptableObject.CreateInstance<BalanceProfile>()),
                locomotion = Track(ScriptableObject.CreateInstance<LocomotionProfile>()),
                arms = Track(ScriptableObject.CreateInstance<ArmProfile>()),
            };
            ActiveRagdoll ragdoll = PrimitiveRagdollBuilder.Build(new PrimitiveBodyShape(), profiles, Vector3.zero, Quaternion.identity);
            Track(ragdoll.gameObject);

            Transform head = ragdoll.Head.transform;
            Transform Eye(float x)
            {
                var eye = new GameObject(x < 0f ? "LeftEye" : "RightEye").transform;
                eye.SetParent(head, false);
                eye.position = head.position + new Vector3(x, 0.15f, 0.1f);
                return eye;
            }

            leftEye = Eye(-0.045f);
            rightEye = Eye(0.045f);
            return ragdoll;
        }

        static float HorizontalYaw(Vector3 forward) =>
            Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.up);

        [UnityTest]
        public IEnumerator HeadTurnsTowardsTheTargetAndTheRagdollKeepsStanding()
        {
            ActiveRagdoll ragdoll = BuildRagdoll(out Transform leftEye, out Transform rightEye);
            Transform target = Track(new GameObject("Target")).transform;
            target.position = new Vector3(2f, 1.8f, 2f); // 45° to the right, about eye height

            var provider = ragdoll.gameObject.AddComponent<TransformLookTarget>();
            provider.target = target;
            var look = ragdoll.gameObject.AddComponent<ActiveRagdollLookAt>();
            look.Configure(null, provider, leftEye, rightEye);

            yield return new WaitForSeconds(4f);

            Assert.AreEqual(RagdollState.Normal, ragdoll.State, "the ragdoll keeps standing");
            Assert.AreEqual(0f, HorizontalYaw(ragdoll.Pelvis.transform.forward), 10f, "the body itself does not turn");

            float headYaw = HorizontalYaw(ragdoll.Head.transform.forward);
            Assert.That(headYaw, Is.InRange(20f, 48f), "the head turns physically towards the target");

            float eyeError = Vector3.Angle(leftEye.forward, target.position - leftEye.position);
            Assert.Less(eyeError, 3f, "the eyes cover the rest and stay on target");
        }

        [UnityTest]
        public IEnumerator LookFadesOutWhileFallen()
        {
            ActiveRagdoll ragdoll = BuildRagdoll(out Transform leftEye, out Transform rightEye);
            Transform target = Track(new GameObject("Target")).transform;
            target.position = new Vector3(-2f, 1.8f, 2f);

            var provider = ragdoll.gameObject.AddComponent<TransformLookTarget>();
            provider.target = target;
            var look = ragdoll.gameObject.AddComponent<ActiveRagdollLookAt>();
            look.Configure(null, provider, leftEye, rightEye);

            yield return new WaitForSeconds(1.5f);
            Assert.Greater(look.Controller.weight, 0.9f);

            ragdoll.GetComponent<BalanceController>().Fall();
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0f, look.Controller.weight, 1e-3f, "no head twisting on the floor");
        }
    }
}
