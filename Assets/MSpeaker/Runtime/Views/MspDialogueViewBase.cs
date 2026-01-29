using System.Collections.Generic;
using System.Linq;
using MSpeaker.Runtime.Interfaces;
using MSpeaker.Runtime.Parser;
using MSpeaker.Runtime.Services;
using MSpeaker.Runtime.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MSpeaker.Runtime.Views
{
    public class MspDialogueViewBase : MonoBehaviour, IMspDialogueView
    {
        [SerializeField] protected TextMeshProUGUI nameText;
        [SerializeField] protected TextMeshProUGUI sentenceText;

        [Header("Choice UI")] [SerializeField] protected Transform choiceButtonHolder;
        [SerializeField] protected GameObject choiceButtonPrefab;

        protected bool _isStillDisplaying;
        protected bool _isPaused;
        protected readonly List<MspChoiceButton> _choiceButtonInstances = new();

        private IMspConditionEvaluator _conditionEvaluator;

        public UnityEvent OnSetView { get; } = new();
        public UnityEvent OnLineComplete { get; } = new();

        public virtual bool IsStillDisplaying() => _isStillDisplaying;

        public virtual void SetView(MspConversation conversation, int lineIndex)
        {
            if (conversation?.Lines == null) return;
            if (lineIndex < 0 || lineIndex >= conversation.Lines.Count) return;

            if (nameText != null)
                nameText.text = conversation.Lines[lineIndex].Speaker ?? string.Empty;
            if (sentenceText != null)
                sentenceText.text = conversation.Lines[lineIndex].LineContent?.Text ?? string.Empty;

            OnSetView.Invoke();
            OnLineComplete.Invoke();
        }

        public virtual void ClearView()
        {
            if (nameText != null) nameText.text = string.Empty;
            if (sentenceText != null) sentenceText.text = string.Empty;

            ClearChoiceButtons();
        }

        protected void ClearChoiceButtons()
        {
            foreach (var choiceButton in _choiceButtonInstances)
            {
                if (choiceButton == null) continue;
                choiceButton.OnChoiceClick.RemoveAllListeners();
                Destroy(choiceButton.gameObject);
            }

            _choiceButtonInstances.Clear();
        }

        public virtual void SkipViewEffect()
        {
        }

        public virtual void Pause() => _isPaused = true;

        public virtual void Resume() => _isPaused = false;

        public virtual bool IsPaused() => _isPaused;

        public virtual void DisplayChoices(
            IMspDialogueEngine engine,
            MspConversation conversation,
            List<MspConversation> parsedConversations)
        {
            ClearChoiceButtons();

            if (engine == null || conversation?.Choices == null || conversation.Choices.Count == 0)
                return;

            if (choiceButtonHolder == null || choiceButtonPrefab == null)
            {
                MspDialogueLogger.LogWarning(-1, "Choice UI 未配置。", this);
                return;
            }

            EnsureConditionEvaluator();

            foreach (var choice in conversation.Choices.Keys.Where(ShouldDisplayChoice))
            {
                var instance = Instantiate(choiceButtonPrefab, choiceButtonHolder);
                var choiceButton = instance.GetComponent<MspChoiceButton>();

                if (choiceButton == null)
                {
                    MspDialogueLogger.LogError(-1, "ChoiceButtonPrefab 缺少 MspChoiceButton 组件。", this);
                    Destroy(instance);
                    continue;
                }

                var conversationIndex = parsedConversations.FindIndex(c => c.Name == choice.LeadingConversationName);
                if (conversationIndex < 0)
                {
                    MspDialogueLogger.LogError(-1,
                        $"Choice \"{choice.ChoiceName}\" 指向的会话 \"{choice.LeadingConversationName}\" 不存在。", this);
                }
                else
                {
                    var targetConversation = parsedConversations[conversationIndex];
                    choiceButton.OnChoiceClick.AddListener(() => engine.SwitchConversation(targetConversation));
                }

                var tmp = instance.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = choice.ChoiceName;

                _choiceButtonInstances.Add(choiceButton);
            }
        }

        private void EnsureConditionEvaluator()
        {
            if (_conditionEvaluator != null) return;

            var variableService = new MspVariableService();
            foreach (var kv in MspDialogueGlobals.GlobalVariables)
                variableService.SetValue(kv.Key, kv.Value);

            _conditionEvaluator = new MspConditionEvaluator(variableService);
        }

        protected virtual bool ShouldDisplayChoice(MspChoice choice)
        {
            if (string.IsNullOrEmpty(choice?.ConditionExpression))
                return true;

            EnsureConditionEvaluator();
            return _conditionEvaluator.EvaluateChoice(choice.ConditionExpression);
        }
    }
}