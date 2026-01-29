using System.Collections;
using System.Collections.Generic;
using MSpeaker.Runtime.Interfaces;
using UnityEngine;

namespace MSpeaker.Runtime.Plugins
{
    public sealed class MspVisualEffectPlugin : MspEnginePlugin
    {
        [SerializeField] private CanvasGroup targetCanvasGroup;
        [SerializeField] private bool createCanvasGroupIfMissing = true;

        [Header("Effect Settings")] [SerializeField]
        private bool fadeInOnLine = true;

        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private bool fadeOutOnClear = false;
        [SerializeField] private float fadeOutDuration = 0.3f;

        [Header("Shake Effect")] [SerializeField]
        private bool enableShake = false;

        [SerializeField] private float shakeIntensity = 5f;
        [SerializeField] private float shakeDuration = 0.2f;

        private CanvasGroup _cachedCanvasGroup;
        private Coroutine _fadeCoroutine;
        private Coroutine _shakeCoroutine;
        private Vector3 _originalPosition;
        private RectTransform _rectTransform;

        private void Awake()
        {
            if (targetCanvasGroup == null && createCanvasGroupIfMissing)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    targetCanvasGroup = canvas.GetComponent<CanvasGroup>();
                    if (targetCanvasGroup == null)
                    {
                        targetCanvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                    }
                }
            }

            _cachedCanvasGroup = targetCanvasGroup;
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null)
            {
                _originalPosition = _rectTransform.localPosition;
            }
        }

        public override MspPluginResult OnLineDisplay(IMspPluginContext context)
        {
            if (_cachedCanvasGroup == null) return MspPluginResult.Continue;

            var effectType = GetEffectType(context);
            ApplyEffect(effectType);

            return MspPluginResult.Continue;
        }

        public override void OnClear()
        {
            if (_cachedCanvasGroup == null) return;

            if (fadeOutOnClear)
            {
                FadeOut();
            }
            else
            {
                ResetEffect();
            }
        }

        private void ApplyEffect(string effectType)
        {
            switch (effectType?.ToLower())
            {
                case "fadein":
                case "fade":
                    if (fadeInOnLine) FadeIn();
                    break;

                case "shake":
                    if (enableShake) Shake();
                    break;

                case "flash":
                    Flash();
                    break;

                default:
                    if (fadeInOnLine) FadeIn();
                    break;
            }
        }

        private string GetEffectType(IMspPluginContext context)
        {
            if (context.CurrentLine?.LineContent?.Metadata == null)
                return null;

            return context.CurrentLine.LineContent.Metadata.GetValueOrDefault("effect");
        }

        private void FadeIn()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeCoroutine(0f, 1f, fadeInDuration));
        }

        private void FadeOut()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeCoroutine(_cachedCanvasGroup.alpha, 0f, fadeOutDuration));
        }

        private IEnumerator FadeCoroutine(float from, float to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _cachedCanvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            _cachedCanvasGroup.alpha = to;
            _fadeCoroutine = null;
        }

        private void Shake()
        {
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
            }

            if (_rectTransform != null)
            {
                _shakeCoroutine = StartCoroutine(ShakeCoroutine());
            }
        }

        private IEnumerator ShakeCoroutine()
        {
            var elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var offset = Random.insideUnitCircle * shakeIntensity;
                _rectTransform.localPosition = _originalPosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            _rectTransform.localPosition = _originalPosition;
            _shakeCoroutine = null;
        }

        private void Flash()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            _cachedCanvasGroup.alpha = 1f;
            yield return new WaitForSeconds(0.1f);
            _cachedCanvasGroup.alpha = 0.3f;
            yield return new WaitForSeconds(0.1f);
            _cachedCanvasGroup.alpha = 1f;
            _fadeCoroutine = null;
        }

        private void ResetEffect()
        {
            if (_cachedCanvasGroup != null)
            {
                _cachedCanvasGroup.alpha = 1f;
            }

            if (_rectTransform != null)
            {
                _rectTransform.localPosition = _originalPosition;
            }

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = null;
            }
        }
    }
}