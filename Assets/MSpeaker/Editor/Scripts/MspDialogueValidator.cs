using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MSpeaker.Runtime;
using MSpeaker.Runtime.Parser;

namespace MSpeaker.Editor
{
    /// <summary>
    /// 对话文件验证工具
    /// </summary>
    public static class MspDialogueValidator
    {
        public enum ValidationSeverity
        {
            Info,
            Warning,
            Error
        }

        public class ValidationIssue
        {
            public int LineNumber;
            public ValidationSeverity Severity;
            public string Message;
            public string CodeSnippet;

            public ValidationIssue(int lineNumber, ValidationSeverity severity, string message,
                string codeSnippet = null)
            {
                LineNumber = lineNumber;
                Severity = severity;
                Message = message;
                CodeSnippet = codeSnippet;
            }
        }

        public class ValidationResult
        {
            public bool IsValid => Issues.All(i => i.Severity != ValidationSeverity.Error);
            public List<ValidationIssue> Issues = new();
            public List<MspConversation> ParsedConversations = new();
        }

        /// <summary>
        /// 验证对话文件
        /// </summary>
        public static ValidationResult Validate(MspDialogueAsset asset)
        {
            var result = new ValidationResult();

            if (asset == null)
            {
                result.Issues.Add(new ValidationIssue(0, ValidationSeverity.Error, "对话资源为空"));
                return result;
            }

            if (string.IsNullOrEmpty(asset.Content))
            {
                result.Issues.Add(new ValidationIssue(0, ValidationSeverity.Error, "对话内容为空"));
                return result;
            }

            try
            {
                result.ParsedConversations = MspDialogueParser.Parse(asset);
            }
            catch (System.Exception ex)
            {
                result.Issues.Add(new ValidationIssue(0, ValidationSeverity.Error, $"解析失败: {ex.Message}"));
                return result;
            }

            var lines = asset.Content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            ValidateConditionalBlocks(lines, result);
            ValidateLoops(lines, result);
            ValidateConversationReferences(result);
            ValidateLabelReferences(result);
            ValidateChoices(result);
            ValidateEmptyConversations(result);
            ValidateDuplicateConversationNames(result);

            return result;
        }

        private static void ValidateConditionalBlocks(string[] lines, ValidationResult result)
        {
            var stack = new Stack<int>();
            var ifLineNumbers = new Dictionary<int, int>();

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNum = i + 1;

                if (Regex.IsMatch(line, @"^\s*{{\s*If\s*\(", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"^\s*{{\s*IfVar\s*\(", RegexOptions.IgnoreCase))
                {
                    stack.Push(i);
                    ifLineNumbers[stack.Count - 1] = lineNum;
                }
                else if (Regex.IsMatch(line, @"^\s*{{\s*EndIf\s*}}\s*$", RegexOptions.IgnoreCase))
                {
                    if (stack.Count == 0)
                    {
                        result.Issues.Add(new ValidationIssue(
                            lineNum,
                            ValidationSeverity.Error,
                            "发现 {{EndIf}} 但没有对应的 {{If}} 或 {{IfVar}}",
                            line.Trim()
                        ));
                    }
                    else
                    {
                        stack.Pop();
                    }
                }
            }

            while (stack.Count > 0)
            {
                var stackIndex = stack.Count - 1;
                var ifLineNum = ifLineNumbers[stackIndex];
                stack.Pop();
                result.Issues.Add(new ValidationIssue(
                    ifLineNum,
                    ValidationSeverity.Error,
                    "条件块未闭合，缺少 {{EndIf}}",
                    lines[ifLineNum - 1].Trim()
                ));
            }
        }

        private static void ValidateLoops(string[] lines, ValidationResult result)
        {
            var loopStartLines = new List<int>();

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNum = i + 1;

                if (Regex.IsMatch(line, @"^\s*{{\s*Loop\s*\(", RegexOptions.IgnoreCase))
                {
                    loopStartLines.Add(lineNum);
                }
            }
        }

        private static void ValidateConversationReferences(ValidationResult result)
        {
            var conversationNames = result.ParsedConversations.Select(c => c.Name).ToHashSet();

            foreach (var conversation in result.ParsedConversations)
            {
                if (conversation.Choices == null) continue;

                foreach (var choice in conversation.Choices.Keys)
                {
                    if (string.IsNullOrEmpty(choice.LeadingConversationName))
                    {
                        result.Issues.Add(new ValidationIssue(
                            -1,
                            ValidationSeverity.Warning,
                            $"选择分支 \"{choice.ChoiceName}\" 没有指定目标对话",
                            choice.ChoiceName
                        ));
                        continue;
                    }

                    if (!conversationNames.Contains(choice.LeadingConversationName))
                    {
                        result.Issues.Add(new ValidationIssue(
                            -1,
                            ValidationSeverity.Error,
                            $"选择分支 \"{choice.ChoiceName}\" 引用了不存在的对话: {choice.LeadingConversationName}",
                            $"{choice.ChoiceName} -> {choice.LeadingConversationName}"
                        ));
                    }
                }
            }
        }

        private static void ValidateLabelReferences(ValidationResult result)
        {
            foreach (var conversation in result.ParsedConversations)
            {
                if (conversation.Labels == null) continue;

                var labelNames = conversation.Labels.Keys.ToHashSet();

                for (var i = 0; i < conversation.Lines.Count; i++)
                {
                    var line = conversation.Lines[i];
                    if (line.LineType == MspLineType.Goto && !string.IsNullOrEmpty(line.LabelName))
                    {
                        if (!labelNames.Contains(line.LabelName))
                        {
                            result.Issues.Add(new ValidationIssue(
                                -1,
                                ValidationSeverity.Error,
                                $"对话 \"{conversation.Name}\" 中的 {{Goto({line.LabelName})}} 引用了不存在的标签",
                                $"{{Goto({line.LabelName})}}"
                            ));
                        }
                    }
                }
            }
        }

        private static void ValidateChoices(ValidationResult result)
        {
            foreach (var conversation in result.ParsedConversations)
            {
                if (conversation.Choices == null || conversation.Choices.Count == 0) continue;

                var choiceTexts = new Dictionary<string, int>();
                foreach (var choice in conversation.Choices.Keys)
                {
                    if (string.IsNullOrEmpty(choice.ChoiceName))
                    {
                        result.Issues.Add(new ValidationIssue(
                            -1,
                            ValidationSeverity.Warning,
                            $"对话 \"{conversation.Name}\" 中有空的选择分支",
                            ""
                        ));
                        continue;
                    }

                    if (choiceTexts.ContainsKey(choice.ChoiceName))
                    {
                        result.Issues.Add(new ValidationIssue(
                            -1,
                            ValidationSeverity.Info,
                            $"对话 \"{conversation.Name}\" 中有重复的选择文本: {choice.ChoiceName}",
                            choice.ChoiceName
                        ));
                    }
                    else
                    {
                        choiceTexts[choice.ChoiceName] = 1;
                    }
                }
            }
        }

        private static void ValidateEmptyConversations(ValidationResult result)
        {
            foreach (var conversation in result.ParsedConversations)
            {
                if (conversation.Lines == null || conversation.Lines.Count == 0)
                {
                    result.Issues.Add(new ValidationIssue(
                        -1,
                        ValidationSeverity.Warning,
                        $"对话 \"{conversation.Name}\" 是空的（没有对话行）",
                        conversation.Name
                    ));
                }
                else
                {
                    var hasContent = conversation.Lines.Any(line =>
                        line.LineType == MspLineType.Normal &&
                        !string.IsNullOrWhiteSpace(line.LineContent?.Text));

                    if (!hasContent)
                    {
                        result.Issues.Add(new ValidationIssue(
                            -1,
                            ValidationSeverity.Warning,
                            $"对话 \"{conversation.Name}\" 没有实际的对话内容",
                            conversation.Name
                        ));
                    }
                }
            }
        }

        private static void ValidateDuplicateConversationNames(ValidationResult result)
        {
            var nameCounts = new Dictionary<string, int>();
            foreach (var conversation in result.ParsedConversations)
            {
                if (nameCounts.ContainsKey(conversation.Name))
                {
                    nameCounts[conversation.Name]++;
                }
                else
                {
                    nameCounts[conversation.Name] = 1;
                }
            }

            foreach (var kv in nameCounts)
            {
                if (kv.Value > 1)
                {
                    result.Issues.Add(new ValidationIssue(
                        -1,
                        ValidationSeverity.Error,
                        $"发现重复的对话名称: {kv.Key} (出现 {kv.Value} 次)",
                        kv.Key
                    ));
                }
            }
        }
    }
}