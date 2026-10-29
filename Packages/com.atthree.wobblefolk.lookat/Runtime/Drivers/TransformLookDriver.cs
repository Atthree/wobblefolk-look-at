using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>
    /// Drives look bones through their transforms, on top of whatever pose the Animator (or nothing) produced this
    /// frame. Each bone gets only its own share of the turn, because children already inherit their parents'
    /// rotation. Missing bones are skipped and their share is taken over by the next bone up the chain.
    /// <para>
    /// Without an Animator nothing resets the bones each frame, so the driver undoes its own previous offset before
    /// applying the new one; with an Animator the animated pose is simply used as the base.
    /// </para>
    /// </summary>
    [AddComponentMenu("Wobblefolk/Look At/Transform Look Driver")]
    public sealed class TransformLookDriver : MonoBehaviour, ILookDriver
    {
        [Header("Chain (any may be empty)")]
        public Transform torso;
        public Transform upperChest;
        public Transform neck;
        public Transform head;

        [Header("Eyes (any may be empty)")]
        public Transform leftEye;
        public Transform rightEye;

        [Tooltip("Drive the body links (torso to head). Turn off when another driver moves them, e.g. a joint driver; " +
                 "the eyes are then aimed absolutely, so they stay on target while a physical head wobbles.")]
        public bool driveBody = true;
        [Tooltip("Drive the eyes.")]
        public bool driveEyes = true;

        struct BoneRecord
        {
            public Transform bone;
            public Quaternion source;
            public Quaternion written;
            public bool hasWritten;
            public Quaternion restInCharacter;
            public bool hasRest;
        }

        readonly BoneRecord[] records = new BoneRecord[6];

        const int LeftEyeIndex = 4;
        const int RightEyeIndex = 5;

        public void ApplyLook(LookSolver solver, in LookFrame frame)
        {
            Bind();
            RestoreUnchangedBones();

            Quaternion characterRotation = frame.characterRotation;
            float weight = frame.weight;
            Quaternion applied = Quaternion.identity;
            if (driveBody)
            {
                for (int i = 0; i < LookSegments.BodyCount; i++)
                {
                    if (records[i].bone == null) continue;
                    Quaternion delta = LookDriverMath.WorldDelta(solver.GetAccumulated((LookSegment)i), characterRotation, weight);
                    Rotate(i, delta * Quaternion.Inverse(applied));
                    applied = delta;
                }
            }
            else
            {
                // Another driver moves the body; the eyes still have to account for the head's share.
                applied = LookDriverMath.WorldDelta(solver.GetAccumulated(LookSegment.Head), characterRotation, weight);
            }

            if (driveEyes && frame.driveEyes)
            {
                RotateEye(LeftEyeIndex, solver, frame, applied, true);
                RotateEye(RightEyeIndex, solver, frame, applied, false);
            }
        }

        void RotateEye(int index, LookSolver solver, in LookFrame frame, Quaternion headDelta, bool isLeft)
        {
            Transform eye = records[index].bone;
            if (eye == null) return;

            YawPitch angles = LookDriverMath.EyeAngles(solver, frame, eye.position, isLeft);
            Quaternion delta = LookDriverMath.WorldDelta(angles, frame.characterRotation, frame.weight);
            if (driveBody)
            {
                Rotate(index, delta * Quaternion.Inverse(headDelta));
                return;
            }

            // The head is moved by physics and may lag: set the eye's world rotation directly from its rest pose in
            // the character frame, instead of relying on where the head should be.
            ref BoneRecord record = ref records[index];
            if (!record.hasRest)
            {
                record.restInCharacter = Quaternion.Inverse(frame.characterRotation) * eye.rotation;
                record.hasRest = true;
            }

            record.source = eye.localRotation;
            Quaternion world = delta * frame.characterRotation * record.restInCharacter;
            eye.rotation = frame.weight >= 1f ? world : Quaternion.Slerp(eye.rotation, world, frame.weight);
            record.written = eye.localRotation;
            record.hasWritten = true;
        }

        void Bind()
        {
            records[0].bone = torso;
            records[1].bone = upperChest;
            records[2].bone = neck;
            records[3].bone = head;
            records[LeftEyeIndex].bone = leftEye;
            records[RightEyeIndex].bone = rightEye;
        }

        /// <summary>
        /// If a bone still holds exactly what we wrote last frame, nothing (no Animator) has reset it: put back the
        /// pose it had before our offset, so offsets do not pile up frame after frame.
        /// </summary>
        void RestoreUnchangedBones()
        {
            for (int i = 0; i < records.Length; i++)
            {
                ref BoneRecord record = ref records[i];
                if (record.bone != null && record.hasWritten && record.bone.localRotation == record.written)
                {
                    record.bone.localRotation = record.source;
                }
            }
        }

        void Rotate(int index, Quaternion worldDelta)
        {
            ref BoneRecord record = ref records[index];
            Transform bone = record.bone;
            record.source = bone.localRotation;
            bone.rotation = worldDelta * bone.rotation;
            record.written = bone.localRotation;
            record.hasWritten = true;
        }

        /// <summary>Puts every driven bone back to its pose before the last look offset.</summary>
        public void RestorePose()
        {
            for (int i = 0; i < records.Length; i++)
            {
                ref BoneRecord record = ref records[i];
                if (record.bone != null && record.hasWritten)
                {
                    record.bone.localRotation = record.source;
                    record.hasWritten = false;
                }
            }
        }

        void OnDisable()
        {
            RestorePose();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f);
            if (leftEye != null) Gizmos.DrawRay(leftEye.position, leftEye.forward * 0.5f);
            if (rightEye != null) Gizmos.DrawRay(rightEye.position, rightEye.forward * 0.5f);
        }
    }
}
