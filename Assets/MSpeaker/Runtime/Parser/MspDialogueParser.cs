using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MSpeaker.Runtime.Utils;

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
            var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            var conversations = new List<MspConversation>();
            var currentConversation = NewConversation("Default");

            var sentenceParts = new List<MspLineContent>();
            var currentLine = NewLine();

            var conditionalStack = new Stack<MspConditionalBlock>();
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
                    case Token.IfVarStart:
                    {
                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);
                        var condition = argInvocationArg;
                        var block = new MspConditionalBlock
                        {
                            ConditionType = token == Token.IfStart ? MspConditionType.If : MspConditionType.IfVar,
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
                        var countExpression = argInvocationArg.Trim();
                        var loopCount = 1;

                        if (!countExpression.StartsWith("$") && int.TryParse(countExpression, out var parsedCount))
                        {
                            loopCount = parsedCount;
                        }

                        var loopInfo = new MspLoopInfo
                        {
                            LoopCount = loopCount,
                            LoopCountExpression = countExpression,
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
                        currentLine.SpeakerImagePath = ReplaceGlobalVariables(argInvocationArg);
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
                        FlushLineIntoConversation(currentConversation, ref currentLine, sentenceParts);

                        currentConversation.Choices ??= new Dictionary<MspChoice, int>();

                        var (choiceText, targetConversation, metadata) = ParseChoice(raw);
                        var choice = new MspChoice
                        {
                            ChoiceName = ReplaceGlobalVariables(choiceText),
                            LeadingConversationName = ReplaceGlobalVariables(targetConversation),
                            Metadata = metadata
                        };

                        var anchorLineIndex = currentConversation.Lines.Count > 0
                            ? currentConversation.Lines.Count - 1
                            : 0;
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

            while (loopStack.Count > 0)
            {
                var loopInfo = loopStack.Pop();
                loopInfo.LoopEndLineIndex = currentConversation.Lines.Count;

                var endLine = NewLine();
                endLine.LineType = MspLineType.LoopEnd;
                endLine.LineContent.Text = string.Empty;
                currentConversation.Lines.Add(endLine);
            }

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
            var metadata = GatherMetadata(rawLine);
            var lineWithoutMetadata = MetadataRegex.Replace(rawLine, string.Empty).Trim();
            var m = ChoiceRegex.Match(lineWithoutMetadata);
            if (!m.Success)
                return (lineWithoutMetadata, string.Empty, metadata);

            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim(), metadata);
        }

        private static string RemoveMetadataAndInvocationsFromSentence(string rawLine)
        {
            var line = rawLine ?? string.Empty;
            line = FunctionRegex.Replace(line, string.Empty);
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

                return match.Value;
            });
        }

        private static Dictionary<int, MspFunctionInvocation> GatherInlineFunctionInvocations(string rawLine)
        {
            var invocations = new Dictionary<int, MspFunctionInvocation>();
            if (string.IsNullOrEmpty(rawLine)) return invocations;

            var searchStartIndex = 0;
            foreach (Match match in FunctionRegex.Matches(rawLine))
            {
                var full = match.Value;
                var index = rawLine.IndexOf(full, searchStartIndex, StringComparison.Ordinal);

                if (index >= 0)
                {
                    searchStartIndex = index + full.Length;

                    var content = full.Substring(2, full.Length - 4).Trim();

                    var parenIndex = content.IndexOf('(');
                    if (parenIndex >= 0 && content.EndsWith(")"))
                    {
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
            if (!string.IsNullOrEmpty(line.SpeakerImagePath)) return true;
            if (sentenceParts.Any(p => !string.IsNullOrWhiteSpace(p.Text))) return true;
            return false;
        }

        private static void FlushLineIntoConversation(MspConversation conversation, ref MspLine currentLine,
            List<MspLineContent> sentenceParts)
        {
            if (!HasMeaningfulLine(currentLine, sentenceParts))
                return;

            currentLine.LineContent.Text = sentenceParts.Count > 0
                ? string.Join("\n", sentenceParts.Select(p => p.Text)).TrimEnd('\n')
                : string.Empty;

            var offset = 0;
            for (var i = 0; i < sentenceParts.Count; i++)
            {
                var part = sentenceParts[i];
                foreach (var kv in part.Invocations)
                {
                    var adjustedIndex = Math.Max(0, offset + kv.Key);
                    if (!currentLine.LineContent.Invocations.ContainsKey(adjustedIndex))
                        currentLine.LineContent.Invocations.Add(adjustedIndex, kv.Value);
                }

                foreach (var kv in part.Metadata)
                {
                    if (!currentLine.LineContent.Metadata.ContainsKey(kv.Key))
                        currentLine.LineContent.Metadata.Add(kv.Key, kv.Value);
                }

                offset += (part.Text ?? string.Empty).Length;
                if (i < sentenceParts.Count - 1) offset += 1;
            }

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