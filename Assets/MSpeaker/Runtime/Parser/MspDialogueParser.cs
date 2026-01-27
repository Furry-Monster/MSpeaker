using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MSpeaker.Runtime.Utils;
using UnityEngine;

namespace MSpeaker.Runtime.Parser
{
    /// <summary>
    /// 解析 .msp 文本为对话结构：
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
        private static readonly Regex LineCommentRegex = new(@"^\s*#.*");
        private static readonly Regex SpeakerRegex = new(@"^\s*\[(.+?)\]\s*$");
        private static readonly Regex ChoiceRegex = new(@"^\s*-\s*(.+?)\s*->\s*(.+?)\s*$");
        private static readonly Regex VariableRegex = new(@"\$[a-zA-Z0-9_]+");

        private static readonly Regex MetadataRegex = new(@"##.*");
        private static readonly Regex FunctionRegex = new(@"{{(.+?)}}");

        private static readonly Regex ArgumentInvocationLineRegex =
            new(@"^\s*{{\s*(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*\((?<arg>.*)\)\s*}}\s*$");

        private static readonly Regex IfRegex = new(@"^\s*{{\s*If\s*\((.+?)\)\s*}}\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex IfVarRegex = new(@"^\s*{{\s*IfVar\s*\((.+?)\)\s*}}\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex ElseRegex = new(@"^\s*{{\s*Else\s*}}\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex EndIfRegex = new(@"^\s*{{\s*EndIf\s*}}\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex LabelRegex = new(@"^\s*{{\s*Label\s*\((.+?)\)\s*}}\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex GotoRegex = new(@"^\s*{{\s*Goto\s*\((.+?)\)\s*}}\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex LoopRegex = new(@"^\s*{{\s*Loop\s*\((.+?)\)\s*}}\s*$", RegexOptions.IgnoreCase);

        private enum Token
        {
            Comment,
            Speaker,
            Sentence,
            Choice,
            DialogueNameInvoke,
            ImageInvoke,
            IfStart,
            IfVarStart,
            Else,
            EndIf,
            Label,
            Goto,
            LoopStart
        }

        public static List<MspConversation> Parse(MspDialogueAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var content = asset.Content ?? string.Empty;
            // 兼容 Windows 行尾
            var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            var conversations = new List<MspConversation>();
            var currentConversation = NewConversation("Default");

            var sentenceParts = new List<MspLineContent>();
            var currentLine = NewLine();

            // 条件分支栈，用于匹配 If/Else/EndIf
            var conditionalStack = new Stack<MspConditionalBlock>();
            // 循环栈，用于匹配 Loop
            var loopStack = new Stack<MspLoopInfo>();

            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var token = GetToken(raw, out var argInvocationName, out var argInvocationArg);

                switch (token)
                {
                    case Token.Comment:
                        continue;

                    case Token.IfStart:
                    {
                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                        var condition = ReplaceGlobalVariables(argInvocationArg);
                        var block = new MspConditionalBlock
                        {
                            ConditionType = MspConditionType.If,
                            ConditionExpression = condition,
                            IfStartLineIndex = currentConversation.Lines.Count,
                            ElseLineIndex = -1,
                            EndIfLineIndex = -1
                        };
                        conditionalStack.Push(block);
                        currentConversation.ConditionalBlocks ??= new Dictionary<int, MspConditionalBlock>();
                        currentConversation.ConditionalBlocks[block.IfStartLineIndex] = block;

                        currentLine = NewLine();
                        currentLine.LineType = MspLineType.IfStart;
                        currentLine.LineContent.Text = string.Empty;
                        currentConversation.Lines.Add(currentLine);
                        currentLine = NewLine();
                        continue;
                    }

                    case Token.IfVarStart:
                    {
                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                        var condition = ReplaceGlobalVariables(argInvocationArg);
                        var block = new MspConditionalBlock
                        {
                            ConditionType = MspConditionType.IfVar,
                            ConditionExpression = condition,
                            IfStartLineIndex = currentConversation.Lines.Count,
                            ElseLineIndex = -1,
                            EndIfLineIndex = -1
                        };
                        conditionalStack.Push(block);
                        currentConversation.ConditionalBlocks ??= new Dictionary<int, MspConditionalBlock>();
                        currentConversation.ConditionalBlocks[block.IfStartLineIndex] = block;

                        currentLine = NewLine();
                        currentLine.LineType = MspLineType.IfStart;
                        currentLine.LineContent.Text = string.Empty;
                        currentConversation.Lines.Add(currentLine);
                        currentLine = NewLine();
                        continue;
                    }

                    case Token.Else:
                    {
                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                        if (conditionalStack.Count == 0)
                        {
                            MspDialogueLogger.LogError(i + 1, "发现 {{Else}} 但没有对应的 {{If}} 或 {{IfVar}}。", asset);
                            continue;
                        }

                        var block = conditionalStack.Peek();
                        block.ElseLineIndex = currentConversation.Lines.Count;

                        currentLine = NewLine();
                        currentLine.LineType = MspLineType.Else;
                        currentLine.LineContent.Text = string.Empty;
                        currentConversation.Lines.Add(currentLine);
                        currentLine = NewLine();
                        continue;
                    }

                    case Token.EndIf:
                    {
                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                        if (conditionalStack.Count == 0)
                        {
                            MspDialogueLogger.LogError(i + 1, "发现 {{EndIf}} 但没有对应的 {{If}} 或 {{IfVar}}。", asset);
                            continue;
                        }

                        var block = conditionalStack.Pop();
                        block.EndIfLineIndex = currentConversation.Lines.Count;

                        currentLine = NewLine();
                        currentLine.LineType = MspLineType.EndIf;
                        currentLine.LineContent.Text = string.Empty;
                        currentConversation.Lines.Add(currentLine);
                        currentLine = NewLine();
                        continue;
                    }

                    case Token.Label:
                    {
                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                        var labelName = ReplaceGlobalVariables(argInvocationArg);
                        currentConversation.Labels ??= new Dictionary<string, int>();
                        currentConversation.Labels[labelName] = currentConversation.Lines.Count;

                        currentLine = NewLine();
                        currentLine.LineType = MspLineType.Label;
                        currentLine.LabelName = labelName;
                        currentLine.LineContent.Text = string.Empty;
                        currentConversation.Lines.Add(currentLine);
                        currentLine = NewLine();
                        continue;
                    }

                    case Token.Goto:
                    {
                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                        var labelName = ReplaceGlobalVariables(argInvocationArg);

                        currentLine = NewLine();
                        currentLine.LineType = MspLineType.Goto;
                        currentLine.LabelName = labelName;
                        currentLine.LineContent.Text = string.Empty;
                        currentConversation.Lines.Add(currentLine);
                        currentLine = NewLine();
                        continue;
                    }

                    case Token.LoopStart:
                    {
                        // 关闭之前的循环（如果有）
                        if (loopStack.Count > 0)
                        {
                            var prevLoop = loopStack.Pop();
                            prevLoop.LoopEndLineIndex = currentConversation.Lines.Count;

                            var endLine = NewLine();
                            endLine.LineType = MspLineType.LoopEnd;
                            endLine.LineContent.Text = string.Empty;
                            currentConversation.Lines.Add(endLine);
                        }

                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                        var countStr = ReplaceGlobalVariables(argInvocationArg);
                        if (!int.TryParse(countStr, out var loopCount))
                        {
                            MspDialogueLogger.LogError(i + 1, $"{{{{Loop({countStr})}}}} 中的参数必须是整数。", asset);
                            loopCount = 1;
                        }

                        var loopInfo = new MspLoopInfo
                        {
                            LoopCount = loopCount,
                            LoopStartLineIndex = currentConversation.Lines.Count,
                            LoopEndLineIndex = -1
                        };
                        loopStack.Push(loopInfo);

                        currentLine = NewLine();
                        currentLine.LineType = MspLineType.LoopStart;
                        currentLine.LoopInfo = loopInfo;
                        currentLine.LineContent.Text = string.Empty;
                        currentConversation.Lines.Add(currentLine);
                        currentLine = NewLine();
                        continue;
                    }

                    case Token.DialogueNameInvoke:
                    {
                        var name = ReplaceGlobalVariables(argInvocationArg);

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
                        var path = ReplaceGlobalVariables(argInvocationArg);
                        var sprite = Resources.Load<Sprite>(path);
                        if (sprite == null)
                            MspDialogueLogger.LogError(i + 1, $"找不到图片资源：Resources/{path}（{{{{Image(...)}}}}）", asset);

                        currentLine.SpeakerImage = sprite;
                        continue;
                    }

                    case Token.Speaker:
                    {
                        var speaker = ReplaceGlobalVariables(ExtractSpeaker(raw));
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
                        currentConversation.Choices ??= new Dictionary<MspChoice, int>();

                        var (choiceText, targetConversation, metadata) = ParseChoice(raw);
                        var choice = new MspChoice
                        {
                            ChoiceName = ReplaceGlobalVariables(choiceText),
                            LeadingConversationName = ReplaceGlobalVariables(targetConversation),
                            Metadata = metadata
                        };

                        // Choice 锚定到“当前行索引”（当前行尚未 flush 时，Lines.Count 就是该行的 index）
                        var anchorLineIndex = currentConversation.Lines.Count;
                        currentConversation.Choices.Add(choice, anchorLineIndex);
                        continue;
                    }

                    case Token.Sentence:
                    default:
                    {
                        var processed = ReplaceGlobalVariables(RemoveMetadataAndInvocationsFromSentence(raw));
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

            // 处理未关闭的循环（在对话结束时自动关闭）
            while (loopStack.Count > 0)
            {
                var loopInfo = loopStack.Pop();
                loopInfo.LoopEndLineIndex = currentConversation.Lines.Count;

                var endLine = NewLine();
                endLine.LineType = MspLineType.LoopEnd;
                endLine.LineContent.Text = string.Empty;
                currentConversation.Lines.Add(endLine);
            }

            // 检查未关闭的条件分支
            if (conditionalStack.Count > 0)
            {
                MspDialogueLogger.LogWarning(-1, $"发现 {conditionalStack.Count} 个未关闭的条件分支（缺少 {{EndIf}}）。", asset);
            }

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

            // 检查特殊指令（必须在 ArgumentInvocationLineRegex 之前检查，因为它们也是带参数的）
            var ifMatch = IfRegex.Match(rawLine);
            if (ifMatch.Success)
            {
                invocationArg = ifMatch.Groups[1].Value.Trim();
                return Token.IfStart;
            }

            var ifVarMatch = IfVarRegex.Match(rawLine);
            if (ifVarMatch.Success)
            {
                invocationArg = ifVarMatch.Groups[1].Value.Trim();
                return Token.IfVarStart;
            }

            if (ElseRegex.IsMatch(rawLine))
                return Token.Else;

            if (EndIfRegex.IsMatch(rawLine))
                return Token.EndIf;

            var labelMatch = LabelRegex.Match(rawLine);
            if (labelMatch.Success)
            {
                invocationArg = labelMatch.Groups[1].Value.Trim();
                return Token.Label;
            }

            var gotoMatch = GotoRegex.Match(rawLine);
            if (gotoMatch.Success)
            {
                invocationArg = gotoMatch.Groups[1].Value.Trim();
                return Token.Goto;
            }

            var loopMatch = LoopRegex.Match(rawLine);
            if (loopMatch.Success)
            {
                invocationArg = loopMatch.Groups[1].Value.Trim();
                return Token.LoopStart;
            }

            var argLineMatch = ArgumentInvocationLineRegex.Match(rawLine);
            if (argLineMatch.Success)
            {
                invocationName = argLineMatch.Groups["name"].Value.Trim();
                invocationArg = argLineMatch.Groups["arg"].Value.Trim();

                if (string.Equals(invocationName, "Image", StringComparison.OrdinalIgnoreCase))
                    return Token.ImageInvoke;

                if (string.Equals(invocationName, "DialogueName", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(invocationName, "ConversationName", StringComparison.OrdinalIgnoreCase))
                    return Token.DialogueNameInvoke;
            }

            return Token.Sentence;
        }

        private static string ExtractSpeaker(string rawLine)
        {
            var m = SpeakerRegex.Match(rawLine);
            return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
        }

        private static (string choiceText, string targetConversation, Dictionary<string, string> metadata) ParseChoice(
            string rawLine)
        {
            // 先解析 metadata（不影响 choice 文本）
            var metadata = GatherMetadata(rawLine);

            // 去掉所有 metadata，再按 "->" 分割
            var lineWithoutMetadata = MetadataRegex.Replace(rawLine, string.Empty).Trim();
            var m = ChoiceRegex.Match(lineWithoutMetadata);
            if (!m.Success)
                return (lineWithoutMetadata, string.Empty, metadata);

            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim(), metadata);
        }

        private static string RemoveMetadataAndInvocationsFromSentence(string rawLine)
        {
            var line = rawLine ?? string.Empty;

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
                var key = match.Value.Trim().TrimStart('$');
                if (MspDialogueGlobals.GlobalVariables.TryGetValue(key, out var value))
                    return value ?? string.Empty;

                MspDialogueLogger.LogWarning(-1, $"检测到变量 ${key}，但 GlobalVariables 中没有对应条目。");
                return match.Value;
            });
        }

        private static Dictionary<int, MspFunctionInvocation> GatherInlineFunctionInvocations(string rawLine)
        {
            var invocations = new Dictionary<int, MspFunctionInvocation>();
            if (string.IsNullOrEmpty(rawLine)) return invocations;

            // 使用循环和起始位置来准确查找所有匹配项，避免重复模式导致的索引错误
            var searchStartIndex = 0;
            foreach (Match match in FunctionRegex.Matches(rawLine))
            {
                var full = match.Value;
                // 从上次搜索位置开始查找，确保找到正确的匹配位置
                var index = rawLine.IndexOf(full, searchStartIndex, StringComparison.Ordinal);

                // 如果找到了匹配，更新搜索起始位置以避免重复匹配
                if (index >= 0)
                {
                    searchStartIndex = index + full.Length;

                    // 提取函数名和参数
                    var content = full.Substring(2, full.Length - 4).Trim(); // 去掉 {{ 和 }}

                    // 检查是否有参数
                    var parenIndex = content.IndexOf('(');
                    if (parenIndex >= 0 && content.EndsWith(")"))
                    {
                        // 带参数的函数调用
                        var funcName = content.Substring(0, parenIndex).Trim();
                        var argString = content.Substring(parenIndex + 1, content.Length - parenIndex - 2).Trim();
                        var args = MspArgumentParser.ParseArguments(argString);

                        invocations[index] = new MspFunctionInvocation
                        {
                            FunctionName = funcName,
                            Arguments = args
                        };
                    }
                    else
                    {
                        // 无参数的函数调用
                        invocations[index] = new MspFunctionInvocation
                        {
                            FunctionName = content,
                            Arguments = new List<MspFunctionArgument>()
                        };
                    }
                }
            }

            return invocations;
        }

        private static Dictionary<string, string> GatherMetadata(string rawLine)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(rawLine)) return result;

            foreach (Match match in MetadataRegex.Matches(rawLine))
            {
                var comment = match.Value.Trim();
                comment = comment.Replace("##", "").Trim();
                if (string.IsNullOrEmpty(comment)) continue;

                foreach (var token in comment.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var colonIndex = token.IndexOf(':');
                    if (colonIndex <= 0 || colonIndex >= token.Length - 1)
                    {
                        result.TryAdd(token, token);
                        continue;
                    }

                    var key = token.Substring(0, colonIndex);
                    var value = token.Substring(colonIndex + 1);
                    result.TryAdd(key, value);
                }
            }

            return result;
        }

        private static MspConversation NewConversation(string name)
        {
            return new MspConversation
            {
                Name = name,
                Lines = new List<MspLine>(),
                Labels = new Dictionary<string, int>(),
                ConditionalBlocks = new Dictionary<int, MspConditionalBlock>()
            };
        }

        private static MspLine NewLine()
        {
            return new MspLine
            {
                LineType = MspLineType.Normal,
                LineContent = new MspLineContent
                {
                    Invocations = new Dictionary<int, MspFunctionInvocation>(),
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

        private static void FlushLineIntoConversation(MspConversation conversation, ref MspLine currentLine,
            List<MspLineContent> sentenceParts)
        {
            if (!HasMeaningfulLine(currentLine, sentenceParts))
                return;

            // 合并文本（保留换行）
            currentLine.LineContent.Text = sentenceParts.Count > 0
                ? string.Join("\n", sentenceParts.Select(p => p.Text)).TrimEnd('\n')
                : string.Empty;

            // 合并 invocation（按文本拼接长度计算偏移）
            var offset = 0;
            for (var i = 0; i < sentenceParts.Count; i++)
            {
                var part = sentenceParts[i];
                foreach (var kv in part.Invocations)
                {
                    var adjustedIndex = Mathf.Clamp(offset + kv.Key, 0, int.MaxValue);
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

            // 处理循环结束：当遇到新的 LoopStart 或对话结束时，关闭之前的循环
            // 这个逻辑在解析循环时已经处理，这里不需要额外处理

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