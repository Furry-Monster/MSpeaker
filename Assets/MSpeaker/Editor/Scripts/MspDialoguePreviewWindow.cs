using System.Collections.Generic;
using System.Linq;
using MSpeaker.Runtime;
using MSpeaker.Runtime.Parser;
using UnityEditor;
using UnityEngine;

namespace MSpeaker.Editor
{
    public class MspDialoguePreviewWindow : EditorWindow
    {
        private MspDialogueAsset _currentAsset;
        private List<MspConversation> _conversations;
        private Vector2 _conversationScrollPosition;
        private Vector2 _contentScrollPosition;
        private int _selectedConversationIndex = -1;
        private MspConversation _selectedConversation;
        private bool _showStatistics = true;
        private bool _showRawContent = false;
        private Dictionary<int, MspConditionalBlock> _cachedConditionalBlocks;
        private string _lastAssetContentHash;

        [MenuItem("Window/MSpeaker/对话预览窗口")]
        public static void ShowWindow()
        {
            var window = GetWindow<MspDialoguePreviewWindow>("对话预览");
            window.minSize = new Vector2(800, 600);
        }

        public static void ShowWindow(MspDialogueAsset asset)
        {
            var window = GetWindow<MspDialoguePreviewWindow>("对话预览");
            window.minSize = new Vector2(800, 600);
            window.SetAsset(asset);
        }

        private void SetAsset(MspDialogueAsset asset)
        {
            _currentAsset = asset;
            if (asset != null)
            {
                var contentHash = asset.Content?.GetHashCode().ToString() ?? "";
                if (_conversations != null && _lastAssetContentHash == contentHash)
                {
                    return;
                }

                try
                {
                    _conversations = MspDialogueParser.Parse(asset);
                    _lastAssetContentHash = contentHash;
                    _selectedConversationIndex = _conversations.Count > 0 ? 0 : -1;
                    _selectedConversation = _selectedConversationIndex >= 0
                        ? _conversations[_selectedConversationIndex]
                        : null;
                    _cachedConditionalBlocks = _selectedConversation?.ConditionalBlocks;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MSpeaker] 解析失败: {ex.Message}");
                    _conversations = null;
                    _selectedConversationIndex = -1;
                    _selectedConversation = null;
                    _cachedConditionalBlocks = null;
                    _lastAssetContentHash = null;
                }
            }
            else
            {
                _conversations = null;
                _selectedConversationIndex = -1;
                _selectedConversation = null;
                _cachedConditionalBlocks = null;
                _lastAssetContentHash = null;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            DrawConversationList();
            DrawConversationContent();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConversationList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));

            EditorGUILayout.LabelField("对话列表", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            var newAsset = (MspDialogueAsset)EditorGUILayout.ObjectField(
                _currentAsset, typeof(MspDialogueAsset), false);
            if (newAsset != _currentAsset)
            {
                SetAsset(newAsset);
            }

            if (GUILayout.Button("刷新", GUILayout.Width(50)))
            {
                if (_currentAsset != null)
                {
                    _lastAssetContentHash = null;
                    SetAsset(_currentAsset);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (_conversations == null || _conversations.Count == 0)
            {
                EditorGUILayout.HelpBox("没有可显示的对话。请选择一个 .msp 资源文件。", MessageType.Info);
            }
            else
            {
                using var scroll = new EditorGUILayout.ScrollViewScope(_conversationScrollPosition);
                _conversationScrollPosition = scroll.scrollPosition;

                for (var i = 0; i < _conversations.Count; i++)
                {
                    var conversation = _conversations[i];
                    var isSelected = i == _selectedConversationIndex;

                    var style = isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                    if (GUILayout.Button($"{i + 1}. {conversation.Name}", style))
                    {
                        _selectedConversationIndex = i;
                        _selectedConversation = conversation;
                        _cachedConditionalBlocks = conversation.ConditionalBlocks;
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConversationContent()
        {
            EditorGUILayout.BeginVertical();

            if (_selectedConversation == null)
            {
                EditorGUILayout.HelpBox("请从左侧列表中选择一个对话", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField($"对话: {_selectedConversation.Name}", EditorStyles.largeLabel);
            EditorGUILayout.Space();
            _showStatistics = EditorGUILayout.Foldout(_showStatistics, "统计信息", true);
            if (_showStatistics)
            {
                EditorGUI.indentLevel++;
                var lineCount = _selectedConversation.Lines?.Count ?? 0;
                var choiceCount = _selectedConversation.Choices?.Count ?? 0;
                var labelCount = _selectedConversation.Labels?.Count ?? 0;
                var conditionalBlockCount = _selectedConversation.ConditionalBlocks?.Count ?? 0;

                EditorGUILayout.LabelField($"总行数: {lineCount}");
                EditorGUILayout.LabelField($"选择分支: {choiceCount}");
                EditorGUILayout.LabelField($"标签: {labelCount}");
                EditorGUILayout.LabelField($"条件块: {conditionalBlockCount}");

                if (_selectedConversation.Lines != null)
                {
                    var normalLines = _selectedConversation.Lines.Count(line => line.LineType == MspLineType.Normal);
                    var controlLines = _selectedConversation.Lines.Count - normalLines;
                    EditorGUILayout.LabelField($"普通对话行: {normalLines}");
                    EditorGUILayout.LabelField($"控制流行: {controlLines}");
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            if (_selectedConversation.Labels is { Count: > 0 })
            {
                EditorGUILayout.LabelField("标签:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var label in _selectedConversation.Labels)
                {
                    EditorGUILayout.LabelField($"{label.Key} -> 行 {label.Value}", EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            // 选择分支
            if (_selectedConversation.Choices is { Count: > 0 })
            {
                EditorGUILayout.LabelField("选择分支:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var choice in _selectedConversation.Choices)
                {
                    var condition = string.IsNullOrEmpty(choice.Key.ConditionExpression)
                        ? ""
                        : $" [条件: {choice.Key.ConditionExpression}]";
                    EditorGUILayout.LabelField(
                        $"• {choice.Key.ChoiceName} -> {choice.Key.LeadingConversationName}{condition}",
                        EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("对话内容:", EditorStyles.boldLabel);

            _showRawContent = EditorGUILayout.Toggle("显示原始内容", _showRawContent);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_contentScrollPosition))
            {
                _contentScrollPosition = scroll.scrollPosition;

                if (_selectedConversation.Lines == null || _selectedConversation.Lines.Count == 0)
                {
                    EditorGUILayout.HelpBox("此对话没有内容", MessageType.Info);
                }
                else
                {
                    for (var i = 0; i < _selectedConversation.Lines.Count; i++)
                    {
                        var line = _selectedConversation.Lines[i];
                        DrawLine(i, line);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLine(int index, MspLine line)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"行 {index}", EditorStyles.miniLabel, GUILayout.Width(50));
            var typeColor = GetLineTypeColor(line.LineType);
            var originalColor = GUI.color;
            GUI.color = typeColor;
            EditorGUILayout.LabelField($"[{line.LineType}]", EditorStyles.miniLabel, GUILayout.Width(100));
            GUI.color = originalColor;

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(line.Speaker))
            {
                EditorGUILayout.LabelField($"说话者: {line.Speaker}", EditorStyles.boldLabel);
            }

            if (line.LineType == MspLineType.Normal)
            {
                if (!string.IsNullOrWhiteSpace(line.LineContent?.Text))
                {
                    if (_showRawContent)
                    {
                        EditorGUILayout.TextArea(line.LineContent.Text, EditorStyles.wordWrappedLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(line.LineContent.Text, EditorStyles.wordWrappedLabel);
                    }
                }

                if (line.LineContent?.Invocations is { Count: > 0 })
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("函数调用:", EditorStyles.miniLabel);
                    foreach (var inv in line.LineContent.Invocations.Values)
                    {
                        var args = inv.Arguments is { Count: > 0 }
                            ? string.Join(", ", inv.Arguments.Select(a => a.RawValue))
                            : "";
                        var funcCall = string.IsNullOrEmpty(args)
                            ? $"{{{{ {inv.FunctionName} }}}}"
                            : $"{{{{ {inv.FunctionName}({args}) }}}}";
                        EditorGUILayout.LabelField($"  • {funcCall}", EditorStyles.miniLabel);
                    }

                    EditorGUI.indentLevel--;
                }

                if (line.LineContent?.Metadata is { Count: > 0 })
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("元数据:", EditorStyles.miniLabel);
                    foreach (var meta in line.LineContent.Metadata)
                    {
                        EditorGUILayout.LabelField($"  • {meta.Key}: {meta.Value}", EditorStyles.miniLabel);
                    }

                    EditorGUI.indentLevel--;
                }
            }
            else if (line.LineType == MspLineType.Goto)
            {
                EditorGUILayout.LabelField($"跳转到标签: {line.LabelName}", EditorStyles.wordWrappedLabel);
            }
            else if (line.LineType == MspLineType.Label)
            {
                EditorGUILayout.LabelField($"标签: {line.LabelName}", EditorStyles.wordWrappedLabel);
            }
            else if (line.LineType == MspLineType.LoopStart)
            {
                var count = line.LoopInfo?.LoopCountExpression ?? "?";
                EditorGUILayout.LabelField($"循环开始: {count} 次", EditorStyles.wordWrappedLabel);
            }
            else if (line.LineType == MspLineType.LoopEnd)
            {
                EditorGUILayout.LabelField("循环结束", EditorStyles.wordWrappedLabel);
            }
            else if (line.LineType == MspLineType.IfStart)
            {
                if (_cachedConditionalBlocks != null && _cachedConditionalBlocks.TryGetValue(index, out var block))
                {
                    EditorGUILayout.LabelField($"条件: {block.ConditionExpression}", EditorStyles.wordWrappedLabel);
                }
            }
            else if (line.LineType == MspLineType.Else)
            {
                EditorGUILayout.LabelField("Else 分支", EditorStyles.wordWrappedLabel);
            }
            else if (line.LineType == MspLineType.EndIf)
            {
                EditorGUILayout.LabelField("EndIf", EditorStyles.wordWrappedLabel);
            }

            if (!string.IsNullOrEmpty(line.SpeakerImagePath))
            {
                EditorGUILayout.LabelField($"图片: {line.SpeakerImagePath}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private static Color GetLineTypeColor(MspLineType lineType)
        {
            return lineType switch
            {
                MspLineType.Normal => Color.white,
                MspLineType.Label => new Color(0.5f, 0.8f, 1f),
                MspLineType.Goto => new Color(0.8f, 0.5f, 1f),
                MspLineType.LoopStart or MspLineType.LoopEnd => new Color(1f, 0.8f, 0.5f),
                MspLineType.IfStart or MspLineType.Else or MspLineType.EndIf => new Color(0.5f, 1f, 0.8f),
                _ => Color.gray
            };
        }
    }
}