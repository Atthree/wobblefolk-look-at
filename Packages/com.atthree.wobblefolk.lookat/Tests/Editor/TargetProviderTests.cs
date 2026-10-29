using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wobblefolk.LookAt.Tests
{
    public sealed class TargetProviderTests
    {
        sealed class FakePoint : IInterestPoint
        {
            public Vector3 LookPosition { get; set; }
            public float Priority { get; set; } = 1f;
            public float Radius { get; set; } = 10f;
            public bool IsAvailable { get; set; } = true;
        }

        readonly List<GameObject> created = new List<GameObject>();
        static readonly LookContext Context = new LookContext(Vector3.zero, Vector3.forward, null);

        GameObject Create(string name, PrimitiveType? primitive = null)
        {
            GameObject go = primitive.HasValue ? GameObject.CreatePrimitive(primitive.Value) : new GameObject(name);
            go.name = name;
            created.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            created.Clear();
        }

        // ---------------------------------------------------------------- interest selection

        [Test]
        public void Score_IsZeroBeyondRadiusOutsideViewOrUnavailable()
        {
            var settings = new InterestSettings();
            Assert.AreEqual(0f, InterestSelector.Score(new FakePoint { LookPosition = new Vector3(0, 0, 20) }, Vector3.zero, Vector3.forward, settings), "beyond radius");
            Assert.AreEqual(0f, InterestSelector.Score(new FakePoint { LookPosition = new Vector3(0, 0, -3) }, Vector3.zero, Vector3.forward, settings), "behind");
            Assert.AreEqual(0f, InterestSelector.Score(new FakePoint { LookPosition = new Vector3(0, 0, 3), IsAvailable = false }, Vector3.zero, Vector3.forward, settings), "unavailable");
            Assert.Greater(InterestSelector.Score(new FakePoint { LookPosition = new Vector3(0, 0, 3) }, Vector3.zero, Vector3.forward, settings), 0f);
        }

        [Test]
        public void Select_PrefersCloserAndMoreCentralPoints()
        {
            var near = new FakePoint { LookPosition = new Vector3(0, 0, 2) };
            var far = new FakePoint { LookPosition = new Vector3(0, 0, 8) };
            var side = new FakePoint { LookPosition = new Vector3(2, 0, 0.5f) };
            var points = new List<IInterestPoint> { far, side, near };

            Assert.AreSame(near, InterestSelector.Select(points, Vector3.zero, Vector3.forward, new InterestSettings(), null, out _));
        }

        [Test]
        public void Select_PriorityBeatsProximity()
        {
            var near = new FakePoint { LookPosition = new Vector3(0, 0, 2) };
            var important = new FakePoint { LookPosition = new Vector3(0, 0, 6), Priority = 5f };
            var points = new List<IInterestPoint> { near, important };

            Assert.AreSame(important, InterestSelector.Select(points, Vector3.zero, Vector3.forward, new InterestSettings(), null, out _));
        }

        [Test]
        public void Select_StickinessKeepsTheCurrentPointAgainstASlightlyBetterOne()
        {
            var current = new FakePoint { LookPosition = new Vector3(0, 0, 3.2f) };
            var slightlyBetter = new FakePoint { LookPosition = new Vector3(0, 0, 3f) };
            var points = new List<IInterestPoint> { slightlyBetter, current };
            var settings = new InterestSettings();

            Assert.AreSame(slightlyBetter, InterestSelector.Select(points, Vector3.zero, Vector3.forward, settings, null, out _));
            Assert.AreSame(current, InterestSelector.Select(points, Vector3.zero, Vector3.forward, settings, current, out _));
        }

        [Test]
        public void Select_ReturnsNullBelowMinimumScore()
        {
            var faint = new FakePoint { LookPosition = new Vector3(0, 0, 9.9f) };
            var settings = new InterestSettings { minScore = 0.2f };
            Assert.IsNull(InterestSelector.Select(new List<IInterestPoint> { faint }, Vector3.zero, Vector3.forward, settings, null, out _));
        }

        // ---------------------------------------------------------------- providers

        [Test]
        public void TransformTarget_AppliesOffsetAndIgnoresInactiveTargets()
        {
            GameObject target = Create("Target");
            target.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0));
            var provider = Create("Provider").AddComponent<TransformLookTarget>();
            provider.target = target.transform;
            provider.offset = new Vector3(0, 0, 1);

            provider.localOffset = true;
            Assert.IsTrue(provider.TryGetTarget(Context, out Vector3 local));
            Assert.That(Vector3.Distance(local, new Vector3(2, 2, 3)), Is.LessThan(1e-4f), "local offset is rotated with the target");

            provider.localOffset = false;
            provider.TryGetTarget(Context, out Vector3 world);
            Assert.That(Vector3.Distance(world, new Vector3(1, 2, 4)), Is.LessThan(1e-4f));

            target.SetActive(false);
            Assert.IsFalse(provider.TryGetTarget(Context, out _));
            provider.target = null;
            Assert.IsFalse(provider.TryGetTarget(Context, out _));
        }

        [Test]
        public void ResolveRay_SkipsOwnCollidersAndNearHits()
        {
            GameObject character = Create("Character");
            GameObject body = Create("Body", PrimitiveType.Cube);
            body.transform.SetParent(character.transform);
            body.transform.position = new Vector3(0, 0, 2);
            GameObject wall = Create("Wall", PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 0, 5);
            Physics.SyncTransforms();

            var buffer = new RaycastHit[8];
            var ray = new Ray(Vector3.zero, Vector3.forward);

            Assert.IsTrue(RayLookTarget.ResolveRay(ray, 50f, 0f, ~0, null, buffer, out Vector3 first));
            Assert.AreEqual(1.5f, first.z, 1e-3f, "without an ignore root the character's own body is hit");

            Assert.IsTrue(RayLookTarget.ResolveRay(ray, 50f, 0f, ~0, character.transform, buffer, out Vector3 skipped));
            Assert.AreEqual(4.5f, skipped.z, 1e-3f, "own colliders are skipped");

            Assert.IsTrue(RayLookTarget.ResolveRay(ray, 50f, 2f, ~0, null, buffer, out Vector3 far));
            Assert.AreEqual(4.5f, far.z, 1e-3f, "hits closer than the minimum distance are skipped");

            Assert.IsFalse(RayLookTarget.ResolveRay(new Ray(Vector3.zero, Vector3.back), 50f, 0f, ~0, null, buffer, out _));
        }

        [Test]
        public void CameraForwardTarget_HitsSurfaceOrFallsBackAlongTheRay()
        {
            Camera cam = Create("Camera").AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var provider = Create("Provider").AddComponent<CameraForwardTarget>();
            provider.Camera = cam;
            provider.fallbackDistance = 20f;

            Assert.IsTrue(provider.TryGetTarget(Context, out Vector3 empty));
            Assert.IsFalse(provider.LastHitSurface);
            Assert.AreEqual(20f, empty.z, 1e-3f);

            GameObject wall = Create("Wall", PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 0, 6);
            Physics.SyncTransforms();

            provider.TryGetTarget(Context, out Vector3 hit);
            Assert.IsTrue(provider.LastHitSurface);
            Assert.AreEqual(5.5f, hit.z, 1e-3f);

            provider.raycast = false;
            provider.TryGetTarget(Context, out Vector3 noRay);
            Assert.AreEqual(20f, noRay.z, 1e-3f, "raycast off always uses the fallback distance");
        }

        [Test]
        public void DisabledProvider_HasNoTarget()
        {
            var provider = Create("Provider").AddComponent<TransformLookTarget>();
            provider.target = Create("Target").transform;
            provider.enabled = false;
            Assert.IsFalse(provider.TryGetTarget(Context, out _));
        }

        [Test]
        public void InterestTarget_PullsTowardsThePointAndFallsBackWithoutOne()
        {
            var fallback = Create("Fallback").AddComponent<TransformLookTarget>();
            fallback.target = Create("FallbackTarget").transform;
            fallback.target.position = new Vector3(0, 0, 4);

            var interest = Create("Interest").AddComponent<InterestLookTarget>();
            interest.fallback = fallback;
            interest.reevaluateInterval = 0f;

            Assert.IsTrue(interest.TryGetTarget(Context, out Vector3 none));
            Assert.That(Vector3.Distance(none, new Vector3(0, 0, 4)), Is.LessThan(1e-4f), "no interest point: fallback target");

            var point = Create("Point").AddComponent<LookInterestPoint>();
            point.transform.position = new Vector3(2, 0, 2); // 45° to the right, well inside the 6 m radius
            InterestPointRegistry.Register(point);
            try
            {
                interest.pull = 1f;
                interest.TryGetTarget(Context, out Vector3 full);
                Assert.That(Vector3.Distance(full, point.transform.position), Is.LessThan(1e-4f), "full pull looks at the point");

                interest.pull = 0.5f;
                interest.TryGetTarget(Context, out Vector3 half);
                Assert.AreEqual(22.5f, Vector3.Angle(Vector3.forward, half), 0.1f, "half pull turns half way by angle");
                Assert.AreSame(point, interest.Current);
            }
            finally
            {
                InterestPointRegistry.Unregister(point);
            }
        }
    }
}
