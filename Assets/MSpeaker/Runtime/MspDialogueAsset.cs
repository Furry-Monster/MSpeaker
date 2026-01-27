using UnityEngine;

namespace MSpeaker.Runtime
{
    /// <summary>
    /// 存储 .msp 文本内容的 ScriptableObject（由 Importer 自动生成）。
    /// </summary>
    public sealed class MspDialogueAsset : ScriptableObject
    {
        [field: SerializeField, TextArea(5, 50)]
        public string Content { get; set; }
    }
}

