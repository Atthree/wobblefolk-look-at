using UnityEngine;
using Wobblefolk.Ragdoll;

namespace Wobblefolk.LookAt.Samples
{
    /// <summary>
    /// Builds the integration sample at runtime: a primitive Wobblefolk Active Ragdoll with eyes, player controls and a
    /// follow camera, whose head turns physically (through its joint motor) towards where the camera aims, and whose
    /// gaze is pulled to nearby interest points. Requires the com.wobblefolk.active-ragdoll package.
    /// </summary>
    public sealed class RagdollLookSampleSetup : MonoBehaviour
    {
        [Tooltip("Optional. Empty = the head-only runtime profile of ActiveRagdollLookAt.")]
        public LookProfile lookProfile;
        public Material bodyMaterial;
        public Material eyeWhiteMaterial;
        public Material pupilMaterial;
        [Tooltip("How strongly interest points pull the gaze away from the camera aim (0..1).")]
        [Range(0f, 1f)] public float interestPull = 0.7f;

        void Awake()
        {
            new GameObject("Ragdoll Physics Settings").AddComponent<RagdollPhysicsSettings>();

            var profiles = new RagdollProfileSet
            {
                ragdoll = ScriptableObject.CreateInstance<RagdollProfile>(),
                balance = ScriptableObject.CreateInstance<BalanceProfile>(),
                locomotion = ScriptableObject.CreateInstance<LocomotionProfile>(),
                arms = ScriptableObject.CreateInstance<ArmProfile>(),
                carry = ScriptableObject.CreateInstance<CarryProfile>(),
                climb = ScriptableObject.CreateInstance<ClimbProfile>(),
            };
            ActiveRagdoll ragdoll = PrimitiveRagdollBuilder.Build(new PrimitiveBodyShape(), profiles, transform.position,
                transform.rotation, bodyMaterial, "Ragdoll With Look At");

            Transform leftEye = CreateEye(ragdoll.Head.transform, -0.045f);
            Transform rightEye = CreateEye(ragdoll.Head.transform, 0.045f);

            Camera cam = Camera.main;
            var follow = cam.GetComponent<RagdollFollowCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<RagdollFollowCamera>();
            follow.Target = ragdoll;
            var input = ragdoll.gameObject.AddComponent<RagdollPlayerInput>();
            input.FollowCamera = follow;

            // The cursor is locked by the player input, so the mouse target uses the screen centre: the camera aim.
            var aim = ragdoll.gameObject.AddComponent<MouseLookTarget>();
            aim.Camera = cam;
            aim.source = ScreenPointSource.Auto;
            var interest = ragdoll.gameObject.AddComponent<InterestLookTarget>();
            interest.fallback = aim;
            interest.pull = interestPull;

            var look = ragdoll.gameObject.AddComponent<ActiveRagdollLookAt>();
            look.leftEye = leftEye;
            look.rightEye = rightEye;
            look.TargetProvider = interest;
            if (lookProfile != null) look.Profile = lookProfile;
        }

        Transform CreateEye(Transform head, float x)
        {
            var eye = new GameObject(x < 0f ? "LeftEye" : "RightEye").transform;
            eye.SetParent(head, false);
            // On the front of the primitive's head sphere (radius 0.12, centred 0.18 above the head pivot).
            eye.position = head.position + head.rotation * new Vector3(x, 0.2f, 0.1f);
            eye.rotation = head.rotation;

            Visual(eye, PrimitiveType.Sphere, Vector3.zero, 0.06f, eyeWhiteMaterial);
            Visual(eye, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.024f), 0.03f, pupilMaterial);
            return eye;
        }

        static void Visual(Transform parent, PrimitiveType type, Vector3 localPosition, float size, Material material)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            // Immediately: a collider left until the end of the frame would join the head's rigidbody.
            DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.localScale = Vector3.one * size;
            if (material != null) g.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
