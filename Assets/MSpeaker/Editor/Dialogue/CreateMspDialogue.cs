using System.IO;
using UnityEditor;
using UnityEngine;

namespace MSpeaker.Editor.Dialogue
{
    public static class CreateMspDialogue
    {
        private const string TemplateFullPath = "Assets/MSpeaker/Editor/Templates/DefaultDialogue.msp.txt";

        [MenuItem("Assets/Create/MSpeaker Dialogue (.msp)", false, 50)]
        public static void CreateDialogue()
        {
            if (!File.Exists(TemplateFullPath))
            {
                Debug.LogError("Template file not found at: " + TemplateFullPath);
                return;
            }

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(TemplateFullPath, "New Dialogue.msp");
        }
    }
}

