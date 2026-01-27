using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MSpeaker.Runtime.Parser
{
    /// <summary>
    /// 解析 .msp 文本为对话结构。语法参考 Ibralogue：
    /// - 说话者: [Name]
    /// - 句子: 任意文本（可多行）
    /// - 分支: - Choice -> ConversationName
    /// - 注释: # 开头整行
    /// - 元数据: 行内 ## key:value foo:bar flag
    /// - 调用: {{Func}}（行内，会从文本中移除并记录插入点；运行时可调用静态方法）
    /// - 会话名: {{DialogueName(Name)}} 或 {{ConversationName(Name)}}
    /// - 头像: {{Image(ResourcesRelativePath)}}
    /// </summary>
    public static class MspDialogueParser
    {
        private static readonly Regex LineCommentRegex = new Regex(@"^\s*#.*");
        private static readonly Regex SpeakerRegex = new Regex(@"^\s*\[(.+?)\]\s*$");
        private static readonly Regex ChoiceRegex = new Regex(@"^\s*-\s*(.+?)\s*->\s*(.+?)\s*$");
        private static readonly Regex VariableRegex = new Regex(@"\$[a-zA-Z0-9_]+");

        private static readonly Regex MetadataRegex = new Regex(@"##.*");
        private static readonly Regex FunctionRegex = new Regex(@"{{(.+?)}}");
        private static readonly Regex ArgumentInvocationLineRegex =
            new Regex(@"^\s*{{\s*(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*\((?<arg>.*)\)\s*}}\s*$");

        private enum Token
        {
            Comment,
            Speaker,
            Sentence,
            Choice,
            DialogueNameInvoke,
            ImageInvoke
        }

        public static List<MspConversation> Parse(MspDialogueAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            string content = asset.Content ?? string.Empty;
            // 兼容 Windows 行尾
            string[] lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            var conversations = new List<MspConversation>();
            var currentConversation = NewConversation("Default");

            var sentenceParts = new List<MspLineContent>();
            var currentLine = NewLine();

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                Token token = GetToken(raw, out string argInvocationName, out string argInvocationArg);

                switch (token)
                {
                    case Token.Comment:
                        continue;

                    case Token.DialogueNameInvoke:
                    {
                        string name = ReplaceGlobalVariables(argInvocationArg);

                        // 如果是文件开头且尚未产生任何有效内容，则直接重命名当前会话
                        if (currentConversation.Name == "Default" &&
                            currentConversation.Lines.Count == 0 &&
                            !HasMeaningfulLine(currentLine, sentenceParts))
                        {
                            currentConversation.Name = name;
                            continue;
                        }

                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                        FlushConversation(conversations, ref currentConversation);

                        currentConversation = NewConversation(name);
                        currentLine = NewLine();
                        sentenceParts.Clear();
                        continue;
                    }

                    case Token.ImageInvoke:
                    {
                        string path = ReplaceGlobalVariables(argInvocationArg);
                        Sprite sprite = Resources.Load<Sprite>(path);
                        if (sprite == null)
                            MspDialogueLogger.LogError(i + 1, $"找不到图片资源：Resources/{path}（{{{{Image(...)}}}}）", asset);

                        currentLine.SpeakerImage = sprite;
                        continue;
                    }

                    case Token.Speaker:
                    {
                        string speaker = ReplaceGlobalVariables(ExtractSpeaker(raw));
                        if (string.IsNullOrWhiteSpace(speaker))
                            MspDialogueLogger.LogWarning(i + 1, "检测到空的 Speaker 名称。", asset);

                        if (string.IsNullOrEmpty(currentLine.Speaker))
                        {
                            currentLine.Speaker = speaker;
                        }
                        else
                        {
                            FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                            currentLine = NewLine();
                            currentLine.Speaker = speaker;
                            sentenceParts.Clear();
                        }

                        continue;
                    }

                    case Token.Choice:
                    {
                        if (currentConversation.Choices == null)
                            currentConversation.Choices = new Dictionary<MspChoice, int>();

                        var (choiceText, targetConversation, metadata) = ParseChoice(raw);
                        var choice = new MspChoice
                        {
                            ChoiceName = ReplaceGlobalVariables(choiceText),
                            LeadingConversationName = ReplaceGlobalVariables(targetConversation),
                            Metadata = metadata
                        };

                        // Choice 锚定到“当前行索引”（当前行尚未 flush 时，Lines.Count 就是该行的 index）
                        int anchorLineIndex = currentConversation.Lines.Count;
                        currentConversation.Choices.Add(choice, anchorLineIndex);
                        continue;
                    }

                    case Token.Sentence:
                    default:
                    {
                        string processed = ReplaceGlobalVariables(RemoveMetadataAndInvocationsFromSentence(raw));
                        if (sentenceParts.Count == 0 && string.IsNullOrWhiteSpace(processed))
                            continue;

                        sentenceParts.Add(new MspLineContent
                        {
                            Text = processed,
                            Invocations = GatherInlineFunctionInvocations(raw),
                            Metadata = GatherMetadata(raw)
                        });

                        continue;
                    }
                }
            }

            FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
            if (currentConversation.Lines.Count > 0) conversations.Add(currentConversation);

            if (conversations.Count == 0)
                throw new ArgumentException("Dialogue has no conversations", nameof(asset));

            return conversations;
        }

        private static Token GetToken(string rawLine, out string invocationName, out string invocationArg)
        {
            invocationName = null;
            invocationArg = null;

            if (LineCommentRegex.IsMatch(rawLine))
                return Token.Comment;

            if (SpeakerRegex.IsMatch(rawLine))
                return Token.Speaker;

            if (ChoiceRegex.IsMatch(rawLine))
                return Token.Choice;

            Match argLineMatch = ArgumentInvocationLineRegex.Match(rawLine);
            if (argLineMatch.Success)
            {
                invocationName = argLineMatch.Groups["name"].Value?.Trim();
                invocationArg = argLineMatch.Groups["arg"].Value?.Trim();

                if (string.Equals(invocationName, "Image", StringComparison.OrdinalIgnoreCase))
                    return Token.ImageInvoke;

                // Ibralogue 文档/测试里用 DialogueName；为了兼容，也支持 ConversationName
                if (string.Equals(invocationName, "DialogueName", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(invocationName, "ConversationName", StringComparison.OrdinalIgnoreCase))
                    return Token.DialogueNameInvoke;
            }

            return Token.Sentence;
        }

        private static string ExtractSpeaker(string rawLine)
        {
            Match m = SpeakerRegex.Match(rawLine);
            return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
        }

        private static (string choiceText, string targetConversation, Dictionary<string, string> metadata) ParseChoice(string rawLine)
        {
            // 先解析 metadata（不影响 choice 文本）
            Dictionary<string, string> metadata = GatherMetadata(rawLine);

            // 去掉所有 metadata，再按 "->" 分割
            string lineWithoutMetadata = MetadataRegex.Replace(rawLine, string.Empty).Trim();
            Match m = ChoiceRegex.Match(lineWithoutMetadata);
            if (!m.Success)
                return (lineWithoutMetadata, string.Empty, metadata);

            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim(), metadata);
        }

        private static string RemoveMetadataAndInvocationsFromSentence(string rawLine)
        {
            string line = rawLine ?? string.Empty;

            // 移除行内 {{...}}（用于插入点/函数调用）
            line = FunctionRegex.Replace(line, string.Empty);

            // 移除行内 ## ...
            line = MetadataRegex.Replace(line, string.Empty);

            return line;
        }

        private static string ReplaceGlobalVariables(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;

            return VariableRegex.Replace(line, match =>
            {
                string key = match.Value.Trim().TrimStart('$');
                if (MspDialogueGlobals.GlobalVariables.TryGetValue(key, out string value))
                    return value ?? string.Empty;

                MspDialogueLogger.LogWarning(-1, $"检测到变量 ${key}，但 GlobalVariables 中没有对应条目。");
                return match.Value;
            });
        }

        private static Dictionary<int, string> GatherInlineFunctionInvocations(string rawLine)
        {
            var invocations = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(rawLine)) return invocations;

            foreach (Match match in FunctionRegex.Matches(rawLine))
            {
                string full = match.Value;
                int index = rawLine.IndexOf(full, StringComparison.Ordinal);

                string name = full;
                if (name.Length >= 4)
                    name = name.Substring(2, name.Length - 4).Trim();

                // 行级的 {{Image(...)}} / {{DialogueName(...)}} 在上面作为 Token 处理；
                // 行内出现时，这里只做普通 invocation 记录（运行时不会自动解析参数）
                invocations[index] = name;
            }

            return invocations;
        }

        private static Dictionary<string, string> GatherMetadata(string rawLine)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(rawLine)) return result;

            foreach (Match match in MetadataRegex.Matches(rawLine))
            {
                string comment = match.Value.Trim();
                comment = comment.Replace("##", "").Trim();
                if (string.IsNullOrEmpty(comment)) continue;

                foreach (string token in comment.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int colonIndex = token.IndexOf(':');
                    if (colonIndex <= 0 || colonIndex >= token.Length - 1)
                    {
                        if (!result.ContainsKey(token)) result[token] = token;
                        continue;
                    }

                    string key = token.Substring(0, colonIndex);
                    string value = token.Substring(colonIndex + 1);
                    if (!result.ContainsKey(key)) result[key] = value;
                }
            }

            return result;
        }

        private static MspConversation NewConversation(string name)
        {
            return new MspConversation
            {
                Name = name,
                Lines = new List<MspLine>()
            };
        }

        private static MspLine NewLine()
        {
            return new MspLine
            {
                LineContent = new MspLineContent
                {
                    Invocations = new Dictionary<int, string>(),
                    Metadata = new Dictionary<string, string>()
                }
            };
        }

        private static bool HasMeaningfulLine(MspLine line, List<MspLineContent> sentenceParts)
        {
            if (!string.IsNullOrEmpty(line.Speaker)) return true;
            if (line.SpeakerImage != null) return true;
            if (sentenceParts.Any(p => !string.IsNullOrWhiteSpace(p.Text))) return true;
            return false;
        }

        private static void FlushLineIntoConversation(MspConversation conversation, ref MspLine currentLine, List<MspLineContent> sentenceParts)
        {
            if (!HasMeaningfulLine(currentLine, sentenceParts))
                return;

            // 合并文本（保留换行）
            currentLine.LineContent.Text = string.Join("\n", sentenceParts.Select(p => p.Text)).TrimEnd('\n');

            // 合并 invocation（按文本拼接长度计算偏移）
            int offset = 0;
            for (int i = 0; i < sentenceParts.Count; i++)
            {
                MspLineContent part = sentenceParts[i];
                foreach (var kv in part.Invocations)
                {
                    int adjustedIndex = Mathf.Clamp(offset + kv.Key, 0, int.MaxValue);
                    if (!currentLine.LineContent.Invocations.ContainsKey(adjustedIndex))
                        currentLine.LineContent.Invocations.Add(adjustedIndex, kv.Value);
                }

                foreach (var kv in part.Metadata)
                {
                    if (!currentLine.LineContent.Metadata.ContainsKey(kv.Key))
                        currentLine.LineContent.Metadata.Add(kv.Key, kv.Value);
                }

                offset += (part.Text ?? string.Empty).Length;
                if (i < sentenceParts.Count - 1) offset += 1; // "\n"
            }

            // 确保 Speaker 为空时也能跑（例如纯旁白）
            conversation.Lines.Add(currentLine);

            currentLine = NewLine();
            sentenceParts.Clear();
        }

        private static void FlushConversation(List<MspConversation> conversations, ref MspConversation conversation)
        {
            if (conversation.Lines.Count > 0)
                conversations.Add(conversation);
        }
    }
}

