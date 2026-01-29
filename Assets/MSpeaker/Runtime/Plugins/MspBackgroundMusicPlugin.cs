using MSpeaker.Runtime.Interfaces;
using UnityEngine;

namespace MSpeaker.Runtime.Plugins
{
    public sealed class MspBackgroundMusicPlugin : MspEnginePlugin
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private bool createAudioSourceIfMissing = true;
        [SerializeField] private bool fadeInOnStart = true;
        [SerializeField] private float fadeInDuration = 1f;
        [SerializeField] private bool fadeOutOnEnd = true;
        [SerializeField] private float fadeOutDuration = 1f;

        [Header("Music Settings")] [SerializeField]
        private AudioClip defaultMusic;

        [SerializeField] private bool loop = true;
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;

        private AudioSource _cachedMusicSource;
        private Coroutine _fadeCoroutine;
        private float _targetVolume;

        private void Awake()
        {
            if (musicSource == null && createAudioSourceIfMissing)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = loop;
            }

            _cachedMusicSource = musicSource;
            _targetVolume = volume;
        }

        public override void OnConversationStart(IMspPluginContext context)
        {
            if (_cachedMusicSource == null) return;

            var musicClip = GetMusicClip(context);
            if (musicClip != null && _cachedMusicSource.clip != musicClip)
            {
                PlayMusic(musicClip);
            }
            else if (_cachedMusicSource.clip != null && !_cachedMusicSource.isPlaying)
            {
                PlayMusic(_cachedMusicSource.clip);
            }
        }

        public override void OnConversationEnd(IMspPluginContext context)
        {
            if (_cachedMusicSource == null) return;

            if (fadeOutOnEnd)
            {
                FadeOut();
            }
            else
            {
                _cachedMusicSource.Stop();
            }
        }

        public override void OnPause(IMspPluginContext context)
        {
            if (_cachedMusicSource != null && _cachedMusicSource.isPlaying)
            {
                _cachedMusicSource.Pause();
            }
        }

        public override void OnResume(IMspPluginContext context)
        {
            if (_cachedMusicSource != null && !_cachedMusicSource.isPlaying && _cachedMusicSource.clip != null)
            {
                _cachedMusicSource.UnPause();
            }
        }

        public override void OnClear()
        {
            if (_cachedMusicSource != null)
            {
                if (fadeOutOnEnd)
                {
                    FadeOut();
                }
                else
                {
                    _cachedMusicSource.Stop();
                }
            }
        }

        private void PlayMusic(AudioClip clip)
        {
            if (_cachedMusicSource == null || clip == null) return;

            _cachedMusicSource.clip = clip;
            _cachedMusicSource.volume = fadeInOnStart ? 0f : _targetVolume;
            _cachedMusicSource.Play();

            if (fadeInOnStart)
            {
                FadeIn();
            }
        }

        private void FadeIn()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeCoroutine(0f, _targetVolume, fadeInDuration));
        }

        private void FadeOut()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            if (_cachedMusicSource != null && _cachedMusicSource.isPlaying)
            {
                _fadeCoroutine = StartCoroutine(FadeCoroutine(_cachedMusicSource.volume, 0f, fadeOutDuration, true));
            }
        }

        private System.Collections.IEnumerator FadeCoroutine(float from, float to, float duration,
            bool stopOnComplete = false)
        {
            if (_cachedMusicSource == null) yield break;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _cachedMusicSource.volume = Mathf.Lerp(from, to, t);
                yield return null;
            }

            _cachedMusicSource.volume = to;

            if (stopOnComplete)
            {
                _cachedMusicSource.Stop();
            }

            _fadeCoroutine = null;
        }

        private AudioClip GetMusicClip(IMspPluginContext context)
        {
            if (context?.CurrentConversation?.Lines == null || context.CurrentConversation.Lines.Count == 0)
                return defaultMusic;

            var firstLine = context.CurrentConversation.Lines[0];
            if (firstLine?.LineContent?.Metadata != null &&
                firstLine.LineContent.Metadata.TryGetValue("bgm", out var bgmPath))
            {
                if (!string.IsNullOrEmpty(bgmPath))
                {
                    return Resources.Load<AudioClip>(bgmPath) ?? defaultMusic;
                }
            }

            return defaultMusic;
        }
    }
}