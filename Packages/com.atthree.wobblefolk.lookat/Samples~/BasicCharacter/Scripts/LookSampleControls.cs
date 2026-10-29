using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Wobblefolk.LookAt.Samples
{
    /// <summary>
    /// Sample controls: 1 / 2 / 3 switch presets, I toggles mouse vs. interest points, L locks the gaze on a random
    /// object for two seconds. Profiles can also be edited live in the Inspector during Play mode.
    /// </summary>
    public sealed class LookSampleControls : MonoBehaviour
    {
        public LookAtController controller;
        public LookProfile[] presets;
        public LookTargetProvider mouseTarget;
        public LookTargetProvider interestTarget;
        public Transform[] lockTargets;
        [Min(0f)] public float lockDuration = 2f;

        int presetIndex;
        bool interestMode;
        string status = "";

        void Start()
        {
            ApplyPreset(0);
            controller.Events.lookedAtTarget.AddListener(() => status = "looked at target");
            controller.Events.lostTarget.AddListener(() => status = "lost target");
        }

        void Update()
        {
            for (int i = 0; i < presets.Length && i < 9; i++)
            {
                if (Pressed(i)) ApplyPreset(i);
            }

            if (PressedInterest())
            {
                interestMode = !interestMode;
                controller.TargetProvider = interestMode ? interestTarget : mouseTarget;
            }

            if (PressedLock() && lockTargets.Length > 0)
            {
                controller.LookAt(lockTargets[Random.Range(0, lockTargets.Length)], lockDuration);
            }
        }

        void ApplyPreset(int index)
        {
            if (index < 0 || index >= presets.Length) return;
            presetIndex = index;
            controller.Profile = presets[index];
        }

        void OnGUI()
        {
            string preset = presets.Length > 0 && presets[presetIndex] != null ? presets[presetIndex].name : "-";
            GUI.Label(new Rect(12, 10, 640, 22), $"Profile: {preset}   Target: {(interestMode ? "interest points" : "mouse")}   {(controller.HasOverride ? "(locked)" : "")}   {status}");
            GUI.Label(new Rect(12, 30, 640, 22), "1 / 2 / 3 presets    I mouse / interest points    L look at a random object for 2 s");
        }

#if ENABLE_INPUT_SYSTEM
        static bool Pressed(int number)
        {
            Keyboard k = Keyboard.current;
            return k != null && (k[Key.Digit1 + number].wasPressedThisFrame || k[Key.Numpad1 + number].wasPressedThisFrame);
        }

        static bool PressedInterest() => Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
        static bool PressedLock() => Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame;
#else
        static bool Pressed(int number) => Input.GetKeyDown(KeyCode.Alpha1 + number) || Input.GetKeyDown(KeyCode.Keypad1 + number);
        static bool PressedInterest() => Input.GetKeyDown(KeyCode.I);
        static bool PressedLock() => Input.GetKeyDown(KeyCode.L);
#endif
    }
}
