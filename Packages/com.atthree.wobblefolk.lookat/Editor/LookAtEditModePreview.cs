using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wobblefolk.LookAt.Editor
{
    /// <summary>
    /// Runs look controllers in Edit mode so a character follows a preview target dragged around the Scene view.
    /// The modified pose is never saved: bones are restored before a scene is saved, before entering Play mode,
    /// before scripts reload and when the preview stops.
    /// </summary>
    [InitializeOnLoad]
    public static class LookAtEditModePreview
    {
        const double MaxStep = 0.1;

        static readonly Dictionary<LookAtController, Transform> active = new Dictionary<LookAtController, Transform>();
        static readonly List<LookAtController> buffer = new List<LookAtController>();
        static double lastTime;
        static bool subscribed;

        static LookAtEditModePreview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) StopAll();
            };
            AssemblyReloadEvents.beforeAssemblyReload += StopAll;
            EditorSceneManager.sceneSaving += (scene, path) => RestoreAll();
        }

        public static bool IsActive(LookAtController controller) => controller != null && active.ContainsKey(controller);

        public static Transform GetTarget(LookAtController controller) =>
            controller != null && active.TryGetValue(controller, out Transform target) ? target : null;

        /// <summary>Starts previewing; the controller looks at <paramref name="target"/> (or its own provider when null).</summary>
        public static void Start(LookAtController controller, Transform target)
        {
            if (controller == null || Application.isPlaying) return;
            if (!active.ContainsKey(controller)) controller.Solver.Reset();
            active[controller] = target;

            if (!subscribed)
            {
                EditorApplication.update += Update;
                subscribed = true;
                lastTime = EditorApplication.timeSinceStartup;
            }
        }

        public static void Stop(LookAtController controller)
        {
            if (controller == null || !active.Remove(controller)) return;
            Restore(controller);
            if (active.Count == 0) Unsubscribe();
        }

        public static void StopAll()
        {
            buffer.Clear();
            buffer.AddRange(active.Keys);
            foreach (LookAtController controller in buffer) Stop(controller);
        }

        static void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)System.Math.Min(now - lastTime, MaxStep);
            lastTime = now;

            buffer.Clear();
            buffer.AddRange(active.Keys);
            foreach (LookAtController controller in buffer)
            {
                if (controller == null)
                {
                    active.Remove(controller);
                    continue;
                }

                Transform target = active[controller];
                if (target != null) controller.LookAt(target);
                controller.Step(dt);
            }

            if (active.Count == 0) Unsubscribe();
            SceneView.RepaintAll();
        }

        /// <summary>Puts the previewed bones back, e.g. so a scene is saved in its rest pose.</summary>
        static void RestoreAll()
        {
            foreach (LookAtController controller in active.Keys)
            {
                if (controller != null) Restore(controller);
            }
        }

        static void Restore(LookAtController controller)
        {
            foreach (var driver in controller.GetComponents<TransformLookDriver>()) driver.RestorePose();
            foreach (var driver in controller.GetComponents<JointLookDriver>()) driver.ResetTargets();
            EyeBehaviour eyes = controller.GetComponent<EyeBehaviour>();
            if (eyes != null) eyes.ApplyLids(false);
            controller.ClearLookAt();
            controller.Solver.Reset();
        }

        static void Unsubscribe()
        {
            if (!subscribed) return;
            EditorApplication.update -= Update;
            subscribed = false;
        }
    }
}
