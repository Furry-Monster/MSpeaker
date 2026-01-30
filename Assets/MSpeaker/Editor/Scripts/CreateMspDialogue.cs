using System.IO;
using UnityEditor;
using UnityEngine;

namespace MSpeaker.Editor
{
    public static class CreateMspDialogue
    {
        [MenuItem("Assets/Create/MSpeaker Dialogue (.msp)", false, 50)]
        public static void CreateDialogue()
        {
            var templatePath = MspEditorSettings.TemplatePath;
            if (!File.Exists(templatePath))
            {
                Debug.LogError("[MSpeaker] 模板文件不存在: " + templatePath + "\n可在 Edit → Preferences → MSpeaker 中修改默认模板路径。");
                return;
            }

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "New Dialogue.msp");
        }
    }
}