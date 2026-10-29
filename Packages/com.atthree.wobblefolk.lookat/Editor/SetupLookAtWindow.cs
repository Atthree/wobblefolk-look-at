using UnityEditor;
using UnityEngine;

namespace Wobblefolk.LookAt.Editor
{
    /// <summary>Wobblefolk > Setup Look At: finds the look bones of a character and adds the look components.</summary>
    sealed class SetupLookAtWindow : EditorWindow
    {
        [SerializeField] GameObject character;
        [SerializeField] LookBoneMap bones = new LookBoneMap();
        [SerializeField] LookSetupOptions options = new LookSetupOptions();

        SerializedObject serialized;
        Vector2 scroll;

        [MenuItem("Wobblefolk/Setup Look At", false, 0)]
        static void Open()
        {
            var window = GetWindow<SetupLookAtWindow>("Setup Look At");
            window.minSize = new Vector2(360f, 460f);
            if (Selection.activeGameObject != null) window.SetCharacter(Selection.activeGameObject);
        }

        void OnEnable()
        {
            serialized = new SerializedObject(this);
        }

        void SetCharacter(GameObject go)
        {
            character = go;
            bones = LookAtSetup.FindBones(go);
            serialized = new SerializedObject(this);
        }

        void OnGUI()
        {
            serialized.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.HelpBox("Pick a character in the scene. Bones are found from a Humanoid avatar (or by name); " +
                                    "fill in any that are missing. Only the head is required.", MessageType.None);

            EditorGUI.BeginChangeCheck();
            var picked = (GameObject)EditorGUILayout.ObjectField("Character", character, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                SetCharacter(picked);
                serialized.Update();
            }

            if (character != null && GUILayout.Button("Find Bones Again")) SetCharacter(character);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bones", EditorStyles.boldLabel);
            BoneField(nameof(LookBoneMap.torso), "Torso (Spine)");
            BoneField(nameof(LookBoneMap.upperChest), "Upper Chest / Shoulders");
            BoneField(nameof(LookBoneMap.neck), "Neck");
            BoneField(nameof(LookBoneMap.head), "Head (required)");
            BoneField(nameof(LookBoneMap.leftEye), "Left Eye");
            BoneField(nameof(LookBoneMap.rightEye), "Right Eye");

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(options)), new GUIContent("Options"), true);
            serialized.ApplyModifiedProperties();

            EditorGUILayout.Space();
            LookAtController existing = character != null ? character.GetComponent<LookAtController>() : null;
            if (existing != null)
            {
                EditorGUILayout.HelpBox("This character already has a Look At Controller. Preview it from its Inspector.", MessageType.Info);
                if (GUILayout.Button("Remove Look At")) LookAtSetup.Remove(character);
            }
            else
            {
                string error = LookAtSetup.Validate(character, bones);
                if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
                if (!bones.HasEyes && character != null)
                {
                    EditorGUILayout.HelpBox("No eye bones: the head and body still turn. For a full turn without eyes, raise the head weight in the profile.", MessageType.Info);
                }

                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button("Setup Look At", GUILayout.Height(30f)))
                    {
                        LookAtController controller = LookAtSetup.Apply(character, bones, options);
                        Selection.activeGameObject = controller.gameObject;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void BoneField(string field, string label)
        {
            SerializedProperty property = serialized.FindProperty(nameof(bones)).FindPropertyRelative(field);
            Color previous = GUI.color;
            if (property.objectReferenceValue == null && field == nameof(LookBoneMap.head)) GUI.color = new Color(1f, 0.7f, 0.6f);
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            GUI.color = previous;
        }
    }
}
