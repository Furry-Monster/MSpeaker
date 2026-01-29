using System;
using System.Collections.Generic;
using MSpeaker.Runtime.Interfaces;
using UnityEngine;

namespace MSpeaker.Runtime.Plugins
{
    public sealed class MspCharacterAnimationPlugin : MspEnginePlugin
    {
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private bool createAnimatorIfMissing = false;

        [Header("Animation Mapping")] [SerializeField]
        private List<SpeakerAnimationMapping> speakerAnimationMappings = new();

        [Serializable]
        private class SpeakerAnimationMapping
        {
            public string speakerName;
            public string animationStateName;
            public string animationTriggerName;
        }

        private readonly Dictionary<string, SpeakerAnimationMapping> _speakerAnimationCache = new();
        private Animator _cachedAnimator;
        private string _currentSpeaker;

        private void Awake()
        {
            if (characterAnimator == null && createAnimatorIfMissing)
            {
                characterAnimator = gameObject.AddComponent<Animator>();
            }

            _cachedAnimator = characterAnimator;

            foreach (var mapping in speakerAnimationMappings)
            {
                if (!string.IsNullOrEmpty(mapping.speakerName))
                {
                    _speakerAnimationCache[mapping.speakerName] = mapping;
                }
            }
        }

        public override MspPluginResult OnLineDisplay(IMspPluginContext context)
        {
            if (_cachedAnimator == null || context.CurrentLine == null)
                return MspPluginResult.Continue;

            var speaker = context.CurrentLine.Speaker;
            if (string.IsNullOrEmpty(speaker))
                return MspPluginResult.Continue;

            if (speaker != _currentSpeaker)
            {
                _currentSpeaker = speaker;
                PlaySpeakerAnimation(speaker);
            }

            var animationTrigger = GetAnimationTrigger(context);
            if (!string.IsNullOrEmpty(animationTrigger))
            {
                _cachedAnimator.SetTrigger(animationTrigger);
            }

            return MspPluginResult.Continue;
        }

        public override void OnClear()
        {
            _currentSpeaker = null;
        }

        private void PlaySpeakerAnimation(string speaker)
        {
            if (!_speakerAnimationCache.TryGetValue(speaker, out var mapping))
                return;

            if (!string.IsNullOrEmpty(mapping.animationStateName))
            {
                _cachedAnimator.Play(mapping.animationStateName);
            }
        }

        private string GetAnimationTrigger(IMspPluginContext context)
        {
            if (context.CurrentLine?.LineContent?.Metadata == null)
                return null;

            if (context.CurrentLine.LineContent.Metadata.TryGetValue("anim", out var triggerName))
            {
                return triggerName;
            }

            if (_speakerAnimationCache.TryGetValue(context.CurrentLine.Speaker ?? "", out var mapping))
            {
                return mapping.animationTriggerName;
            }

            return null;
        }
    }
}