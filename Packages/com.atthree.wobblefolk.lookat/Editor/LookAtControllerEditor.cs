using UnityEditor;
using UnityEngine;

namespace Wobblefolk.LookAt.Editor
{
    [CustomEditor(typeof(LookAtController))]
    sealed class LookAtControllerEditor : UnityEditor.Editor
    {
        Transform previewTarget;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (LookAtController)target;

            EditorGUILayout.Space();
            if (Application.isPlaying)
            {
                DrawRuntimeStatus(controller);
                return;
            }

            EditorGUILayout.LabelField("Edit Mode Preview", EditorStyles.boldLabel);
            bool previewing = LookAtEditModePreview.IsActive(controller);
            if (previewing) previewTarget = LookAtEditModePreview.GetTarget(controller);

            EditorGUI.BeginChangeCheck();
            previewTarget = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Preview Target", "Drag this object around the Scene view. Empty = the target provider."),
                previewTarget, typeof(Transform), true);
            if (EditorGUI.EndChangeCheck() && previewing) LookAtEditModePreview.Start(controller, previewTarget);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Preview Target"))
                {
                    Transform origin = controller.LookOrigin;
                    Transform root = controller.CharacterRoot;
                    previewTarget = LookAtSetup.CreatePreviewTarget(root, origin.position + root.forward * 1.5f);
                    LookAtEditModePreview.Start(controller, previewTarget);
                    Selection.activeTransform = previewTarget;
                }

                string label = previewing ? "Stop Preview" : "Preview in Edit Mode";
                if (GUILayout.Button(label))
                {
                    if (previewing) LookAtEditModePreview.Stop(controller);
                    else LookAtEditModePreview.Start(controller, previewTarget);
                }
            }

            if (previewing)
            {
                EditorGUILayout.HelpBox("Previewing. The bones are put back before the scene is saved and when the preview stops.", MessageType.Info);
            }
        }

        static void DrawRuntimeStatus(LookAtController controller)
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                LookSolver solver = controller.Solver;
                EditorGUILayout.Toggle("Has Target", controller.HasTarget);
                EditorGUILayout.Toggle("On Target", controller.IsOnTarget);
                EditorGUILayout.Toggle("Target Behind", solver.IsBehind);
                EditorGUILayout.Toggle("Eyes Active (LOD)", controller.EyesActive);
                EditorGUILayout.FloatField("Gaze Error (°)", solver.GazeError);
                EditorGUILayout.TextField("Gaze", solver.Gaze.ToString());
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
