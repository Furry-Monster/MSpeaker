using MSpeaker.Runtime.Parser;
using UnityEngine;
using UnityEngine.UI;

namespace MSpeaker.Runtime.Plugins
{
    /// <summary>
    /// 读取解析出的 SpeakerImage 并显示到 UI Image 上。
    /// 配合 .msp 中的 {{Image(path)}} 使用（path 为 Resources 相对路径）。
    /// </summary>
    public sealed class MspPortraitImagePlugin : MspEnginePlugin
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private bool hideWhenNull = true;

        public override void Display(MspConversation conversation, int lineIndex)
        {
            if (portraitImage == null || conversation?.Lines == null) return;
            if (lineIndex < 0 || lineIndex >= conversation.Lines.Count) return;

            var sprite = conversation.Lines[lineIndex].SpeakerImage;
            portraitImage.sprite = sprite;

            if (hideWhenNull)
                portraitImage.gameObject.SetActive(sprite != null);
        }

        public override void Clear()
        {
            if (portraitImage == null) return;
            portraitImage.sprite = null;
            if (hideWhenNull) portraitImage.gameObject.SetActive(false);
        }
    }
}