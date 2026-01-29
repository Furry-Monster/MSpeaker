using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSpeaker.Runtime.Interfaces;
using MSpeaker.Runtime.Parser;
using MSpeaker.Runtime.Plugins;
using MSpeaker.Runtime.Services;
using MSpeaker.Runtime.Utils;
using MSpeaker.Runtime.Views;
using UnityEngine;
using UnityEngine.Events;

namespace MSpeaker.Runtime
{
    public abstract class MspDialogueEngineBase : MonoBehaviour, IMspDialogueEngine
    {
        [Header("Dialogue Views")] [SerializeField]
        protected MspDialogueViewBase dialogueView;

        [Header("Function Invocations")] [SerializeField]
        private bool searchAllAssemblies;

        [SerializeField] private List<string> includedAssemblies = new();

        public UnityEvent PersistentOnConversationStart = new();
        public UnityEvent PersistentOnConversationEnd = new();

        [HideInInspector] public UnityEvent OnConversationStart = new();
        [HideInInspector] public UnityEvent OnConversationEnd = new();
        public UnityEvent OnConversationPaused = new();
        public UnityEvent OnConversationResumed = new();

        UnityEvent IMspDialogueEngine.OnConversationStart => OnConversationStart;
        UnityEvent IMspDialogueEngine.OnConversationEnd => OnConversationEnd;
        UnityEvent IMspDialogueEngine.OnConversationPaused => OnConversationPaused;
        UnityEvent IMspDialogueEngine.OnConversationResumed => OnConversationResumed;

        public List<MspConversation> ParsedConversations { get; protected set; }
        public IMspDialogueView View => dialogueView;

        protected MspConversation _currentConversation;
        protected int _lineIndex;
        protected bool _linePlaying;
        protected bool _isPaused;
        private Coroutine _displayCoroutine;

        private readonly Dictionary<int, int> _loopCounters = new();
        private bool _skippingConditionalBlock;
        private int _conditionalBlockEndIndex = -1;

        // Services
        protected IMspVariableService _variableService;
        protected IMspConditionEvaluator _conditionEvaluator;
        protected IMspFunctionInvoker _functionInvoker;

        // Plugins
        protected MspEnginePlugin[] _plugins;
        protected readonly MspPluginContext _pluginContext = new();

        protected virtual void Awake()
        {
            InitializeServices();
        }

        protected virtual void InitializeServices()
        {
            _variableService = new MspVariableService();
            _conditionEvaluator = new MspConditionEvaluator(_variableService);
            _functionInvoker = new MspFunctionInvoker(_variableService, searchAllAssemblies, includedAssemblies);

            // 同步静态全局变量到服务
            foreach (var kv in MspDialogueGlobals.GlobalVariables)
                _variableService.SetValue(kv.Key, kv.Value);
        }

        public void StartConversation(MspDialogueAsset dialogueAsset, int startIndex = 0)
        {
            if (dialogueAsset == null) throw new ArgumentNullException(nameof(dialogueAsset));

            ParsedConversations = MspDialogueParser.Parse(dialogueAsset);

            if (startIndex < 0 || startIndex >= ParsedConversations.Count)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            _plugins = GetComponents<MspEnginePlugin>();
            SortPlugins();
            SwitchConversation(ParsedConversations[startIndex]);
        }

        private void SortPlugins()
        {
            if (_plugins == null || _plugins.Length <= 1) return;
            Array.Sort(_plugins, (a, b) => a.Priority.CompareTo(b.Priority));
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

            UpdatePluginContext();
            NotifyPlugins(p => p.OnConversationStart(_pluginContext));

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
                dialogueView.ClearView();

            NotifyPlugins(p => p.OnClear());

            _linePlaying = false;
            _lineIndex = 0;
            _currentConversation = null;
            _isPaused = false;
            _loopCounters.Clear();
            ResetConditionalState();

            if (hadActiveConversation)
            {
                UpdatePluginContext();
                NotifyPlugins(p => p.OnConversationEnd(_pluginContext));
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

            UpdatePluginContext();
            NotifyPlugins(p => p.OnPause(_pluginContext));
            OnConversationPaused.Invoke();
        }

        public void ResumeConversation()
        {
            if (_currentConversation == null || !_isPaused) return;
            _isPaused = false;
            if (dialogueView != null) dialogueView.Resume();

            UpdatePluginContext();
            NotifyPlugins(p => p.OnResume(_pluginContext));
            OnConversationResumed.Invoke();
        }

        public bool IsConversationPaused() => _isPaused;

        public void JumpTo(string conversationName)
        {
            if (ParsedConversations == null || ParsedConversations.Count == 0)
                throw new InvalidOperationException("No conversation active.");

            var conversation = ParsedConversations.Find(c => c.Name == conversationName);
            if (conversation == null)
                throw new ArgumentException($"Conversation \"{conversationName}\" not found.",
                    nameof(conversationName));

            SwitchConversation(conversation);
        }

        public void TryDisplayNextLine()
        {
            if (_linePlaying || _currentConversation == null || dialogueView == null)
                return;

            if (HasChoicesAtLine(_lineIndex) || HasChoicesAtNextEndIf())
                return;

            dialogueView.ClearView();
            NotifyPlugins(p => p.OnClear());

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

        protected virtual IEnumerator DisplayDialogue()
        {
            if (dialogueView == null || !ValidateLineIndex())
            {
                _linePlaying = false;
                yield break;
            }

            _linePlaying = true;
            var currentLine = _currentConversation.Lines[_lineIndex];

            // 处理控制流行
            var controlResult = HandleControlFlowLine(currentLine);
            if (controlResult.HasValue)
            {
                if (controlResult.Value >= 0)
                    yield return StartCoroutine(JumpToLineAndContinue(controlResult.Value));
                else
                    yield return StartCoroutine(ContinueToNextLine());
                yield break;
            }

            // 处理条件跳过
            if (_skippingConditionalBlock && _conditionalBlockEndIndex >= 0 && _lineIndex < _conditionalBlockEndIndex)
            {
                yield return StartCoroutine(ContinueToNextLine());
                yield break;
            }

            // 显示对话
            UpdatePluginContext();
            NotifyPlugins(p => p.OnBeforeLineDisplay(_pluginContext, currentLine));

            TryDisplayChoices();
            dialogueView.SetView(_currentConversation, _lineIndex);

            // 插件显示并等待
            yield return StartCoroutine(NotifyPluginsLineDisplay());

            _functionInvoker.Invoke(currentLine.LineContent?.Invocations, currentLine.LineContent, this);
            if (currentLine.LineContent?.Invocations?.Count > 0)
                dialogueView.SetView(_currentConversation, _lineIndex);

            yield return new WaitUntil(() => !dialogueView.IsStillDisplaying() || _isPaused);
            if (_isPaused) yield return new WaitUntil(() => !_isPaused);

            NotifyPlugins(p => p.OnLineComplete(_pluginContext));
            _linePlaying = false;
        }

        private bool ValidateLineIndex()
        {
            if (_currentConversation?.Lines == null || _lineIndex < 0 || _lineIndex >= _currentConversation.Lines.Count)
            {
                MspDialogueLogger.LogError(-1, $"Invalid line index: {_lineIndex}", this);
                return false;
            }

            return true;
        }

        // 返回跳转目标行号，-1表示继续下一行，null表示不是控制流行
        private int? HandleControlFlowLine(MspLine line)
        {
            switch (line.LineType)
            {
                case MspLineType.Label:
                    return -1;

                case MspLineType.Goto:
                    if (_currentConversation.Labels?.TryGetValue(line.LabelName, out var targetIndex) == true)
                        return targetIndex;
                    MspDialogueLogger.LogError(-1, $"Label not found: {line.LabelName}", this);
                    _linePlaying = false;
                    return null;

                case MspLineType.LoopStart:
                    return HandleLoopStart(line);

                case MspLineType.LoopEnd:
                    return HandleLoopEnd();

                case MspLineType.IfStart:
                    return HandleIfStart(line);

                case MspLineType.Else:
                    return HandleElse();

                case MspLineType.EndIf:
                    return HandleEndIf();

                default:
                    return null;
            }
        }

        private int? HandleLoopStart(MspLine line)
        {
            if (!_loopCounters.ContainsKey(_lineIndex))
            {
                var loopCount = EvaluateLoopCount(line.LoopInfo);
                _loopCounters[_lineIndex] = loopCount;
            }

            if (_loopCounters[_lineIndex] > 0)
            {
                _loopCounters[_lineIndex]--;
                return _lineIndex + 1;
            }

            return line.LoopInfo?.LoopEndLineIndex >= 0 ? line.LoopInfo.LoopEndLineIndex : -1;
        }

        private int? HandleLoopEnd()
        {
            var loopStart = _currentConversation.Lines
                .Select((l, idx) => new { l, idx })
                .FirstOrDefault(x => x.l.LineType == MspLineType.LoopStart &&
                                     x.l.LoopInfo?.LoopEndLineIndex == _lineIndex);

            if (loopStart != null && _loopCounters.TryGetValue(loopStart.idx, out var count) && count > 0)
                return loopStart.idx;

            return -1;
        }

        private int? HandleIfStart(MspLine line)
        {
            var block = _currentConversation.ConditionalBlocks?.GetValueOrDefault(_lineIndex);
            if (block == null) return null;

            var conditionMet = _conditionEvaluator.Evaluate(block.ConditionExpression);

            if (conditionMet)
            {
                _skippingConditionalBlock = false;
                _conditionalBlockEndIndex = block.EndIfLineIndex;
                return null;
            }

            _skippingConditionalBlock = true;
            if (block.ElseLineIndex >= 0)
            {
                _skippingConditionalBlock = false;
                _conditionalBlockEndIndex = block.EndIfLineIndex;
                return block.ElseLineIndex + 1;
            }

            ResetConditionalState();
            return block.EndIfLineIndex;
        }

        private int? HandleElse()
        {
            var block = _currentConversation.ConditionalBlocks?.Values
                .FirstOrDefault(b => b.ElseLineIndex == _lineIndex);
            if (block != null)
            {
                ResetConditionalState();
                return block.EndIfLineIndex;
            }

            return null;
        }

        private int? HandleEndIf()
        {
            ResetConditionalState();

            if (HasChoicesAtLine(_lineIndex))
            {
                var previousLineIndex = FindPreviousNormalLine(_lineIndex - 1);
                if (previousLineIndex >= 0)
                    return previousLineIndex;
            }

            return -1;
        }

        protected virtual int EvaluateLoopCount(MspLoopInfo loopInfo)
        {
            if (loopInfo == null) return 1;

            var expression = loopInfo.LoopCountExpression;
            if (string.IsNullOrEmpty(expression))
                return loopInfo.LoopCount;

            if (expression.StartsWith("$"))
            {
                var varValue = _variableService.GetValue(expression);
                if (varValue is int intVal) return intVal;
                if (int.TryParse(varValue?.ToString(), out var parsed)) return parsed;
                return 1;
            }

            return int.TryParse(expression, out var count) ? count : loopInfo.LoopCount;
        }

        private void ResetConditionalState()
        {
            _skippingConditionalBlock = false;
            _conditionalBlockEndIndex = -1;
        }

        private bool HasChoicesAtLine(int lineIndex)
        {
            return _currentConversation?.Choices?.Any(x => x.Value == lineIndex) == true;
        }

        private bool HasChoicesAtNextEndIf()
        {
            var nextLineIndex = _lineIndex + 1;
            if (nextLineIndex >= _currentConversation.Lines.Count) return false;
            var nextLine = _currentConversation.Lines[nextLineIndex];
            return nextLine.LineType == MspLineType.EndIf && HasChoicesAtLine(nextLineIndex);
        }

        private int FindPreviousNormalLine(int startIndex)
        {
            for (var i = startIndex; i >= 0; i--)
            {
                var line = _currentConversation.Lines[i];
                if (line.LineType == MspLineType.Normal && !string.IsNullOrWhiteSpace(line.LineContent?.Text))
                    return i;
            }

            return -1;
        }

        private void TryDisplayChoices()
        {
            if (_currentConversation?.Choices is not { Count: > 0 }) return;

            var foundChoice = _currentConversation.Choices.FirstOrDefault(x => x.Value == _lineIndex);
            if (foundChoice.Key != null)
            {
                DisplayChoicesIfConditionMet(foundChoice.Key);
                return;
            }

            var nextLineIndex = _lineIndex + 1;
            if (nextLineIndex < _currentConversation.Lines.Count &&
                _currentConversation.Lines[nextLineIndex].LineType == MspLineType.EndIf)
            {
                var endifChoice = _currentConversation.Choices.FirstOrDefault(x => x.Value == nextLineIndex);
                if (endifChoice.Key != null)
                    DisplayChoicesIfConditionMet(endifChoice.Key);
            }
        }

        private void DisplayChoicesIfConditionMet(MspChoice choice)
        {
            if (_conditionEvaluator.EvaluateChoice(choice.ConditionExpression))
            {
                var choices = _currentConversation.Choices.Keys.ToList();
                NotifyPlugins(p => p.OnBeforeChoicesDisplay(_pluginContext, choices));
                dialogueView.DisplayChoices(this, _currentConversation, ParsedConversations);
            }
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

        private void UpdatePluginContext()
        {
            _pluginContext.Update(this, _currentConversation, _lineIndex, _isPaused);
        }

        private void NotifyPlugins(Action<MspEnginePlugin> action)
        {
            if (_plugins == null) return;
            foreach (var plugin in _plugins)
                action(plugin);
        }

        private IEnumerator NotifyPluginsLineDisplay()
        {
            if (_plugins == null) yield break;

            foreach (var plugin in _plugins)
            {
                var result = plugin.OnLineDisplay(_pluginContext);
                if (result.ShouldWait)
                    yield return new WaitUntil(() => plugin.IsComplete);
            }
        }
    }
}