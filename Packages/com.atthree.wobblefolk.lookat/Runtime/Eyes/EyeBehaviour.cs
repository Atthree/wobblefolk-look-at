using UnityEngine;

namespace Wobblefolk.LookAt
{
    public enum EyelidMode
    {
        /// <summary>No eyelids: blinking is skipped.</summary>
        None = 0,
        /// <summary>Blend shapes on a skinned mesh, from 0 (open) to the max weight (closed).</summary>
        BlendShape = 1,
        /// <summary>Eyelid bones rotated about a local axis.</summary>
        BoneRotation = 2,
        /// <summary>Lid objects scaled between an open and a closed scale (handy for primitive characters).</summary>
        Scale = 3,
    }

    /// <summary>
    /// Adds life to the eyes: saccades (small random jumps and idle glances) and blinking. The
    /// <see cref="LookAtController"/> on the same object ticks it every look update; timing comes from the
    /// <see cref="LookProfile"/>, the eyelid rig from this component.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Wobblefolk/Look At/Eye Behaviour")]
    public sealed class EyeBehaviour : MonoBehaviour
    {
        [Tooltip("Seed for saccade and blink timing. 0 = derived from the object, so characters differ.")]
        [SerializeField] int randomSeed = 0;

        [Header("Eyelids")]
        public EyelidMode eyelidMode = EyelidMode.None;

        [Tooltip("Blend shape mode: mesh with the blink shapes.")]
        public SkinnedMeshRenderer lidRenderer;
        [Tooltip("Blend shape mode: names of the shapes that close the lids.")]
        public string[] blendShapeNames = { "Blink" };
        [Tooltip("Blend shape mode: weight of a fully closed lid.")]
        [Range(0f, 100f)] public float closedBlendShapeWeight = 100f;

        [Tooltip("Bone and scale modes: the lid transforms.")]
        public Transform[] lids = new Transform[0];
        [Tooltip("Bone mode: local axis the lids rotate about to close.")]
        public Vector3 closeAxis = Vector3.right;
        [Tooltip("Bone mode: rotation of a fully closed lid (degrees).")]
        public float closedAngle = 30f;
        [Tooltip("Scale mode: local scale of an open lid.")]
        public Vector3 openScale = new Vector3(1f, 0f, 1f);
        [Tooltip("Scale mode: local scale of a closed lid.")]
        public Vector3 closedScale = Vector3.one;

        SaccadeGenerator saccades;
        BlinkTimer blinks;
        Quaternion[] lidRestRotations;
        int[] blendShapeIndices;
        bool initialized;

        /// <summary>Current saccade offset added to the eyes (degrees).</summary>
        public YawPitch SaccadeOffset { get; private set; }

        /// <summary>0 = open, 1 = closed.</summary>
        public float Closedness { get; private set; }

        public SaccadeGenerator Saccades => saccades;
        public BlinkTimer Blinks => blinks;

        void Initialize()
        {
            if (initialized) return;
            int seed = randomSeed != 0 ? randomSeed : GetInstanceID();
            saccades = new SaccadeGenerator(seed);
            blinks = new BlinkTimer(seed * 31 + 7);

            lidRestRotations = new Quaternion[lids.Length];
            for (int i = 0; i < lids.Length; i++)
            {
                if (lids[i] != null) lidRestRotations[i] = lids[i].localRotation;
            }

            blendShapeIndices = new int[blendShapeNames.Length];
            for (int i = 0; i < blendShapeNames.Length; i++)
            {
                blendShapeIndices[i] = lidRenderer != null && lidRenderer.sharedMesh != null
                    ? lidRenderer.sharedMesh.GetBlendShapeIndex(blendShapeNames[i])
                    : -1;
            }

            initialized = true;
        }

        /// <summary>Starts a blink now.</summary>
        public void Blink()
        {
            Initialize();
            blinks.Blink();
        }

        /// <summary>Advances saccades and blinking; returns the eye offset for this frame.</summary>
        /// <param name="targetSpeed">Angular speed of the look target (degrees/s).</param>
        /// <param name="gazeShift">How far the look target jumped this frame (degrees).</param>
        public YawPitch Tick(LookProfile profile, float deltaTime, float targetSpeed, float gazeShift)
        {
            Initialize();
            SaccadeOffset = saccades.Update(profile.saccades, deltaTime, targetSpeed);
            Closedness = eyelidMode == EyelidMode.None ? 0f : blinks.Update(profile.blinks, deltaTime, gazeShift);
            return SaccadeOffset;
        }

        /// <summary>Writes the lid pose. With <paramref name="active"/> false (e.g. distance LOD) the lids stay open.</summary>
        public void ApplyLids(bool active)
        {
            Initialize();
            float closed = active ? Closedness : 0f;
            switch (eyelidMode)
            {
                case EyelidMode.BlendShape:
                    if (lidRenderer == null) break;
                    foreach (int index in blendShapeIndices)
                    {
                        if (index >= 0) lidRenderer.SetBlendShapeWeight(index, closed * closedBlendShapeWeight);
                    }
                    break;

                case EyelidMode.BoneRotation:
                    Quaternion close = Quaternion.AngleAxis(closedAngle * closed, closeAxis);
                    for (int i = 0; i < lids.Length; i++)
                    {
                        if (lids[i] != null) lids[i].localRotation = lidRestRotations[i] * close;
                    }
                    break;

                case EyelidMode.Scale:
                    Vector3 scale = Vector3.Lerp(openScale, closedScale, closed);
                    foreach (Transform lid in lids)
                    {
                        if (lid != null) lid.localScale = scale;
                    }
                    break;
            }
        }

        void OnDisable()
        {
            if (!initialized) return;
            Closedness = 0f;
            ApplyLids(false);
        }
    }
}
