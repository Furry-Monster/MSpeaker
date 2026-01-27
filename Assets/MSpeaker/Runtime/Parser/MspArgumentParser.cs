using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MSpeaker.Runtime.Parser;

namespace MSpeaker.Runtime.Utils
{
    /// <summary>
    /// 函数参数解析和类型转换工具
    /// </summary>
    public static class MspArgumentParser
    {
        private static readonly Regex VariableRegex = new(@"^\$[a-zA-Z0-9_]+$");
        private static readonly Regex IntegerRegex = new(@"^-?\d+$");
        private static readonly Regex FloatRegex = new(@"^-?\d+\.\d+$");
        private static readonly Regex BooleanRegex = new(@"^(true|false)$", RegexOptions.IgnoreCase);

        /// <summary>
        /// 解析函数调用参数字符串，支持逗号分隔的多个参数
        /// </summary>
        /// <param name="argString">参数字符串，例如 "hello, 123, true"</param>
        /// <returns>解析后的参数列表</returns>
        public static List<MspFunctionArgument> ParseArguments(string argString)
        {
            var arguments = new List<MspFunctionArgument>();

            if (string.IsNullOrWhiteSpace(argString))
                return arguments;

            var parts = SplitArguments(argString);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                arguments.Add(new MspFunctionArgument
                {
                    RawValue = trimmed,
                    Type = DetectArgumentType(trimmed),
                    ConvertedValue = ConvertArgument(trimmed)
                });
            }

            return arguments;
        }

        /// <summary>
        /// 检测参数类型
        /// </summary>
        private static MspArgumentType DetectArgumentType(string value)
        {
            if (VariableRegex.IsMatch(value))
                return MspArgumentType.Variable;

            if (BooleanRegex.IsMatch(value))
                return MspArgumentType.Boolean;

            if (IntegerRegex.IsMatch(value))
                return MspArgumentType.Integer;

            if (FloatRegex.IsMatch(value))
                return MspArgumentType.Float;

            // 检查是否是带引号的字符串
            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
                return MspArgumentType.String;

            // 默认作为字符串处理
            return MspArgumentType.String;
        }

        /// <summary>
        /// 转换参数值
        /// </summary>
        private static object ConvertArgument(string value)
        {
            var type = DetectArgumentType(value);

            switch (type)
            {
                case MspArgumentType.Variable:
                    return value.TrimStart('$');

                case MspArgumentType.Boolean:
                    return bool.Parse(value);

                case MspArgumentType.Integer:
                    return int.Parse(value, CultureInfo.InvariantCulture);

                case MspArgumentType.Float:
                    return float.Parse(value, CultureInfo.InvariantCulture);

                case MspArgumentType.String:
                    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                        (value.StartsWith("'") && value.EndsWith("'")))
                        return value.Substring(1, value.Length - 2);
                    return value;

                default:
                    return value;
            }
        }

        /// <summary>
        /// 分割参数字符串，考虑引号内的逗号
        /// </summary>
        private static List<string> SplitArguments(string argString)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            var quoteChar = '\0';

            for (var i = 0; i < argString.Length; i++)
            {
                var ch = argString[i];

                if (ch is '"' or '\'' && (i == 0 || argString[i - 1] != '\\'))
                {
                    if (!inQuotes)
                    {
                        inQuotes = true;
                        quoteChar = ch;
                    }
                    else if (ch == quoteChar)
                    {
                        inQuotes = false;
                        quoteChar = '\0';
                    }

                    current.Append(ch);
                }
                else if (ch == ',' && !inQuotes)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }

            if (current.Length > 0)
                parts.Add(current.ToString());

            return parts;
        }

        /// <summary>
        /// 解析条件表达式，支持变量比较
        /// </summary>
        /// <param name="expression">条件表达式，例如 "score > 10" 或 "name == 'John'"</param>
        /// <returns>解析后的条件信息</returns>
        public static MspConditionInfo ParseCondition(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return new MspConditionInfo { IsValid = false };

            expression = expression.Trim();

            if (expression.Contains(','))
            {
                var parts = expression.Split(',');
                if (parts.Length == 2)
                {
                    return new MspConditionInfo
                    {
                        IsValid = true,
                        LeftOperand = parts[0].Trim(),
                        Operator = "==",
                        RightOperand = parts[1].Trim(),
                        IsVariableComparison = true
                    };
                }
            }

            var operators = new[] { "==", "!=", ">=", "<=", ">", "<" };
            foreach (var op in operators)
            {
                var index = expression.IndexOf(op, StringComparison.Ordinal);
                if (index > 0)
                {
                    var left = expression[..index].Trim();
                    var right = expression[(index + op.Length)..].Trim();

                    if ((right.StartsWith("\"") && right.EndsWith("\"")) ||
                        (right.StartsWith("'") && right.EndsWith("'")))
                        right = right.Substring(1, right.Length - 2);

                    return new MspConditionInfo
                    {
                        IsValid = true,
                        LeftOperand = left,
                        Operator = op,
                        RightOperand = right,
                        IsVariableComparison = left.StartsWith("$") || !IsNumeric(left)
                    };
                }
            }

            return new MspConditionInfo
            {
                IsValid = true,
                LeftOperand = expression,
                Operator = "==",
                RightOperand = "true",
                IsVariableComparison = true
            };
        }

        private static bool IsNumeric(string value)
        {
            return IntegerRegex.IsMatch(value) || FloatRegex.IsMatch(value);
        }
    }

    /// <summary>
    /// 条件信息
    /// </summary>
    public sealed class MspConditionInfo
    {
        public bool IsValid;
        public string LeftOperand;
        public string Operator;
        public string RightOperand;
        public bool IsVariableComparison;
    }
}