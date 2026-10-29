using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Wobblefolk.LookAt
{
    [Serializable]
    public sealed class LookEvents
    {
        /// <summary>The gaze settled on the current target.</summary>
        public UnityEvent lookedAtTarget = new UnityEvent();
        /// <summary>The gaze left the target, or the target disappeared.</summary>
        public UnityEvent lostTarget = new UnityEvent();
    }

    /// <summary>
    /// Runs the look system for one character. Every LateUpdate (after cameras and animation) it picks the target —
    /// a temporary <see cref="LookAt(Transform, float)"/> override, else the target provider — converts it into the
    /// character's frame, advances the <see cref="LookSolver"/> and hands the result to every <see cref="ILookDriver"/>
    /// on this object.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    [AddComponentMenu("Wobblefolk/Look At/Look At Controller")]
    public sealed class LookAtController : MonoBehaviour
    {
        [SerializeField] LookProfile profile;
        [Tooltip("Where to look when there is no override. Empty = no target (look ahead).")]
        [SerializeField] LookTargetProvider targetProvider;
        [Tooltip("Transform whose rotation is the character's reference frame. Empty = this transform.")]
        [SerializeField] Transform characterRoot;
        [Tooltip("Point the look is measured from, usually between the eyes or the head. Empty = the character root.")]
        [SerializeField] Transform lookOrigin;

        [Tooltip("Blend with the unmodified pose: 0 = no look at all, 1 = full look.")]
        [Range(0f, 1f)] public float weight = 1f;

        [Header("Eyes")]
        [Tooltip("Saccades and blinking. Empty = looked up on this object; none = eyes without them.")]
        [SerializeField] EyeBehaviour eyeBehaviour;
        [Tooltip("Eyes, saccades and blinks are skipped beyond this distance from the LOD camera (m). 0 = never skipped.")]
        [Min(0f)] public float eyeLodDistance = 0f;
        [Tooltip("Camera for the eye LOD distance. Empty = the main camera.")]
        public Camera lodCamera;

        [Header("Events")]
        [Tooltip("The gaze counts as on target within this angle (degrees).")]
        [Range(0f, 45f)] public float onTargetAngle = 4f;
        [Tooltip("The gaze counts as lost beyond this angle (degrees). Larger than the on-target angle to avoid flicker.")]
        [Range(0f, 90f)] public float lostTargetAngle = 12f;
        [SerializeField] LookEvents events = new LookEvents();

        readonly List<ILookDriver> drivers = new List<ILookDriver>();
        LookSolver solver;
        Transform overrideTransform;
        Vector3 overridePoint;
        float overrideUntil = float.NegativeInfinity;
        bool hasOverride;
        bool overrideIsTransform;
        bool onTarget;
        bool hasPreviousRequest;
        YawPitch previousRequest;

        public LookProfile Profile
        {
            get => profile;
            set
            {
                profile = value;
                if (solver != null) solver.Profile = value;
            }
        }

        public LookTargetProvider TargetProvider
        {
            get => targetProvider;
            set => targetProvider = value;
        }

        public LookEvents Events => events;
        public LookSolver Solver => solver ??= new LookSolver(profile);
        public Transform CharacterRoot => characterRoot != null ? characterRoot : transform;
        public Transform LookOrigin => lookOrigin != null ? lookOrigin : CharacterRoot;

        /// <summary>World point the character is trying to look at this frame.</summary>
        public Vector3 CurrentTarget { get; private set; }
        public bool HasTarget { get; private set; }
        public bool IsOnTarget => onTarget;
        public bool HasOverride => hasOverride;

        /// <summary>False while the eyes are skipped by the distance LOD.</summary>
        public bool EyesActive { get; private set; } = true;

        public EyeBehaviour EyeBehaviour => eyeBehaviour;

        internal void Configure(LookProfile lookProfile, LookTargetProvider provider, Transform root, Transform origin)
        {
            Profile = lookProfile;
            targetProvider = provider;
            characterRoot = root;
            lookOrigin = origin;
        }

        void Awake()
        {
            RefreshDrivers();
            if (eyeBehaviour == null) eyeBehaviour = GetComponent<EyeBehaviour>();
        }

        /// <summary>Re-collects the drivers on this object, e.g. after adding one at runtime.</summary>
        public void RefreshDrivers()
        {
            drivers.Clear();
            GetComponents(drivers);
        }

        /// <summary>Look at a transform for <paramref name="duration"/> seconds (negative = until cleared), then return to the provider.</summary>
        public void LookAt(Transform target, float duration = -1f)
        {
            overrideTransform = target;
            overrideIsTransform = true;
            hasOverride = target != null;
            overrideUntil = duration < 0f ? float.PositiveInfinity : Time.time + duration;
        }

        /// <summary>Look at a world point for <paramref name="duration"/> seconds (negative = until cleared).</summary>
        public void LookAt(Vector3 worldPoint, float duration = -1f)
        {
            overrideTransform = null;
            overrideIsTransform = false;
            overridePoint = worldPoint;
            hasOverride = true;
            overrideUntil = duration < 0f ? float.PositiveInfinity : Time.time + duration;
        }

        public void ClearLookAt()
        {
            hasOverride = false;
            overrideTransform = null;
        }

        void LateUpdate()
        {
            Step(Time.deltaTime);
        }

        /// <summary>One look update: target, solver and drivers. Called from LateUpdate; public for previews and tests.</summary>
        public void Step(float deltaTime)
        {
            if (profile == null)
            {
                return;
            }

            // Awake does not run for previews in Edit mode, so collect the drivers lazily.
            if (drivers.Count == 0) RefreshDrivers();

            LookSolver look = Solver;
            look.Profile = profile;
            Transform root = CharacterRoot;
            Vector3 origin = LookOrigin.position;

            HasTarget = TryGetTarget(origin, root, out Vector3 target);
            CurrentTarget = target;
            Vector3 localDirection = HasTarget ? Quaternion.Inverse(root.rotation) * (target - origin) : Vector3.forward;
            look.Update(localDirection, HasTarget, deltaTime);

            // How far the requested look direction jumped this frame drives saccade resting and gaze-shift blinks.
            float shift = HasTarget && hasPreviousRequest ? LookMath.AngleBetween(previousRequest, look.RequestedTarget) : 0f;
            hasPreviousRequest = HasTarget;
            previousRequest = look.RequestedTarget;

            EyesActive = IsWithinEyeLod(origin);
            if (eyeBehaviour == null) eyeBehaviour = GetComponent<EyeBehaviour>();
            bool eyesAlive = EyesActive && eyeBehaviour != null && eyeBehaviour.enabled;
            float speed = deltaTime > 0f ? shift / deltaTime : 0f;
            YawPitch eyeOffset = eyesAlive ? eyeBehaviour.Tick(profile, deltaTime, speed, shift) : default;

            var frame = new LookFrame(root.rotation, target, HasTarget, weight, eyeOffset, EyesActive);
            for (int i = 0; i < drivers.Count; i++)
            {
                if (drivers[i] is Behaviour behaviour && !behaviour.enabled) continue;
                drivers[i].ApplyLook(look, frame);
            }

            if (eyeBehaviour != null && eyeBehaviour.enabled) eyeBehaviour.ApplyLids(eyesAlive);
            UpdateEvents(look);
        }

        bool IsWithinEyeLod(Vector3 origin)
        {
            if (eyeLodDistance <= 0f) return true;
            Camera cam = lodCamera != null ? lodCamera : Camera.main;
            return cam == null || (cam.transform.position - origin).sqrMagnitude <= eyeLodDistance * eyeLodDistance;
        }

        bool TryGetTarget(Vector3 origin, Transform root, out Vector3 target)
        {
            // An override ends when its time is up or its transform has been destroyed.
            if (hasOverride && (Time.time > overrideUntil || (overrideIsTransform && overrideTransform == null)))
            {
                ClearLookAt();
            }

            if (hasOverride)
            {
                target = overrideIsTransform ? overrideTransform.position : overridePoint;
                return true;
            }

            if (targetProvider != null)
            {
                var context = new LookContext(origin, root.forward, root);
                return targetProvider.TryGetTarget(context, out target);
            }

            target = default;
            return false;
        }

        void UpdateEvents(LookSolver look)
        {
            float error = look.GazeError;
            if (!onTarget && HasTarget && error <= onTargetAngle)
            {
                onTarget = true;
                events.lookedAtTarget.Invoke();
            }
            else if (onTarget && (!HasTarget || error > lostTargetAngle))
            {
                onTarget = false;
                events.lostTarget.Invoke();
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < drivers.Count; i++)
            {
                if (drivers[i] is TransformLookDriver transformDriver) transformDriver.RestorePose();
            }
        }

        void OnDrawGizmosSelected()
        {
            if (profile == null) return;
            Transform root = CharacterRoot;
            Vector3 origin = LookOrigin.position;
            float length = HasTarget ? Mathf.Max(0.5f, Vector3.Distance(origin, CurrentTarget)) : 1.5f;

            if (Application.isPlaying || solver != null)
            {
                if (HasTarget)
                {
                    Gizmos.color = new Color(1f, 0.85f, 0.2f);
                    Gizmos.DrawLine(origin, CurrentTarget);
                    Gizmos.DrawWireSphere(CurrentTarget, 0.06f);
                }

                Gizmos.color = onTarget ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.4f, 0.3f);
                Gizmos.DrawRay(origin, root.rotation * LookMath.ToDirection(Solver.Gaze) * length);
            }

            // Reach of the whole chain (outer cone) and of the head alone (inner cone).
            float upReach = 0f, downReach = 0f;
            for (int i = 0; i < LookSegments.Count; i++)
            {
                LookSegmentSettings s = profile.Get((LookSegment)i);
                upReach += s.maxPitchUp;
                downReach += s.maxPitchDown;
            }

            DrawLimitCone(origin, root.rotation, profile.MaxReachYaw, Mathf.Min(upReach, 89f), Mathf.Min(downReach, 89f), 0.6f,
                new Color(1f, 1f, 1f, 0.35f));
            DrawLimitCone(origin, root.rotation, profile.head.maxYaw, profile.head.maxPitchUp, profile.head.maxPitchDown, 0.45f,
                new Color(0.4f, 0.8f, 1f, 0.6f));
        }

        static void DrawLimitCone(Vector3 origin, Quaternion rotation, float yaw, float up, float down, float radius, Color color)
        {
            const int Steps = 16;
            Gizmos.color = color;
            Vector3 Point(float y, float p) => origin + rotation * LookMath.ToDirection(new YawPitch(y, p)) * radius;

            for (int i = 0; i < Steps; i++)
            {
                float a = Mathf.Lerp(-yaw, yaw, i / (float)Steps);
                float b = Mathf.Lerp(-yaw, yaw, (i + 1) / (float)Steps);
                Gizmos.DrawLine(Point(a, up), Point(b, up));
                Gizmos.DrawLine(Point(a, -down), Point(b, -down));
                float c = Mathf.Lerp(-down, up, i / (float)Steps);
                float d = Mathf.Lerp(-down, up, (i + 1) / (float)Steps);
                Gizmos.DrawLine(Point(-yaw, c), Point(-yaw, d));
                Gizmos.DrawLine(Point(yaw, c), Point(yaw, d));
            }

            Gizmos.DrawLine(origin, Point(-yaw, up));
            Gizmos.DrawLine(origin, Point(yaw, up));
            Gizmos.DrawLine(origin, Point(-yaw, -down));
            Gizmos.DrawLine(origin, Point(yaw, -down));
        }
    }
}
