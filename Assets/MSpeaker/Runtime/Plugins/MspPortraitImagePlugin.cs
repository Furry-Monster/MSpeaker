using System.Collections.Generic;
using MSpeaker.Runtime.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace MSpeaker.Runtime.Plugins
{
    public sealed class MspPortraitImagePlugin : MspEnginePlugin
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private bool hideWhenNull = true;

        private readonly Dictionary<string, Sprite> _spriteCache = new();

        public override MspPluginResult OnLineDisplay(IMspPluginContext context)
        {
            if (portraitImage == null || context.CurrentLine == null)
                return MspPluginResult.Continue;

            var path = context.CurrentLine.SpeakerImagePath;
            var sprite = LoadSprite(path);
            portraitImage.sprite = sprite;

            if (hideWhenNull)
                portraitImage.gameObject.SetActive(sprite != null);

            return MspPluginResult.Continue;
        }

        public override void OnClear()
        {
            if (portraitImage == null) return;
            portraitImage.sprite = null;
            if (hideWhenNull)
                portraitImage.gameObject.SetActive(false);
        }

        private Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (_spriteCache.TryGetValue(path, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>(path);
            _spriteCache[path] = sprite;
            return sprite;
        }
    }
}
