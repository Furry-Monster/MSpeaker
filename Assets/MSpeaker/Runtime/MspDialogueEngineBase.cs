using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSpeaker.Runtime.Parser;
using MSpeaker.Runtime.Plugins;
using MSpeaker.Runtime.Utils;
using MSpeaker.Runtime.Views;
using UnityEngine;
using UnityEngine.Events;

namespace MSpeaker.Runtime
{
    public abstract class MspDialogueEngineBase : MonoBehaviour
    {
        protected MspEnginePlugin[] enginePlugins;

        public UnityEvent PersistentOnConversationStart = new();
        public UnityEvent PersistentOnConversationEnd = new();

        [HideInInspector] public UnityEvent OnConversationStart = new();
        [HideInInspector] public UnityEvent OnConversationEnd = new();

        public List<MspConversation> ParsedConversations { get; protected set; }
        protected MspConversation _currentConversation;

        protected int _lineIndex;
        protected bool _linePlaying;
        protected bool _isPaused;
        private Coroutine _displayCoroutine;

        private readonly Dictionary<int, int> _loopCounters = new();
        private bool _skippingConditionalBlock;
        private int _conditionalBlockEndIndex = -1;

        public UnityEvent OnConversationPaused = new();
        public UnityEvent OnConversationResumed = new();

        [Header("Dialogue Views")] [SerializeField]
        protected MspDialogueViewBase dialogueView;

        public MspDialogueViewBase View => dialogueView;

        [Header("Function Invocations")] [SerializeField]
        private bool searchAllAssemblies;

        [SerializeField] private List<string> includedAssemblies = new();

        public void StartConversation(MspDialogueAsset dialogueAsset, int startIndex = 0)
        {
            if (dialogueAsset == null) throw new ArgumentNullException(nameof(dialogueAsset));

            ParsedConversations = MspDialogueParser.Parse(dialogueAsset);

            if (startIndex < 0 || startIndex >= ParsedConversations.Count)
                throw new ArgumentOutOfRangeException(nameof(startIndex),
                    "Expected value is between 0 and conversations count (inclusive)");

            enginePlugins = GetComponents<MspEnginePlugin>();
            SwitchConversation(ParsedConversations[startIndex]);
        }

        public void SwitchConversation(MspConversation conversation)
        {
            if (conversation == null) throw new ArgumentNullException(nameof(conversation));

            StopConversation();
            _currentConversation = conversation;
            _lineIndex = 0;
            ResetConditionalState();

            OnConversationStart.RemoveAllListeners();
            OnConversationEnd.RemoveAllListeners();
            OnConversationStart.AddListener(PersistentOnConversationStart.Invoke);
            OnConversationEnd.AddListener(PersistentOnConversationEnd.Invoke);

            OnConversationStart.Invoke();
            _displayCoroutine = StartCoroutine(DisplayDialogue());
        }

        public void StopConversation()
        {
            var hadActiveConversation = _currentConversation != null;

            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
                _displayCoroutine = null;
            }

            if (dialogueView != null)
                dialogueView.ClearView(enginePlugins);

            _linePlaying = false;
            _lineIndex = 0;
            _currentConversation = null;
            _isPaused = false;
            _loopCounters.Clear();
            ResetConditionalState();

            // 只有在确实有活动对话时才触发结束事件
            if (hadActiveConversation)
            {
                OnConversationEnd.Invoke();
            }

            OnConversationStart.RemoveAllListeners();
            OnConversationEnd.RemoveAllListeners();
        }

        public void PauseConversation()
        {
            if (_currentConversation == null || _isPaused) return;
            _isPaused = true;
            if (dialogueView != null) dialogueView.Pause();
            OnConversationPaused.Invoke();
        }

        public void ResumeConversation()
        {
            if (_currentConversation == null || !_isPaused) return;
            _isPaused = false;
            if (dialogueView != null) dialogueView.Resume();
            OnConversationResumed.Invoke();
        }

        public bool IsConversationPaused() => _isPaused;

        private void ResetConditionalState()
        {
            _skippingConditionalBlock = false;
            _conditionalBlockEndIndex = -1;
        }

        private bool HasChoicesAtLine(int lineIndex)
        {
            return _currentConversation?.Choices != null &&
                   _currentConversation.Choices.Any(x => x.Value == lineIndex);
        }

        private int FindPreviousNormalLine(int startIndex)
        {
            for (var i = startIndex; i >= 0; i--)
            {
                var line = _currentConversation.Lines[i];
                if (line.LineType == MspLineType.Normal &&
                    !string.IsNullOrWhiteSpace(line.LineContent?.Text))
                {
                    return i;
                }
            }

            return -1;
        }

        private void TryDisplayChoices()
        {
            if (_currentConversation?.Choices is not { Count: > 0 }) return;

            var foundChoice = _currentConversation.Choices.FirstOrDefault(x => x.Value == _lineIndex);
            if (foundChoice.Key != null && _lineIndex == foundChoice.Value)
            {
                if (ShouldDisplayChoice(foundChoice.Key))
                {
                    dialogueView.DisplayChoices(this, _currentConversation, ParsedConversations);
                }

                return;
            }

            var nextLineIndex = _lineIndex + 1;
            if (nextLineIndex < _currentConversation.Lines.Count)
            {
                var nextLine = _currentConversation.Lines[nextLineIndex];
                if (nextLine.LineType == MspLineType.EndIf)
                {
                    var endifChoice = _currentConversation.Choices.FirstOrDefault(x => x.Value == nextLineIndex);
                    if (endifChoice.Key != null && nextLineIndex == endifChoice.Value)
                    {
                        if (ShouldDisplayChoice(endifChoice.Key))
                        {
                            dialogueView.DisplayChoices(this, _currentConversation, ParsedConversations);
                        }
                    }
                }
            }
        }

        private bool ShouldDisplayChoice(MspChoice choice)
        {
            return string.IsNullOrEmpty(choice.ConditionExpression) ||
                   EvaluateConditionExpression(choice.ConditionExpression);
        }

        private IEnumerator JumpToLineAndContinue(int targetLineIndex)
        {
            _lineIndex = targetLineIndex;
            _linePlaying = false;
            yield return StartCoroutine(DisplayDialogue());
        }

        private IEnumerator ContinueToNextLine()
        {
            _linePlaying = false;
            TryDisplayNextLine();
            yield break;
        }

        public void JumpTo(string conversationName)
        {
            if (ParsedConversations == null || ParsedConversations.Count == 0)
                throw new InvalidOperationException("No conversation executed，can't exec JumpTo.");

            var conversation = ParsedConversations.Find(c => c.Name == conversationName);
            if (conversation == null)
                throw new ArgumentException($"Can't find conversation named \"{conversationName}\" .",
                    nameof(conversationName));

            SwitchConversation(conversation);
        }

        protected virtual IEnumerator DisplayDialogue()
        {
            if (dialogueView == null)
            {
                MspDialogueLogger.LogError(-1, "DialogueView 未设置，无法显示对话。", this);
                yield break;
            }

            if (_currentConversation?.Lines == null || _lineIndex < 0 || _lineIndex >= _currentConversation.Lines.Count)
            {
                MspDialogueLogger.LogError(-1,
                    $"Invalid line index: {_lineIndex}, conversation has {_currentConversation?.Lines?.Count ?? 0} lines.",
                    this);
                _linePlaying = false;
                yield break;
            }

            _linePlaying = true;

            var currentLine = _currentConversation.Lines[_lineIndex];

            if (currentLine.LineType == MspLineType.Label)
            {
                yield return StartCoroutine(ContinueToNextLine());
                yield break;
            }

            if (currentLine.LineType == MspLineType.Goto)
            {
                if (_currentConversation.Labels != null &&
                    _currentConversation.Labels.TryGetValue(currentLine.LabelName, out var targetIndex))
                {
                    yield return StartCoroutine(JumpToLineAndContinue(targetIndex));
                    yield break;
                }
                else
                {
                    MspDialogueLogger.LogError(-1, $"找不到标签：{currentLine.LabelName}", this);
                    _linePlaying = false;
                    yield break;
                }
            }

            if (currentLine.LineType == MspLineType.LoopStart)
            {
                if (!_loopCounters.ContainsKey(_lineIndex))
                {
                    var loopCount = EvaluateLoopCount(currentLine.LoopInfo);
                    _loopCounters[_lineIndex] = loopCount;
                }

                if (_loopCounters[_lineIndex] > 0)
                {
                    _loopCounters[_lineIndex]--;
                    _lineIndex++;
                    if (_lineIndex < _currentConversation.Lines.Count)
                    {
                        yield return StartCoroutine(JumpToLineAndContinue(_lineIndex));
                        yield break;
                    }
                }
                else
                {
                    if (currentLine.LoopInfo.LoopEndLineIndex >= 0)
                    {
                        yield return StartCoroutine(JumpToLineAndContinue(currentLine.LoopInfo.LoopEndLineIndex));
                        yield break;
                    }
                }
            }

            if (currentLine.LineType == MspLineType.LoopEnd)
            {
                var loopStart = _currentConversation.Lines
                    .Select((line, idx) => new { line, idx })
                    .FirstOrDefault(x => x.line.LineType == MspLineType.LoopStart &&
                                         x.line.LoopInfo?.LoopEndLineIndex == _lineIndex);

                if (loopStart != null && _loopCounters.ContainsKey(loopStart.idx) && _loopCounters[loopStart.idx] > 0)
                {
                    yield return StartCoroutine(JumpToLineAndContinue(loopStart.idx));
                    yield break;
                }
                else
                {
                    yield return StartCoroutine(ContinueToNextLine());
                    yield break;
                }
            }

            if (currentLine.LineType == MspLineType.IfStart)
            {
                var conditionMet = EvaluateCondition(currentLine);
                var block = _currentConversation.ConditionalBlocks?.GetValueOrDefault(_lineIndex);

                if (block != null)
                {
                    if (conditionMet)
                    {
                        _skippingConditionalBlock = false;
                        _conditionalBlockEndIndex = block.EndIfLineIndex;
                    }
                    else
                    {
                        _skippingConditionalBlock = true;
                        if (block.ElseLineIndex >= 0)
                        {
                            _lineIndex = block.ElseLineIndex + 1;
                            _skippingConditionalBlock = false;
                            _conditionalBlockEndIndex = block.EndIfLineIndex;
                        }
                        else
                        {
                            _lineIndex = block.EndIfLineIndex;
                            ResetConditionalState();
                        }

                        yield return StartCoroutine(JumpToLineAndContinue(_lineIndex));
                        yield break;
                    }
                }
            }

            if (currentLine.LineType == MspLineType.Else)
            {
                var block = _currentConversation.ConditionalBlocks?.Values
                    .FirstOrDefault(b => b.ElseLineIndex == _lineIndex);
                if (block != null)
                {
                    ResetConditionalState();
                    yield return StartCoroutine(JumpToLineAndContinue(block.EndIfLineIndex));
                    yield break;
                }
            }

            if (currentLine.LineType == MspLineType.EndIf)
            {
                ResetConditionalState();

                if (HasChoicesAtLine(_lineIndex))
                {
                    var previousLineIndex = FindPreviousNormalLine(_lineIndex - 1);
                    if (previousLineIndex >= 0)
                    {
                        yield return StartCoroutine(JumpToLineAndContinue(previousLineIndex));
                        yield break;
                    }
                }

                yield return StartCoroutine(ContinueToNextLine());
                yield break;
            }

            if (_skippingConditionalBlock && _conditionalBlockEndIndex >= 0 && _lineIndex < _conditionalBlockEndIndex)
            {
                yield return StartCoroutine(ContinueToNextLine());
                yield break;
            }

            TryDisplayChoices();
            dialogueView.SetView(_currentConversation, _lineIndex);

            if (enginePlugins != null)
            {
                foreach (var plugin in enginePlugins)
                    plugin.Display(_currentConversation, _lineIndex);
            }

            InvokeFunctions(_currentConversation.Lines[_lineIndex].LineContent?.Invocations);

            yield return new WaitUntil(() => !dialogueView.IsStillDisplaying() || _isPaused);

            if (_isPaused)
                yield return new WaitUntil(() => !_isPaused);

            _linePlaying = false;
        }

        /// <summary>
        /// 评估循环次数（支持变量）
        /// </summary>
        protected virtual int EvaluateLoopCount(MspLoopInfo loopInfo)
        {
            if (loopInfo == null) return 1;

            var expression = loopInfo.LoopCountExpression;
            if (string.IsNullOrEmpty(expression))
                return loopInfo.LoopCount;

            if (expression.StartsWith("$"))
            {
                var varValue = GetVariableValue(expression);
                if (varValue != null)
                {
                    if (varValue is int intVal) return intVal;
                    if (int.TryParse(varValue.ToString(), out var parsed)) return parsed;
                }

                MspDialogueLogger.LogWarning(-1,
                    $"Loop count variable {expression} not found or invalid, using default value 1.", this);
                return 1;
            }

            if (int.TryParse(expression, out var count))
                return count;

            return loopInfo.LoopCount;
        }

        /// <summary>
        /// 评估条件是否满足
        /// </summary>
        protected virtual bool EvaluateCondition(MspLine line)
        {
            if (line.LineType != MspLineType.IfStart) return false;

            var block = _currentConversation.ConditionalBlocks?.GetValueOrDefault(_lineIndex);
            if (block == null) return false;

            return EvaluateConditionExpression(block.ConditionExpression);
        }

        /// <summary>
        /// 评估条件表达式
        /// </summary>
        protected virtual bool EvaluateConditionExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return true;

            var conditionInfo = MspArgumentParser.ParseCondition(expression);
            if (!conditionInfo.IsValid) return false;

            var leftValue = GetVariableValue(conditionInfo.LeftOperand);
            if (leftValue == null && conditionInfo.IsVariableComparison)
                return false;

            var rightValue = GetVariableValue(conditionInfo.RightOperand);
            if (rightValue == null && !IsLiteralValue(conditionInfo.RightOperand))
            {
                rightValue = conditionInfo.RightOperand;
            }

            return CompareValues(leftValue, rightValue, conditionInfo.Operator);
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        protected virtual object GetVariableValue(string variableName)
        {
            if (string.IsNullOrEmpty(variableName)) return null;

            var key = variableName.TrimStart('$');
            if (!MspDialogueGlobals.GlobalVariables.TryGetValue(key, out var value))
                return null;

            if (int.TryParse(value, out var intVal)) return intVal;
            if (float.TryParse(value, out var floatVal)) return floatVal;
            if (bool.TryParse(value, out var boolVal)) return boolVal;
            return value;
        }

        /// <summary>
        /// 判断是否是字面值
        /// </summary>
        private bool IsLiteralValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return int.TryParse(value, out _) ||
                   float.TryParse(value, out _) ||
                   bool.TryParse(value, out _) ||
                   (value.StartsWith("\"") && value.EndsWith("\"")) ||
                   (value.StartsWith("'") && value.EndsWith("'"));
        }

        /// <summary>
        /// 比较两个值
        /// </summary>
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
            if (value is int i)
            {
                number = i;
                return true;
            }

            if (value is float f)
            {
                number = f;
                return true;
            }

            if (value is string s && float.TryParse(s, out var parsed))
            {
                number = parsed;
                return true;
            }

            return false;
        }

        protected virtual void InvokeFunctions(Dictionary<int, MspFunctionInvocation> functionInvocations)
        {
            if (functionInvocations == null || functionInvocations.Count == 0) return;

            var methods = GetDialogueMethods().ToArray();
            if (methods.Length == 0) return;

            var insertedOffset = 0;
            foreach (var kv in functionInvocations.OrderBy(x => x.Key))
            {
                var invocation = kv.Value;
                if (invocation == null || string.IsNullOrEmpty(invocation.FunctionName)) continue;

                foreach (var method in methods)
                {
                    if (!string.Equals(method.Name, invocation.FunctionName, StringComparison.Ordinal)) continue;

                    var parameters = method.GetParameters();
                    var args = BuildMethodArguments(parameters, invocation.Arguments);

                    if (args == null)
                    {
                        MspDialogueLogger.LogWarning(-1,
                            $"Invocation \"{invocation.FunctionName}\" 找到了方法，但参数不匹配。");
                        continue;
                    }

                    if (method.ReturnType == typeof(string))
                    {
                        var replaced = (string)method.Invoke(null, args);
                        replaced ??= string.Empty;

                        var lineContent = _currentConversation.Lines[_lineIndex].LineContent;
                        var insertIndex = Mathf.Clamp(kv.Key + insertedOffset, 0,
                            (lineContent.Text ?? string.Empty).Length);
                        lineContent.Text = (lineContent.Text ?? string.Empty).Insert(insertIndex, replaced);
                        insertedOffset += replaced.Length;

                        dialogueView.SetView(_currentConversation, _lineIndex);
                    }
                    else
                    {
                        method.Invoke(null, args);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// 构建方法参数数组
        /// </summary>
        private object[] BuildMethodArguments(ParameterInfo[] parameters, List<MspFunctionArgument> invocationArgs)
        {
            if (parameters.Length == 0)
            {
                if (invocationArgs == null || invocationArgs.Count == 0)
                    return Array.Empty<object>();
                return null;
            }

            if (parameters.Length == 1 &&
                (parameters[0].ParameterType.IsAssignableFrom(typeof(MspDialogueEngineBase)) ||
                 parameters[0].ParameterType == typeof(MspDialogueEngineBase)))
            {
                return new object[] { this };
            }

            if (invocationArgs == null || invocationArgs.Count != parameters.Length)
            {
                return null;
            }

            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var arg = invocationArgs[i];

                if (arg.Type == MspArgumentType.Variable)
                {
                    var varName = arg.ConvertedValue?.ToString();
                    var varValue = GetVariableValue("$" + varName);
                    if (varValue == null)
                    {
                        MspDialogueLogger.LogWarning(-1, $"变量 ${varName} 不存在。");
                        return null;
                    }

                    args[i] = ConvertValue(varValue, paramType);
                }
                else
                {
                    args[i] = ConvertValue(arg.ConvertedValue, paramType);
                }
            }

            return args;
        }

        /// <summary>
        /// 转换值到指定类型
        /// </summary>
        private object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            if (targetType == typeof(string))
                return value.ToString();

            if (targetType == typeof(int))
            {
                if (value is int i) return i;
                return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
            }

            if (targetType == typeof(float))
            {
                if (value is float f) return f;
                return float.TryParse(value.ToString(), out var parsed) ? parsed : 0f;
            }

            if (targetType == typeof(bool))
            {
                if (value is bool b) return b;
                return bool.TryParse(value.ToString(), out var parsed) && parsed;
            }

            return Convert.ChangeType(value, targetType);
        }

        public void TryDisplayNextLine()
        {
            if (_linePlaying)
            {
                MspDialogueLogger.LogWarning(-1, "TryDisplayNextLine called but line is still playing.", this);
                return;
            }

            if (_currentConversation == null)
            {
                MspDialogueLogger.LogWarning(-1, "TryDisplayNextLine called but no conversation is active.", this);
                return;
            }

            if (dialogueView == null)
            {
                MspDialogueLogger.LogWarning(-1, "TryDisplayNextLine called but dialogue view is null.", this);
                return;
            }

            if (HasChoicesAtLine(_lineIndex) ||
                (_lineIndex + 1 < _currentConversation.Lines.Count &&
                 _currentConversation.Lines[_lineIndex + 1].LineType == MspLineType.EndIf &&
                 HasChoicesAtLine(_lineIndex + 1)))
            {
                return;
            }

            dialogueView.ClearView(enginePlugins);

            if (_lineIndex < _currentConversation.Lines.Count - 1)
            {
                _lineIndex++;
                StartCoroutine(DisplayDialogue());
            }
            else
            {
                StopConversation();
            }
        }

        protected IEnumerable<MethodInfo> GetDialogueMethods()
        {
            var assemblies = new List<Assembly>();
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            if (searchAllAssemblies)
            {
                assemblies.AddRange(allAssemblies);
            }
            else
            {
                foreach (var asm in allAssemblies)
                {
                    var asmName = asm.GetName().Name;
                    if (asmName == "Assembly-CSharp" ||
                        includedAssemblies.Contains(asmName) ||
                        asm == Assembly.GetExecutingAssembly())
                        assemblies.Add(asm);
                }
            }

            var methods = new List<MethodInfo>();
            foreach (var asm in assemblies)
            {
                var found = asm.GetTypes()
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    .Where(m => m.GetCustomAttributes(typeof(MspDialogueFunctionAttribute), false).Length > 0);
                methods.AddRange(found);
            }

            return methods;
        }
    }
}