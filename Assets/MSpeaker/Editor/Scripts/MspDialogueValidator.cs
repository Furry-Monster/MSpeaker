using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MSpeaker.Runtime;
using MSpeaker.Runtime.Parser;

namespace MSpeaker.Editor
{
    public static class MspDialogueValidator
    {
        private static readonly Regex IfRegex = new(@"^\s*{{\s*If\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex IfVarRegex = new(@"^\s*{{\s*IfVar\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EndIfRegex = new(@"^\s*{{\s*EndIf\s*}}\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LoopRegex = new(@"^\s*{{\s*Loop\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public enum ValidationSeverity
        {
            Info,
            Warning,
            Error
        }

        public class ValidationIssue
        {
            public readonly int LineNumber;
            public readonly ValidationSeverity Severity;
            public readonly string Message;
            public readonly string CodeSnippet;

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
            private bool? _isValidCache;

            public bool IsValid
            {
                get
                {
                    if (_isValidCache.HasValue) return _isValidCache.Value;
                    _isValidCache = Issues.All(i => i.Severity != ValidationSeverity.Error);
                    return _isValidCache.Value;
                }
            }

            public readonly List<ValidationIssue> Issues = new();
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

                if (IfRegex.IsMatch(line) || IfVarRegex.IsMatch(line))
                {
                    stack.Push(i);
                    ifLineNumbers[stack.Count - 1] = lineNum;
                }
                else if (EndIfRegex.IsMatch(line))
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
                if (LoopRegex.IsMatch(line))
                {
                    loopStartLines.Add(i + 1);
                }
            }
        }

        private static void ValidateConversationReferences(ValidationResult result)
        {
            var conversationNames = new HashSet<string>(result.ParsedConversations.Count);
            foreach (var conversation in result.ParsedConversations)
            {
                conversationNames.Add(conversation.Name);
            }

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

                    if (!choiceTexts.TryAdd(choice.ChoiceName, 1))
                    {
                        result.Issues.Add(new ValidationIssue(
                            -1,
                            ValidationSeverity.Info,
                            $"对话 \"{conversation.Name}\" 中有重复的选择文本: {choice.ChoiceName}",
                            choice.ChoiceName
                        ));
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
                if (!nameCounts.TryAdd(conversation.Name, 1))
                {
                    nameCounts[conversation.Name]++;
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