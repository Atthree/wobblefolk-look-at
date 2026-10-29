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

            for (int i = 0; i < drivers.Count; i++)
            {
                if (drivers[i] is Behaviour behaviour && !behaviour.enabled) continue;
                drivers[i].ApplyLook(look, root.rotation, weight);
            }

            UpdateEvents(look);
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
    }
}
