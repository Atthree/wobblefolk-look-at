using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// Drives look bones through the <see cref="ConfigurableJoint.targetRotation"/> of their joints, so the look is
    /// physical: the joint motor (slerp drive) turns the bone, and a wobbling body makes the head lag naturally. Each
    /// joint is aimed at the rotation its bone would have relative to its connected body if the chain were posed
    /// kinematically. Missing joints are skipped and their share goes to the next joint up the chain. Uses only Unity's
    /// own ConfigurableJoint; no ragdoll package is needed.
    /// <para>The rest pose is captured on the first update, so the character must be in its rest pose then.</para>
    /// </summary>
    [AddComponentMenu("Wobblefolk/Look At/Joint Look Driver")]
    public sealed class JointLookDriver : MonoBehaviour, ILookDriver
    {
        [Header("Joints (any may be empty)")]
        public ConfigurableJoint torso;
        public ConfigurableJoint upperChest;
        public ConfigurableJoint neck;
        public ConfigurableJoint head;

        struct JointRecord
        {
            public ConfigurableJoint joint;
            public Quaternion jointToLocal;
            public Quaternion startRelative;
            public Quaternion restBody;
            public Quaternion restParent;
            public bool captured;
        }

        readonly JointRecord[] records = new JointRecord[LookSegments.BodyCount];

        public void ApplyLook(LookSolver solver, in LookFrame frame)
        {
            Quaternion character = frame.characterRotation;
            Quaternion previous = Quaternion.identity;

            for (int i = 0; i < records.Length; i++)
            {
                ref JointRecord record = ref records[i];
                ConfigurableJoint joint = JointAt(i);
                if (joint == null)
                {
                    record.captured = false;
                    continue;
                }

                if (!record.captured || record.joint != joint)
                {
                    Capture(ref record, joint, character);
                }

                Quaternion delta = LookDriverMath.WorldDelta(solver.GetAccumulated((LookSegment)i), character, frame.weight);
                joint.targetRotation = TargetRotation(record, character, previous, delta);
                previous = delta;
            }
        }

        /// <summary>
        /// Joint-space target rotation that holds the body at <paramref name="delta"/> (world look rotation about the
        /// character axes) while its connected body is at <paramref name="parentDelta"/>, both relative to the rest pose.
        /// </summary>
        static Quaternion TargetRotation(in JointRecord record, Quaternion character, Quaternion parentDelta, Quaternion delta)
        {
            Quaternion body = delta * character * record.restBody;
            Quaternion parent = parentDelta * character * record.restParent;
            Quaternion relative = Quaternion.Inverse(parent) * body;
            Quaternion local = Quaternion.Inverse(record.startRelative) * relative;
            Quaternion jointSpace = Quaternion.Inverse(record.jointToLocal) * local * record.jointToLocal;
            return Quaternion.Inverse(jointSpace);
        }

        static void Capture(ref JointRecord record, ConfigurableJoint joint, Quaternion character)
        {
            Quaternion inverseCharacter = Quaternion.Inverse(character);
            Quaternion bodyRotation = joint.transform.rotation;
            Quaternion parentRotation = joint.connectedBody != null ? joint.connectedBody.transform.rotation : Quaternion.identity;

            record.joint = joint;
            record.jointToLocal = JointToLocal(joint.axis, joint.secondaryAxis);
            record.startRelative = Quaternion.Inverse(parentRotation) * bodyRotation;
            record.restBody = inverseCharacter * bodyRotation;
            record.restParent = inverseCharacter * parentRotation;
            record.captured = true;
        }

        /// <summary>Rotation from joint space (x = axis, y = secondary axis) into the body's local space.</summary>
        static Quaternion JointToLocal(Vector3 axis, Vector3 secondaryAxis)
        {
            Vector3 right = axis.normalized;
            Vector3 forward = Vector3.Cross(right, secondaryAxis).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            return Quaternion.LookRotation(forward, up);
        }

        ConfigurableJoint JointAt(int index)
        {
            switch (index)
            {
                case 0: return torso;
                case 1: return upperChest;
                case 2: return neck;
                default: return head;
            }
        }

        /// <summary>Releases every driven joint back to its rest target.</summary>
        public void ResetTargets()
        {
            for (int i = 0; i < records.Length; i++)
            {
                ConfigurableJoint joint = JointAt(i);
                if (joint != null) joint.targetRotation = Quaternion.identity;
            }
        }

        /// <summary>Forgets the captured rest pose, e.g. after re-posing the character.</summary>
        public void Recapture()
        {
            for (int i = 0; i < records.Length; i++) records[i].captured = false;
        }

        void OnDisable()
        {
            ResetTargets();
        }
    }
}
