using UnityEditor;
using UnityEngine;

namespace MSpeaker.Editor
{
    public static class MSpeakerSettingsProvider
    {
        private const string PreferencesPath = "Preferences/MSpeaker";

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(PreferencesPath, SettingsScope.User)
            {
                label = "MSpeaker",
                keywords = new[] { "MSpeaker", "Dialogue", "msp", "Template", "Typewriter" },
                guiHandler = OnGUI
            };
        }

        private static void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("默认值与可配置项", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // 新建 .msp 模板路径
            EditorGUI.BeginChangeCheck();
            var templatePath = EditorGUILayout.TextField(
                new GUIContent("新建对话模板路径", "创建新 .msp 文件时使用的模板路径（相对于项目根）。"),
                MspEditorSettings.TemplatePath);
            if (EditorGUI.EndChangeCheck())
                MspEditorSettings.TemplatePath = templatePath;

            // 打字机默认速度
            EditorGUI.BeginChangeCheck();
            var typewriterSpeed = EditorGUILayout.Slider(
                new GUIContent("打字机默认速度 (字/秒)", "新建 MspTypewriterDialogueView 时的建议默认值，仅作参考。"),
                MspEditorSettings.TypewriterCharsPerSecond,
                1f, 200f);
            if (EditorGUI.EndChangeCheck())
                MspEditorSettings.TypewriterCharsPerSecond = typewriterSpeed;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("验证与导入", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var showInfo = EditorGUILayout.Toggle(
                new GUIContent("验证结果显示信息级别", "在 .msp 导入器验证结果中是否显示“信息”级别条目。"),
                MspEditorSettings.ValidationShowInfo);
            if (EditorGUI.EndChangeCheck())
                MspEditorSettings.ValidationShowInfo = showInfo;

            EditorGUI.BeginChangeCheck();
            var autoRevalidate = EditorGUILayout.Toggle(
                new GUIContent("导入器自动重新验证", "打开 .msp 资源 Inspector 时是否自动执行一次验证。"),
                MspEditorSettings.ImporterAutoRevalidate);
            if (EditorGUI.EndChangeCheck())
                MspEditorSettings.ImporterAutoRevalidate = autoRevalidate;

            EditorGUILayout.Space(12);
            if (GUILayout.Button("恢复默认", GUILayout.Width(120)))
            {
                MspEditorSettings.ResetToDefaults();
            }
        }
    }
}