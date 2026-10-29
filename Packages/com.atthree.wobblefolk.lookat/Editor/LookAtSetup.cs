using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wobblefolk.LookAt.Editor
{
    /// <summary>Bones the look system drives. Any may be empty except the head.</summary>
    [Serializable]
    public sealed class LookBoneMap
    {
        public Transform torso;
        public Transform upperChest;
        public Transform neck;
        public Transform head;
        public Transform leftEye;
        public Transform rightEye;

        public bool HasEyes => leftEye != null || rightEye != null;
    }

    public enum LookTargetKind
    {
        /// <summary>Interest points pull the gaze; otherwise it follows the mouse (or screen centre when locked).</summary>
        MouseWithInterestPoints = 0,
        /// <summary>Follows the mouse cursor (or the screen centre while the cursor is locked).</summary>
        Mouse = 1,
        /// <summary>Looks where the camera looks.</summary>
        CameraForward = 2,
        /// <summary>Looks at a sphere in the scene that can be dragged around, also in Edit mode.</summary>
        PreviewSphere = 3,
        /// <summary>No provider: set one later, or drive the look from code.</summary>
        None = 4,
    }

    [Serializable]
    public sealed class LookSetupOptions
    {
        [Tooltip("Empty = find or create a profile of the chosen preset in Assets/LookAt.")]
        public LookProfile profile;
        public LookPreset preset = LookPreset.Realistic;
        public LookTargetKind target = LookTargetKind.MouseWithInterestPoints;
        [Tooltip("Camera for mouse / camera targets. Empty = the main camera at runtime.")]
        public Camera camera;
        [Tooltip("Add saccades (and blinks once eyelids are set on it).")]
        public bool addEyeBehaviour = true;
    }

    /// <summary>Finds look bones and adds or removes the look components. Used by the Setup Look At window.</summary>
    public static class LookAtSetup
    {
        public const string OriginName = "LookOrigin";
        public const string PreviewTargetName = "Look Preview Target";
        const string ProfileFolder = "Assets/LookAt";

        /// <summary>Bones from a Humanoid avatar, else by common bone names.</summary>
        public static LookBoneMap FindBones(GameObject root)
        {
            var map = new LookBoneMap();
            if (root == null) return map;

            Animator animator = root.GetComponentInChildren<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                map.torso = animator.GetBoneTransform(HumanBodyBones.Spine);
                map.upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (map.upperChest == null) map.upperChest = animator.GetBoneTransform(HumanBodyBones.Chest);
                map.neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                map.head = animator.GetBoneTransform(HumanBodyBones.Head);
                map.leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                map.rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
                return map;
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (map.head == null && n.Contains("head") && !n.Contains("end") && !n.Contains("top")) map.head = t;
                else if (map.neck == null && n.Contains("neck")) map.neck = t;
                else if (map.upperChest == null && (n.Contains("upperchest") || n.Contains("chest"))) map.upperChest = t;
                else if (map.torso == null && n.Contains("spine")) map.torso = t;
                else if (map.leftEye == null && n.Contains("eye") && (n.Contains("left") || n.EndsWith("_l") || n.EndsWith(".l"))) map.leftEye = t;
                else if (map.rightEye == null && n.Contains("eye") && (n.Contains("right") || n.EndsWith("_r") || n.EndsWith(".r"))) map.rightEye = t;
            }

            return map;
        }

        /// <summary>Error message, or null when the setup can be applied.</summary>
        public static string Validate(GameObject root, LookBoneMap map)
        {
            if (root == null) return "Select a character.";
            if (PrefabUtility.IsPartOfPrefabAsset(root)) return "Use an instance in the scene, not the prefab asset.";
            if (root.GetComponent<LookAtController>() != null) return "The character already has a Look At Controller. Remove it first.";
            if (map.head == null) return "Assign at least the head bone.";
            if (!map.head.IsChildOf(root.transform)) return "The bones must be children of the character.";
            return null;
        }

        public static LookAtController Apply(GameObject root, LookBoneMap map, LookSetupOptions options)
        {
            string error = Validate(root, map);
            if (error != null) throw new InvalidOperationException(error);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Look At");

            LookProfile profile = options.profile != null ? options.profile : FindOrCreateProfile(options.preset);

            var driver = Undo.AddComponent<TransformLookDriver>(root);
            driver.torso = map.torso;
            driver.upperChest = map.upperChest;
            driver.neck = map.neck;
            driver.head = map.head;
            driver.leftEye = map.leftEye;
            driver.rightEye = map.rightEye;

            // The look is measured from between the eyes, or from the head.
            var origin = new GameObject(OriginName).transform;
            Undo.RegisterCreatedObjectUndo(origin.gameObject, "Setup Look At");
            origin.SetParent(map.head, false);
            origin.position = map.leftEye != null && map.rightEye != null
                ? (map.leftEye.position + map.rightEye.position) * 0.5f
                : map.head.position;
            origin.rotation = root.transform.rotation;

            LookTargetProvider provider = CreateProvider(root, options);
            if (options.addEyeBehaviour && map.HasEyes) Undo.AddComponent<EyeBehaviour>(root);

            var controller = Undo.AddComponent<LookAtController>(root);
            controller.Configure(profile, provider, root.transform, origin);

            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(root);
            return controller;
        }

        static LookTargetProvider CreateProvider(GameObject root, LookSetupOptions options)
        {
            switch (options.target)
            {
                case LookTargetKind.Mouse:
                    return Mouse(root, options.camera);

                case LookTargetKind.CameraForward:
                    var forward = Undo.AddComponent<CameraForwardTarget>(root);
                    if (options.camera != null) forward.Camera = options.camera;
                    return forward;

                case LookTargetKind.MouseWithInterestPoints:
                    var interest = Undo.AddComponent<InterestLookTarget>(root);
                    interest.fallback = Mouse(root, options.camera);
                    return interest;

                case LookTargetKind.PreviewSphere:
                    var transformTarget = Undo.AddComponent<TransformLookTarget>(root);
                    transformTarget.target = CreatePreviewTarget(root.transform);
                    return transformTarget;

                default:
                    return null;
            }
        }

        static MouseLookTarget Mouse(GameObject root, Camera camera)
        {
            var mouse = Undo.AddComponent<MouseLookTarget>(root);
            if (camera != null) mouse.Camera = camera;
            return mouse;
        }

        /// <summary>A small sphere in front of the character to drag around as a look target.</summary>
        public static Transform CreatePreviewTarget(Transform character, Vector3? position = null)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = PreviewTargetName;
            UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.localScale = Vector3.one * 0.15f;
            sphere.transform.position = position ?? character.position + character.up * 1.6f + character.forward * 1.5f;
            Undo.RegisterCreatedObjectUndo(sphere, "Create Look Preview Target");
            return sphere.transform;
        }

        /// <summary>Removes every look component from the character and puts the bones back.</summary>
        public static void Remove(GameObject root)
        {
            if (root == null) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Look At");

            LookAtController controller = root.GetComponent<LookAtController>();
            if (controller != null) LookAtEditModePreview.Stop(controller);

            foreach (var d in root.GetComponents<TransformLookDriver>()) d.RestorePose();
            foreach (var d in root.GetComponents<JointLookDriver>()) d.ResetTargets();

            DestroyAll<LookAtController>(root);
            DestroyAll<EyeBehaviour>(root);
            DestroyAll<TransformLookDriver>(root);
            DestroyAll<JointLookDriver>(root);
            DestroyAll<InterestLookTarget>(root);
            DestroyAll<LookTargetProvider>(root);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == OriginName) Undo.DestroyObjectImmediate(t.gameObject);
            }

            Undo.CollapseUndoOperations(group);
        }

        static void DestroyAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponents<T>()) Undo.DestroyObjectImmediate(component);
        }

        public static LookProfile FindOrCreateProfile(LookPreset preset)
        {
            string path = $"{ProfileFolder}/LookProfile_{preset}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LookProfile>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(ProfileFolder);
            var profile = ScriptableObject.CreateInstance<LookProfile>();
            LookProfilePresets.Apply(profile, preset);
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
