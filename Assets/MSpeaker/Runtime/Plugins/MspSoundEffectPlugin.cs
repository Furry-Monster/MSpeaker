using System;
using System.Collections.Generic;
using MSpeaker.Runtime.Interfaces;
using UnityEngine;

namespace MSpeaker.Runtime.Plugins
{
    public sealed class MspSoundEffectPlugin : MspEnginePlugin
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool createAudioSourceIfMissing = true;
        [SerializeField] private bool stopOnClear = true;

        [Header("Sound Mapping")] [SerializeField]
        private List<SpeakerSoundMapping> speakerSoundMappings = new();

        [Serializable]
        private class SpeakerSoundMapping
        {
            public string speakerName;
            public AudioClip soundClip;
        }

        private readonly Dictionary<string, AudioClip> _speakerSoundCache = new();
        private AudioSource _cachedAudioSource;

        private void Awake()
        {
            if (audioSource == null && createAudioSourceIfMissing)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            _cachedAudioSource = audioSource;

            foreach (var mapping in speakerSoundMappings)
            {
                if (!string.IsNullOrEmpty(mapping.speakerName) && mapping.soundClip != null)
                {
                    _speakerSoundCache[mapping.speakerName] = mapping.soundClip;
                }
            }
        }

        public override MspPluginResult OnLineDisplay(IMspPluginContext context)
        {
            if (_cachedAudioSource == null || context.CurrentLine == null)
                return MspPluginResult.Continue;

            var soundClip = GetSoundClip(context);
            if (soundClip != null)
            {
                _cachedAudioSource.PlayOneShot(soundClip);
            }

            return MspPluginResult.Continue;
        }

        public override void OnClear()
        {
            if (_cachedAudioSource != null && stopOnClear)
            {
                _cachedAudioSource.Stop();
            }
        }

        private AudioClip GetSoundClip(IMspPluginContext context)
        {
            var line = context.CurrentLine;

            if (line.LineContent?.Metadata != null)
            {
                if (line.LineContent.Metadata.TryGetValue("sound", out var soundPath))
                {
                    return LoadSoundFromPath(soundPath);
                }
            }

            if (!string.IsNullOrEmpty(line.Speaker))
            {
                if (_speakerSoundCache.TryGetValue(line.Speaker, out var clip))
                {
                    return clip;
                }
            }

            return null;
        }

        private AudioClip LoadSoundFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (_speakerSoundCache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var clip = Resources.Load<AudioClip>(path);
            if (clip != null)
            {
                _speakerSoundCache[path] = clip;
            }

            return clip;
        }
    }
}