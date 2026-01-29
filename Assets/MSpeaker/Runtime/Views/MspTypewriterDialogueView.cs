using System.Collections;
using MSpeaker.Runtime.Parser;
using UnityEngine;

namespace MSpeaker.Runtime.Views
{
    public sealed class MspTypewriterDialogueView : MspDialogueViewBase
    {
        [SerializeField, Min(1f)] private float charactersPerSecond = 40f;

        private Coroutine _typeCoroutine;
        private string _fullText;

        public override void SetView(MspConversation conversation, int lineIndex)
        {
            if (conversation?.Lines == null) return;
            if (lineIndex < 0 || lineIndex >= conversation.Lines.Count) return;

            if (nameText != null)
                nameText.text = conversation.Lines[lineIndex].Speaker ?? string.Empty;

            _fullText = conversation.Lines[lineIndex].LineContent?.Text ?? string.Empty;
            if (sentenceText != null) sentenceText.text = string.Empty;

            if (_typeCoroutine != null)
            {
                StopCoroutine(_typeCoroutine);
                _typeCoroutine = null;
            }

            _isStillDisplaying = true;
            _typeCoroutine = StartCoroutine(TypeRoutine());

            OnSetView.Invoke();
        }

        public override void ClearView()
        {
            if (_typeCoroutine != null)
            {
                StopCoroutine(_typeCoroutine);
                _typeCoroutine = null;
            }

            _isStillDisplaying = false;
            _fullText = string.Empty;
            base.ClearView();
        }

        public override void SkipViewEffect()
        {
            if (!_isStillDisplaying) return;
            if (sentenceText != null) sentenceText.text = _fullText ?? string.Empty;
            FinishTypewriter();
        }

        private IEnumerator TypeRoutine()
        {
            if (sentenceText == null)
            {
                FinishTypewriter();
                yield break;
            }

            var t = 0f;
            var shown = 0;
            var fullTextLength = _fullText?.Length ?? 0;

            while (shown < fullTextLength)
            {
                if (_isPaused)
                {
                    yield return null;
                    continue;
                }

                t += Time.unscaledDeltaTime * charactersPerSecond;
                var nextShown = Mathf.Clamp(Mathf.FloorToInt(t), 0, fullTextLength);
                if (nextShown != shown)
                {
                    shown = nextShown;
                    sentenceText.text = _fullText?[..shown];
                }

                yield return null;
            }

            sentenceText.text = _fullText ?? string.Empty;
            FinishTypewriter();
        }

        private void FinishTypewriter()
        {
            if (_typeCoroutine != null)
            {
                StopCoroutine(_typeCoroutine);
                _typeCoroutine = null;
            }

            _isStillDisplaying = false;
            OnLineComplete.Invoke();
        }
    }
}