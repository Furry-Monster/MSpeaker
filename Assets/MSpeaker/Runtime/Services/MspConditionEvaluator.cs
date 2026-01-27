using MSpeaker.Runtime.Parser;
using UnityEngine;

namespace MSpeaker.Runtime.Services
{
    public class MspConditionEvaluator : IMspConditionEvaluator
    {
        private readonly IMspVariableService _variableService;

        public MspConditionEvaluator(IMspVariableService variableService)
        {
            _variableService = variableService;
        }

        public bool Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return true;

            var conditionInfo = MspArgumentParser.ParseCondition(expression);
            if (!conditionInfo.IsValid) return false;

            var leftValue = _variableService.GetValue(conditionInfo.LeftOperand);
            if (leftValue == null && conditionInfo.IsVariableComparison)
                return false;

            var rightValue = _variableService.GetValue(conditionInfo.RightOperand);
            if (rightValue == null && !IsLiteralValue(conditionInfo.RightOperand))
                rightValue = conditionInfo.RightOperand;

            return CompareValues(leftValue, rightValue, conditionInfo.Operator);
        }

        public bool EvaluateChoice(string conditionExpression)
        {
            if (string.IsNullOrEmpty(conditionExpression))
                return true;

            var expression = conditionExpression.Trim();
            if (expression.StartsWith("$"))
            {
                var varName = expression.TrimStart('$');
                return _variableService.HasVariable(varName);
            }

            return Evaluate(expression);
        }

        private bool IsLiteralValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return int.TryParse(value, out _) ||
                   float.TryParse(value, out _) ||
                   bool.TryParse(value, out _) ||
                   (value.StartsWith("\"") && value.EndsWith("\"")) ||
                   (value.StartsWith("'") && value.EndsWith("'"));
        }

        private bool CompareValues(object left, object right, string op)
        {
            if (left == null || right == null)
            {
                return op switch
                {
                    "==" => left == right,
                    "!=" => left != right,
                    _ => false
                };
            }

            if (TryConvertToNumber(left, out var leftNum) && TryConvertToNumber(right, out var rightNum))
            {
                return op switch
                {
                    "==" => Mathf.Approximately(leftNum, rightNum),
                    "!=" => !Mathf.Approximately(leftNum, rightNum),
                    ">" => leftNum > rightNum,
                    "<" => leftNum < rightNum,
                    ">=" => leftNum >= rightNum,
                    "<=" => leftNum <= rightNum,
                    _ => false
                };
            }

            var leftStr = left.ToString();
            var rightStr = right.ToString();
            return op switch
            {
                "==" => leftStr == rightStr,
                "!=" => leftStr != rightStr,
                _ => false
            };
        }

        private bool TryConvertToNumber(object value, out float number)
        {
            number = 0f;
            if (value is int i) { number = i; return true; }
            if (value is float f) { number = f; return true; }
            if (value is string s && float.TryParse(s, out var parsed)) { number = parsed; return true; }
            return false;
        }
    }
}
