using UnityEditor;
using UnityEngine;

namespace MSpeaker.Editor
{
    public static class MspEditorSettings
    {
        private const string PrefsPrefix = "MSpeaker.Editor.";

        private const string KeyTemplatePath = PrefsPrefix + "TemplatePath";
        private const string KeyTypewriterCharsPerSecond = PrefsPrefix + "TypewriterCharsPerSecond";
        private const string KeyValidationShowInfo = PrefsPrefix + "ValidationShowInfo";
        private const string KeyImporterAutoRevalidate = PrefsPrefix + "ImporterAutoRevalidate";

        /// <summary>新建 .msp 时使用的模板文件路径（相对于项目根）。</summary>
        public const string DefaultTemplatePath = "Assets/MSpeaker/Editor/Templates/DefaultDialogue.msp.txt";

        /// <summary>打字机视图默认每秒字符数。</summary>
        public const float DefaultTypewriterCharsPerSecond = 40f;

        public static string TemplatePath
        {
            get => EditorPrefs.GetString(KeyTemplatePath, DefaultTemplatePath);
            set => EditorPrefs.SetString(KeyTemplatePath, value ?? DefaultTemplatePath);
        }

        public static float TypewriterCharsPerSecond
        {
            get => EditorPrefs.GetFloat(KeyTypewriterCharsPerSecond, DefaultTypewriterCharsPerSecond);
            set => EditorPrefs.SetFloat(KeyTypewriterCharsPerSecond, Mathf.Clamp(value, 1f, 500f));
        }

        /// <summary>验证结果中是否显示“信息”级别。</summary>
        public static bool ValidationShowInfo
        {
            get => EditorPrefs.GetBool(KeyValidationShowInfo, true);
            set => EditorPrefs.SetBool(KeyValidationShowInfo, value);
        }

        /// <summary>导入器在 Inspector 打开时是否自动重新验证。</summary>
        public static bool ImporterAutoRevalidate
        {
            get => EditorPrefs.GetBool(KeyImporterAutoRevalidate, true);
            set => EditorPrefs.SetBool(KeyImporterAutoRevalidate, value);
        }

        public static void ResetToDefaults()
        {
            TemplatePath = DefaultTemplatePath;
            TypewriterCharsPerSecond = DefaultTypewriterCharsPerSecond;
            ValidationShowInfo = true;
            ImporterAutoRevalidate = true;
        }
    }
}