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

        // 循环状态：lineIndex -> 剩余循环次数
        private readonly Dictionary<int, int> _loopCounters = new();

        // 条件分支跳过状态：当前是否在跳过条件分支
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

            // 先移除可能存在的监听器，避免重复添加
            OnConversationStart.RemoveListener(PersistentOnConversationStart.Invoke);
            OnConversationEnd.RemoveListener(PersistentOnConversationEnd.Invoke);

            // 然后添加监听器
            OnConversationStart.AddListener(PersistentOnConversationStart.Invoke);
            OnConversationEnd.AddListener(PersistentOnConversationEnd.Invoke);

            OnConversationStart.Invoke();
            _displayCoroutine = StartCoroutine(DisplayDialogue());
        }

        public void StopConversation()
        {
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
            _skippingConditionalBlock = false;
            _conditionalBlockEndIndex = -1;

            OnConversationEnd.Invoke();
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

            _linePlaying = true;

            // 处理当前行的特殊类型
            var currentLine = _currentConversation.Lines[_lineIndex];

            // 处理标签
            if (currentLine.LineType == MspLineType.Label)
            {
                // 标签行不显示，直接继续
                _linePlaying = false;
                TryDisplayNextLine();
                yield break;
            }

            // 处理跳转
            if (currentLine.LineType == MspLineType.Goto)
            {
                if (_currentConversation.Labels != null &&
                    _currentConversation.Labels.TryGetValue(currentLine.LabelName, out var targetIndex))
                {
                    _lineIndex = targetIndex;
                    _linePlaying = false;
                    StartCoroutine(DisplayDialogue());
                    yield break;
                }
                else
                {
                    MspDialogueLogger.LogError(-1, $"找不到标签：{currentLine.LabelName}", this);
                    _linePlaying = false;
                    yield break;
                }
            }

            // 处理循环开始
            if (currentLine.LineType == MspLineType.LoopStart)
            {
                if (!_loopCounters.ContainsKey(_lineIndex))
                {
                    _loopCounters[_lineIndex] = currentLine.LoopInfo.LoopCount;
                }

                if (_loopCounters[_lineIndex] > 0)
                {
                    _loopCounters[_lineIndex]--;
                    // 继续执行循环内的内容
                }
                else
                {
                    // 循环结束，跳转到循环结束位置
                    if (currentLine.LoopInfo.LoopEndLineIndex >= 0)
                    {
                        _lineIndex = currentLine.LoopInfo.LoopEndLineIndex;
                        _linePlaying = false;
                        StartCoroutine(DisplayDialogue());
                        yield break;
                    }
                }
            }

            // 处理循环结束
            if (currentLine.LineType == MspLineType.LoopEnd)
            {
                // 查找对应的循环开始
                var loopStart = _currentConversation.Lines
                    .Select((line, idx) => new { line, idx })
                    .FirstOrDefault(x => x.line.LineType == MspLineType.LoopStart &&
                                         x.line.LoopInfo?.LoopEndLineIndex == _lineIndex);

                if (loopStart != null && _loopCounters.ContainsKey(loopStart.idx) && _loopCounters[loopStart.idx] > 0)
                {
                    // 继续循环，跳回循环开始
                    _lineIndex = loopStart.idx;
                    _linePlaying = false;
                    StartCoroutine(DisplayDialogue());
                    yield break;
                }
                else
                {
                    // 循环真正结束，继续下一行
                    _linePlaying = false;
                    TryDisplayNextLine();
                    yield break;
                }
            }

            // 处理条件分支
            if (currentLine.LineType == MspLineType.IfStart)
            {
                var conditionMet = EvaluateCondition(currentLine);
                var block = _currentConversation.ConditionalBlocks?.GetValueOrDefault(_lineIndex);

                if (block != null)
                {
                    if (conditionMet)
                    {
                        // 条件满足，执行 If 分支
                        _skippingConditionalBlock = false;
                        _conditionalBlockEndIndex = block.EndIfLineIndex;
                    }
                    else
                    {
                        // 条件不满足，跳过 If 分支
                        _skippingConditionalBlock = true;
                        if (block.ElseLineIndex >= 0)
                        {
                            // 有 Else 分支，跳转到 Else
                            _lineIndex = block.ElseLineIndex + 1; // Else 行本身不显示
                            _skippingConditionalBlock = false;
                            _conditionalBlockEndIndex = block.EndIfLineIndex;
                        }
                        else
                        {
                            // 没有 Else 分支，直接跳转到 EndIf
                            _lineIndex = block.EndIfLineIndex;
                            _skippingConditionalBlock = false;
                            _conditionalBlockEndIndex = -1;
                        }

                        _linePlaying = false;
                        StartCoroutine(DisplayDialogue());
                        yield break;
                    }
                }
            }

            if (currentLine.LineType == MspLineType.Else)
            {
                // Else 行本身不显示，直接跳转到 EndIf
                var block = _currentConversation.ConditionalBlocks?.Values
                    .FirstOrDefault(b => b.ElseLineIndex == _lineIndex);
                if (block != null)
                {
                    _lineIndex = block.EndIfLineIndex;
                    _skippingConditionalBlock = false;
                    _conditionalBlockEndIndex = -1;
                    _linePlaying = false;
                    StartCoroutine(DisplayDialogue());
                    yield break;
                }
            }

            if (currentLine.LineType == MspLineType.EndIf)
            {
                // EndIf 行不显示，继续下一行
                _skippingConditionalBlock = false;
                _conditionalBlockEndIndex = -1;
                _linePlaying = false;
                TryDisplayNextLine();
                yield break;
            }

            // 如果正在跳过条件分支，直接跳过
            if (_skippingConditionalBlock && _conditionalBlockEndIndex >= 0 && _lineIndex < _conditionalBlockEndIndex)
            {
                _linePlaying = false;
                TryDisplayNextLine();
                yield break;
            }

            // 显示选择（如果有）
            if (_currentConversation?.Choices is { Count: > 0 })
            {
                var foundChoice = _currentConversation.Choices.FirstOrDefault(x => x.Value == _lineIndex);
                if (foundChoice.Key != null && _lineIndex == foundChoice.Value)
                {
                    // 检查选择的条件
                    if (string.IsNullOrEmpty(foundChoice.Key.ConditionExpression) ||
                        EvaluateConditionExpression(foundChoice.Key.ConditionExpression))
                    {
                        dialogueView.DisplayChoices(this, _currentConversation, ParsedConversations);
                    }
                }
            }

            // 显示对话内容
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

            // 获取左操作数的值
            var leftValue = GetVariableValue(conditionInfo.LeftOperand);
            if (leftValue == null && conditionInfo.IsVariableComparison)
            {
                // 变量不存在，返回 false
                return false;
            }

            // 获取右操作数的值
            var rightValue = GetVariableValue(conditionInfo.RightOperand);
            if (rightValue == null && !IsLiteralValue(conditionInfo.RightOperand))
            {
                rightValue = conditionInfo.RightOperand;
            }

            // 执行比较
            return CompareValues(leftValue, rightValue, conditionInfo.Operator);
        }

        /// <summary>
        /// 获取变量值
        /// </summary>
        protected virtual object GetVariableValue(string variableName)
        {
            if (string.IsNullOrEmpty(variableName)) return null;

            // 去掉 $ 前缀
            var key = variableName.TrimStart('$');

            // 从全局变量获取
            if (MspDialogueGlobals.GlobalVariables.TryGetValue(key, out var value))
            {
                // 尝试转换为数字或布尔值
                if (int.TryParse(value, out var intVal)) return intVal;
                if (float.TryParse(value, out var floatVal)) return floatVal;
                if (bool.TryParse(value, out var boolVal)) return boolVal;
                return value;
            }

            return null;
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

            // 尝试数值比较
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

            // 字符串比较
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

            // 依次按索引执行，保证字符串插入顺序稳定
            var insertedOffset = 0;
            foreach (var kv in functionInvocations.OrderBy(x => x.Key))
            {
                var invocation = kv.Value;
                if (invocation == null || string.IsNullOrEmpty(invocation.FunctionName)) continue;

                foreach (var method in methods)
                {
                    if (!string.Equals(method.Name, invocation.FunctionName, StringComparison.Ordinal)) continue;

                    // 构建参数数组
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
                return invocationArgs == null || invocationArgs.Count == 0 ? null : null; // 无参数方法
            }

            // 检查是否是引擎参数
            if (parameters.Length == 1 &&
                (parameters[0].ParameterType.IsAssignableFrom(typeof(MspDialogueEngineBase)) ||
                 parameters[0].ParameterType == typeof(MspDialogueEngineBase)))
            {
                return new object[] { this };
            }

            // 检查参数数量是否匹配
            if (invocationArgs == null || invocationArgs.Count != parameters.Length)
            {
                return null;
            }

            // 转换参数类型
            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var arg = invocationArgs[i];

                // 如果是变量类型，需要从全局变量获取值
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

            // 尝试类型转换
            if (targetType == typeof(string))
                return value.ToString();

            if (targetType == typeof(int))
            {
                if (value is int i) return i;
                if (int.TryParse(value.ToString(), out var parsed)) return parsed;
            }

            if (targetType == typeof(float))
            {
                if (value is float f) return f;
                if (float.TryParse(value.ToString(), out var parsed)) return parsed;
            }

            if (targetType == typeof(bool))
            {
                if (value is bool b) return b;
                if (bool.TryParse(value.ToString(), out var parsed)) return parsed;
            }

            return Convert.ChangeType(value, targetType);
        }

        public void TryDisplayNextLine()
        {
            if (_linePlaying) return;
            if (_currentConversation == null) return;
            if (dialogueView == null) return;

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