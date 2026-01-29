using System.IO;
using System.Linq;
using MSpeaker.Runtime;
using MSpeaker.Runtime.Parser;
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
        private Vector2 _validationScrollPosition;
        private Vector2 _previewScrollPosition;
        private MspDialogueValidator.ValidationResult _validationResult;
        private bool _showValidation = true;
        private bool _showPreview = false;
        private bool _validationDirty = true;

        public override void OnEnable()
        {
            base.OnEnable();
            var importer = (MspDialogueImporter)target;
            if (File.Exists(importer.assetPath))
            {
                _filePreview = File.ReadAllText(importer.assetPath);
            }

            _validationDirty = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var importer = (MspDialogueImporter)target;
            var asset = AssetDatabase.LoadAssetAtPath<MspDialogueAsset>(importer.assetPath);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MSpeaker Dialogue Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            if (_validationDirty && asset != null)
            {
                try
                {
                    _validationResult = MspDialogueValidator.Validate(asset);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MSpeaker] 验证失败: {ex.Message}");
                    _validationResult = new MspDialogueValidator.ValidationResult();
                    _validationResult.Issues.Add(new MspDialogueValidator.ValidationIssue(
                        0,
                        MspDialogueValidator.ValidationSeverity.Error,
                        $"验证错误: {ex.Message}"));
                }

                _validationDirty = false;
            }

            _showValidation = EditorGUILayout.Foldout(_showValidation,
                $"验证结果 ({GetValidationSummary()})", true);

            if (_showValidation)
            {
                DrawValidationSection();
            }

            EditorGUILayout.Space();

            _showPreview = EditorGUILayout.Foldout(_showPreview,
                $"对话预览 ({(_validationResult?.ParsedConversations?.Count ?? 0)} 个对话)", true);

            if (_showPreview && _validationResult is { ParsedConversations: not null })
            {
                DrawPreviewSection();
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("文件内容 (.msp)", EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition, GUILayout.MinHeight(200)))
            {
                _scrollPosition = scroll.scrollPosition;
                EditorGUILayout.SelectableLabel(_filePreview ?? "", EditorStyles.textArea,
                    GUILayout.ExpandHeight(true));
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重新验证"))
            {
                _validationDirty = true;
            }

            if (GUILayout.Button("打开预览窗口"))
            {
                MspDialoguePreviewWindow.ShowWindow(asset);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            ApplyRevertGUI();
        }

        private string GetValidationSummary()
        {
            if (_validationResult == null) return "未验证";

            var errorCount =
                _validationResult.Issues.Count(i => i.Severity == MspDialogueValidator.ValidationSeverity.Error);
            var warningCount =
                _validationResult.Issues.Count(i => i.Severity == MspDialogueValidator.ValidationSeverity.Warning);
            var infoCount =
                _validationResult.Issues.Count(i => i.Severity == MspDialogueValidator.ValidationSeverity.Info);

            if (errorCount > 0)
                return $"{errorCount} 错误, {warningCount} 警告, {infoCount} 信息";
            if (warningCount > 0)
                return $"{warningCount} 警告, {infoCount} 信息";
            if (infoCount > 0)
                return $"{infoCount} 信息";

            return "无问题";
        }

        private void DrawValidationSection()
        {
            if (_validationResult == null)
            {
                EditorGUILayout.HelpBox("点击重新验证按钮进行验证", MessageType.Info);
                return;
            }

            if (_validationResult.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("验证通过，未发现任何问题", MessageType.Info);
                return;
            }

            using var scroll =
                new EditorGUILayout.ScrollViewScope(_validationScrollPosition, GUILayout.MaxHeight(300));
            _validationScrollPosition = scroll.scrollPosition;

            foreach (var issue in _validationResult.Issues.OrderBy(i =>
                         i.Severity == MspDialogueValidator.ValidationSeverity.Error ? 0 :
                         i.Severity == MspDialogueValidator.ValidationSeverity.Warning ? 1 : 2))
            {
                var messageType = issue.Severity == MspDialogueValidator.ValidationSeverity.Error
                    ? MessageType.Error
                    : issue.Severity == MspDialogueValidator.ValidationSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;

                var lineInfo = issue.LineNumber > 0 ? $" (第 {issue.LineNumber} 行)" : "";
                var message = $"{issue.Message}{lineInfo}";

                if (!string.IsNullOrEmpty(issue.CodeSnippet))
                {
                    message += $"\n代码: {issue.CodeSnippet}";
                }

                EditorGUILayout.HelpBox(message, messageType);
            }
        }

        private void DrawPreviewSection()
        {
            if (_validationResult?.ParsedConversations == null || _validationResult.ParsedConversations.Count == 0)
            {
                EditorGUILayout.HelpBox("没有可预览的对话", MessageType.Info);
                return;
            }

            using var scroll = new EditorGUILayout.ScrollViewScope(_previewScrollPosition, GUILayout.MaxHeight(400));
            _previewScrollPosition = scroll.scrollPosition;

            foreach (var conversation in _validationResult.ParsedConversations)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"对话: {conversation.Name}", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;

                var lineCount = conversation.Lines?.Count ?? 0;
                var choiceCount = conversation.Choices?.Count ?? 0;
                var labelCount = conversation.Labels?.Count ?? 0;

                EditorGUILayout.LabelField($"行数: {lineCount} | 选择: {choiceCount} | 标签: {labelCount}",
                    EditorStyles.miniLabel);

                if (conversation.Lines is { Count: > 0 })
                {
                    EditorGUILayout.LabelField("前几行预览:", EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;

                    var previewLines = conversation.Lines.Take(5);
                    foreach (var line in previewLines)
                    {
                        if (line.LineType == MspLineType.Normal &&
                            !string.IsNullOrWhiteSpace(line.LineContent?.Text))
                        {
                            var speaker = string.IsNullOrEmpty(line.Speaker) ? "[无说话者]" : $"[{line.Speaker}]";
                            var text = line.LineContent.Text.Length > 50
                                ? line.LineContent.Text.Substring(0, 50) + "..."
                                : line.LineContent.Text;
                            EditorGUILayout.LabelField($"{speaker} {text}", EditorStyles.wordWrappedMiniLabel);
                        }
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
        }
    }
}