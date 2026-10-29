using UnityEngine;
using Wobblefolk.Ragdoll;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// One-component setup for a Wobblefolk Active Ragdoll. On Start it adds a look frame on the chest, a
    /// <see cref="JointLookDriver"/> that turns the head through its joint motor, an optional
    /// <see cref="TransformLookDriver"/> for the eyes, and a <see cref="LookAtController"/>. The look fades out while
    /// the ragdoll is fallen, getting up or unconscious.
    /// <para>
    /// Only the head is driven physically: the ragdoll's own arm system writes the chest joint (bending over to
    /// reach), so the look stays off it. The look frame rides on the chest, so the head turns relative to the body.
    /// </para>
    /// Compiled only when the com.wobblefolk.active-ragdoll package is installed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActiveRagdoll))]
    [AddComponentMenu("Wobblefolk/Look At/Active Ragdoll Look At")]
    public sealed class ActiveRagdollLookAt : MonoBehaviour
    {
        [Tooltip("Empty = a head-only profile suited to the ragdoll's head joint limits, created at runtime.")]
        [SerializeField] LookProfile profile;
        [SerializeField] LookTargetProvider targetProvider;

        [Header("Eyes (optional)")]
        public Transform leftEye;
        public Transform rightEye;
        [Tooltip("Adds an EyeBehaviour for saccades (and blinks, if lids are set on it).")]
        public bool addEyeBehaviour = true;

        [Header("Fading")]
        [Range(0f, 1f)] public float maxWeight = 1f;
        [Tooltip("How fast the look fades in and out when the ragdoll's state changes (weight per second).")]
        [Min(0f)] public float fadeSpeed = 3f;

        ActiveRagdoll ragdoll;

        public LookAtController Controller { get; private set; }

        /// <summary>Look profile; set it before Start, or at any time to switch profiles.</summary>
        public LookProfile Profile
        {
            get => profile;
            set
            {
                profile = value;
                if (Controller != null && value != null) Controller.Profile = value;
            }
        }
        public JointLookDriver JointDriver { get; private set; }

        public LookTargetProvider TargetProvider
        {
            get => Controller != null ? Controller.TargetProvider : targetProvider;
            set
            {
                targetProvider = value;
                if (Controller != null) Controller.TargetProvider = value;
            }
        }

        internal void Configure(LookProfile lookProfile, LookTargetProvider provider, Transform left, Transform right)
        {
            profile = lookProfile;
            targetProvider = provider;
            leftEye = left;
            rightEye = right;
        }

        void Start()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
            if (ragdoll.Head == null || ragdoll.Chest == null || ragdoll.Head.Joint == null)
            {
                Debug.LogError($"{nameof(ActiveRagdollLookAt)} needs a ragdoll with chest and head bones.", this);
                enabled = false;
                return;
            }

            if (profile == null) profile = CreateHeadOnlyProfile();

            // The look frame rides on the chest, facing the character's forward in the rest pose.
            var frame = new GameObject("LookFrame").transform;
            frame.SetParent(ragdoll.Chest.transform, false);
            frame.SetPositionAndRotation(ragdoll.Head.transform.position, transform.rotation);

            Transform origin = frame;
            if (leftEye != null && rightEye != null)
            {
                origin = new GameObject("LookOrigin").transform;
                origin.SetParent(ragdoll.Head.transform, false);
                origin.position = (leftEye.position + rightEye.position) * 0.5f;
            }

            JointDriver = gameObject.AddComponent<JointLookDriver>();
            JointDriver.head = ragdoll.Head.Joint;

            if (leftEye != null || rightEye != null)
            {
                var eyes = gameObject.AddComponent<TransformLookDriver>();
                eyes.driveBody = false;
                eyes.leftEye = leftEye;
                eyes.rightEye = rightEye;
                if (addEyeBehaviour && GetComponent<EyeBehaviour>() == null) gameObject.AddComponent<EyeBehaviour>();
            }

            Controller = gameObject.AddComponent<LookAtController>();
            Controller.Configure(profile, targetProvider, frame, origin);
            Controller.RefreshDrivers();
            Controller.weight = 0f;
        }

        void Update()
        {
            if (Controller == null) return;
            float target = ragdoll.State == RagdollState.Normal ? maxWeight : 0f;
            Controller.weight = Mathf.MoveTowards(Controller.weight, target, fadeSpeed * Time.deltaTime);
        }

        /// <summary>
        /// A profile that turns only the head (within the ragdoll head joint's default limits) and lets the eyes cover
        /// the rest. Torso, chest and neck take no share because the look frame already rides on the chest.
        /// </summary>
        public static LookProfile CreateHeadOnlyProfile()
        {
            var lookProfile = ScriptableObject.CreateInstance<LookProfile>();
            lookProfile.name = "Active Ragdoll Head Only (runtime)";
            lookProfile.torso.weight = 0f;
            lookProfile.upperChest.weight = 0f;
            lookProfile.neck.weight = 0f;
            lookProfile.head.weight = 0.85f;
            lookProfile.head.engageAngle = 3f;
            lookProfile.head.maxYaw = 45f;
            lookProfile.head.maxPitchUp = 30f;
            lookProfile.head.maxPitchDown = 30f;
            lookProfile.head.smoothTime = 0.12f;
            lookProfile.head.delay = 0.04f;
            lookProfile.maxBodyYaw = 45f;
            return lookProfile;
        }
    }
}
