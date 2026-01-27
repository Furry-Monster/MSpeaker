using System.Collections.Generic;
using System.Linq;
using MSpeaker.Runtime.Parser;
using MSpeaker.Runtime.Plugins;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MSpeaker.Runtime.Views
{
    public class MspDialogueViewBase : MonoBehaviour
    {
        [SerializeField] protected TextMeshProUGUI nameText;
        [SerializeField] protected TextMeshProUGUI sentenceText;

        protected bool _isStillDisplaying;
        protected bool _isPaused;

        [Header("Choice UI")] [SerializeField] protected Transform choiceButtonHolder;
        [SerializeField] protected GameObject choiceButtonPrefab;
        protected readonly List<MspChoiceButton> _choiceButtonInstances = new();

        public UnityEvent OnSetView = new();
        public UnityEvent OnLineComplete = new();

        public virtual bool IsStillDisplaying() => _isStillDisplaying;

        public virtual void SetView(MspConversation conversation, int lineIndex)
        {
            if (conversation?.Lines == null) return;
            if (lineIndex < 0 || lineIndex >= conversation.Lines.Count) return;

            if (nameText != null) nameText.text = conversation.Lines[lineIndex].Speaker ?? string.Empty;
            if (sentenceText != null)
                sentenceText.text = conversation.Lines[lineIndex].LineContent?.Text ?? string.Empty;

            OnSetView.Invoke();
            OnLineComplete.Invoke();
        }

        public virtual void ClearView(MspEnginePlugin[] enginePlugins)
        {
            if (nameText != null) nameText.text = string.Empty;
            if (sentenceText != null) sentenceText.text = string.Empty;

            if (enginePlugins != null)
                foreach (var plugin in enginePlugins)
                    plugin.Clear();

            if (_choiceButtonInstances.Count > 0)
            {
                foreach (var choiceButton in _choiceButtonInstances)
                {
                    if (choiceButton == null) continue;
                    choiceButton.OnChoiceClick.RemoveAllListeners();
                    Destroy(choiceButton.gameObject);
                }

                _choiceButtonInstances.Clear();
            }
        }

        public virtual void SkipViewEffect()
        {
        }

        public virtual void Pause()
        {
            _isPaused = true;
        }

        public virtual void Resume()
        {
            _isPaused = false;
        }

        public virtual bool IsPaused() => _isPaused;

        public virtual void DisplayChoices(MspDialogueEngineBase engine, MspConversation conversation,
            List<MspConversation> parsedConversations)
        {
            _choiceButtonInstances.Clear();

            if (engine == null || conversation?.Choices == null || conversation.Choices.Count == 0)
                return;

            if (choiceButtonHolder == null || choiceButtonPrefab == null)
            {
                MspDialogueLogger.LogWarning(-1,
                    "Choice UI 未配置：请在 View 上设置 choiceButtonHolder 与 choiceButtonPrefab。", this);
                return;
            }

            foreach (var choice in conversation.Choices.Keys.ToList())
            {
                var instance = Instantiate(choiceButtonPrefab, choiceButtonHolder);
                var choiceButton = instance.GetComponent<MspChoiceButton>();
                if (choiceButton == null)
                {
                    MspDialogueLogger.LogError(-1,
                        "ChoiceButtonPrefab 缺少 MspChoiceButton 组件。", this);
                    Destroy(instance);
                    continue;
                }

                var conversationIndex = parsedConversations.FindIndex(c => c.Name == choice.LeadingConversationName);
                if (conversationIndex < 0)
                {
                    MspDialogueLogger.LogError(-1,
                        $"Choice \"{choice.ChoiceName}\" 指向的会话 \"{choice.LeadingConversationName}\" 不存在（位于会话 \"{conversation.Name}\"）。",
                        this);
                }
                else
                {
                    choiceButton.OnChoiceClick.AddListener(() =>
                        engine.SwitchConversation(parsedConversations[conversationIndex]));
                }

                // 尝试给按钮文本赋值（优先 TMP）
                var tmp = instance.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = choice.ChoiceName;

                _choiceButtonInstances.Add(choiceButton);
            }
        }
    }
}