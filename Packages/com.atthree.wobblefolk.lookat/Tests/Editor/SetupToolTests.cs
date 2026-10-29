using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wobblefolk.LookAt.Editor;

namespace Wobblefolk.LookAt.Tests
{
    public sealed class SetupToolTests
    {
        const float Dt = 1f / 60f;
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            LookAtEditModePreview.StopAll();
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        /// <summary>A Mixamo-style skeleton with a generated Humanoid avatar, eyes included, and odd bone axes.</summary>
        GameObject CreateHumanoid()
        {
            var bones = new (string name, string parent, Vector3 position, string human)[]
            {
                ("mixamorig:Hips", null, new Vector3(0f, 0.98f, 0f), "Hips"),
                ("mixamorig:Spine", "mixamorig:Hips", new Vector3(0f, 1.08f, 0f), "Spine"),
                ("mixamorig:Spine1", "mixamorig:Spine", new Vector3(0f, 1.22f, 0f), "Chest"),
                ("mixamorig:Spine2", "mixamorig:Spine1", new Vector3(0f, 1.35f, 0f), "UpperChest"),
                ("mixamorig:Neck", "mixamorig:Spine2", new Vector3(0f, 1.5f, 0f), "Neck"),
                ("mixamorig:Head", "mixamorig:Neck", new Vector3(0f, 1.6f, 0f), "Head"),
                ("mixamorig:LeftEye", "mixamorig:Head", new Vector3(-0.03f, 1.7f, 0.08f), "LeftEye"),
                ("mixamorig:RightEye", "mixamorig:Head", new Vector3(0.03f, 1.7f, 0.08f), "RightEye"),
                ("mixamorig:LeftShoulder", "mixamorig:Spine2", new Vector3(-0.06f, 1.44f, 0f), "LeftShoulder"),
                ("mixamorig:LeftArm", "mixamorig:LeftShoulder", new Vector3(-0.18f, 1.44f, 0f), "LeftUpperArm"),
                ("mixamorig:LeftForeArm", "mixamorig:LeftArm", new Vector3(-0.46f, 1.44f, 0f), "LeftLowerArm"),
                ("mixamorig:LeftHand", "mixamorig:LeftForeArm", new Vector3(-0.72f, 1.44f, 0f), "LeftHand"),
                ("mixamorig:RightShoulder", "mixamorig:Spine2", new Vector3(0.06f, 1.44f, 0f), "RightShoulder"),
                ("mixamorig:RightArm", "mixamorig:RightShoulder", new Vector3(0.18f, 1.44f, 0f), "RightUpperArm"),
                ("mixamorig:RightForeArm", "mixamorig:RightArm", new Vector3(0.46f, 1.44f, 0f), "RightLowerArm"),
                ("mixamorig:RightHand", "mixamorig:RightForeArm", new Vector3(0.72f, 1.44f, 0f), "RightHand"),
                ("mixamorig:LeftUpLeg", "mixamorig:Hips", new Vector3(-0.1f, 0.93f, 0f), "LeftUpperLeg"),
                ("mixamorig:LeftLeg", "mixamorig:LeftUpLeg", new Vector3(-0.1f, 0.51f, 0f), "LeftLowerLeg"),
                ("mixamorig:LeftFoot", "mixamorig:LeftLeg", new Vector3(-0.1f, 0.08f, 0f), "LeftFoot"),
                ("mixamorig:RightUpLeg", "mixamorig:Hips", new Vector3(0.1f, 0.93f, 0f), "RightUpperLeg"),
                ("mixamorig:RightLeg", "mixamorig:RightUpLeg", new Vector3(0.1f, 0.51f, 0f), "RightLowerLeg"),
                ("mixamorig:RightFoot", "mixamorig:RightLeg", new Vector3(0.1f, 0.08f, 0f), "RightFoot"),
            };

            var root = new GameObject("Humanoid");
            created.Add(root);
            var map = new Dictionary<string, Transform>();
            var human = new List<HumanBone>();
            int k = 1;
            foreach (var b in bones)
            {
                var t = new GameObject(b.name).transform;
                t.SetParent(b.parent == null ? root.transform : map[b.parent], false);
                t.position = b.position;
                if (!b.name.Contains("Eye")) t.rotation = Quaternion.Euler(31f * k, 47f * k, 13f * k);
                map[b.name] = t;
                k++;
                var bone = new HumanBone { humanName = b.human, boneName = b.name };
                bone.limit.useDefaultValues = true;
                human.Add(bone);
            }

            var skeleton = new List<SkeletonBone>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            }

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f,
            });
            created.Add(avatar);
            Assert.IsTrue(avatar.isHuman, "test avatar must be Humanoid");
            root.AddComponent<Animator>().avatar = avatar;
            return root;
        }

        LookSetupOptions Options(LookTargetKind kind = LookTargetKind.PreviewSphere)
        {
            var profile = ScriptableObject.CreateInstance<LookProfile>();
            profile.saccades.enabled = false; // keep the eye checks exact
            created.Add(profile);
            return new LookSetupOptions { profile = profile, target = kind };
        }

        LookAtController Apply(GameObject root, LookSetupOptions options)
        {
            LookAtController controller = LookAtSetup.Apply(root, LookAtSetup.FindBones(root), options);
            var provider = controller.TargetProvider as TransformLookTarget;
            if (provider != null && provider.target != null) created.Add(provider.target.gameObject);
            return controller;
        }

        [Test]
        public void FindBones_MapsTheHumanoidAvatar()
        {
            GameObject root = CreateHumanoid();
            LookBoneMap bones = LookAtSetup.FindBones(root);

            Assert.AreEqual("mixamorig:Spine", bones.torso.name);
            Assert.AreEqual("mixamorig:Spine2", bones.upperChest.name, "upper chest preferred over chest");
            Assert.AreEqual("mixamorig:Neck", bones.neck.name);
            Assert.AreEqual("mixamorig:Head", bones.head.name);
            Assert.AreEqual("mixamorig:LeftEye", bones.leftEye.name);
            Assert.AreEqual("mixamorig:RightEye", bones.rightEye.name);
        }

        [Test]
        public void FindBones_FallsBackToNamesWithoutAnAvatar()
        {
            var root = new GameObject("Generic");
            created.Add(root);
            Transform Child(string n, Transform parent)
            {
                var t = new GameObject(n).transform;
                t.SetParent(parent, false);
                return t;
            }

            Transform spine = Child("Spine", root.transform);
            Transform chest = Child("Chest", spine);
            Transform neck = Child("Neck", chest);
            Transform head = Child("Head", neck);
            Child("Head_End", head);
            Transform left = Child("Eye_L", head);
            Transform right = Child("Eye_R", head);

            LookBoneMap bones = LookAtSetup.FindBones(root);
            Assert.AreSame(spine, bones.torso);
            Assert.AreSame(chest, bones.upperChest);
            Assert.AreSame(neck, bones.neck);
            Assert.AreSame(head, bones.head);
            Assert.AreSame(left, bones.leftEye);
            Assert.AreSame(right, bones.rightEye);
        }

        [Test]
        public void Apply_AddsAWorkingLookSetup()
        {
            GameObject root = CreateHumanoid();
            LookAtController controller = Apply(root, Options());

            var driver = root.GetComponent<TransformLookDriver>();
            Assert.IsNotNull(driver);
            Assert.IsNotNull(root.GetComponent<EyeBehaviour>());
            Assert.AreEqual(LookAtSetup.OriginName, controller.LookOrigin.name);
            Assert.AreEqual(0f, controller.LookOrigin.position.x, 1e-4f, "origin between the eyes");

            Transform target = ((TransformLookTarget)controller.TargetProvider).target;
            target.position = new Vector3(3f, 2.2f, 4f);
            Transform eye = driver.leftEye;
            // Like real rigs, the eye bone's own axes are arbitrary: its look direction is the character's forward in
            // the rest pose, carried along by the eye's rotation.
            Vector3 lookAxis = Quaternion.Inverse(eye.rotation) * root.transform.forward;
            for (int i = 0; i < 240; i++) controller.Step(Dt);

            Assert.Less(Vector3.Angle(eye.rotation * lookAxis, target.position - eye.position), 1.5f, "the set-up character looks at the target");
        }

        [Test]
        public void Apply_IsASingleUndoStep()
        {
            GameObject root = CreateHumanoid();
            Apply(root, Options(LookTargetKind.MouseWithInterestPoints));
            Assert.IsNotNull(root.GetComponent<LookAtController>());

            Undo.PerformUndo();
            Assert.IsNull(root.GetComponent<LookAtController>());
            Assert.IsNull(root.GetComponent<TransformLookDriver>());
            Assert.IsNull(root.GetComponent<MouseLookTarget>());
            Assert.IsNull(root.GetComponent<InterestLookTarget>());
        }

        [Test]
        public void Remove_TakesEverythingOffAndRestoresThePose()
        {
            GameObject root = CreateHumanoid();
            Transform head = LookAtSetup.FindBones(root).head;
            Quaternion rest = head.rotation;

            LookAtController controller = Apply(root, Options());
            ((TransformLookTarget)controller.TargetProvider).target.position = new Vector3(3f, 1.6f, 1f);
            for (int i = 0; i < 120; i++) controller.Step(Dt);
            Assert.Greater(Quaternion.Angle(rest, head.rotation), 5f);

            LookAtSetup.Remove(root);
            Assert.IsNull(root.GetComponent<LookAtController>());
            Assert.IsNull(root.GetComponent<TransformLookDriver>());
            Assert.IsNull(root.GetComponent<EyeBehaviour>());
            Assert.IsNull(root.GetComponent<LookTargetProvider>());
            Assert.IsNull(head.Find(LookAtSetup.OriginName));
            Assert.Less(Quaternion.Angle(rest, head.rotation), 0.01f);
        }

        [Test]
        public void Validate_RequiresAHeadAndNoExistingSetup()
        {
            GameObject root = CreateHumanoid();
            Assert.IsNotNull(LookAtSetup.Validate(root, new LookBoneMap()), "no head");
            Assert.IsNull(LookAtSetup.Validate(root, LookAtSetup.FindBones(root)));

            Apply(root, Options());
            Assert.IsNotNull(LookAtSetup.Validate(root, LookAtSetup.FindBones(root)), "already set up");
        }

        [Test]
        public void EditModePreview_RestoresThePoseWhenStopped()
        {
            GameObject root = CreateHumanoid();
            Transform head = LookAtSetup.FindBones(root).head;
            Quaternion rest = head.rotation;
            LookAtController controller = Apply(root, Options(LookTargetKind.None));

            var target = new GameObject("PreviewTarget").transform;
            created.Add(target.gameObject);
            target.position = new Vector3(-3f, 1.6f, 2f);

            LookAtEditModePreview.Start(controller, target);
            Assert.IsTrue(LookAtEditModePreview.IsActive(controller));
            for (int i = 0; i < 120; i++)
            {
                controller.LookAt(target);
                controller.Step(Dt);
            }

            Assert.Greater(Quaternion.Angle(rest, head.rotation), 5f, "the preview turns the head");

            LookAtEditModePreview.Stop(controller);
            Assert.IsFalse(LookAtEditModePreview.IsActive(controller));
            Assert.Less(Quaternion.Angle(rest, head.rotation), 0.01f, "the rest pose is back");
            Assert.IsFalse(controller.HasOverride);
        }
    }
}
