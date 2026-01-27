using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MSpeaker.Editor
{
    [CustomEditor(typeof(MspDialogueImporter))]
    public sealed class MspDialogueImporterEditor : ScriptedImporterEditor
    {
        private string _filePreview;
        private Vector2 _scrollPosition;

        public override void OnEnable()
        {
            base.OnEnable();
            var importer = (MspDialogueImporter)target;
            _filePreview = System.IO.File.ReadAllText(importer.assetPath);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("File Preview (.msp)", EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition, GUILayout.MinHeight(200)))
            {
                _scrollPosition = scroll.scrollPosition;
                EditorGUILayout.SelectableLabel(_filePreview, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            }

            ApplyRevertGUI();
        }
    }
}

