using MSpeaker.Dialogue.Parser;
using UnityEngine;

namespace MSpeaker.Dialogue.Plugins
{
    /// <summary>
    /// Engine 插件：用于在显示每一行时同步更新额外 UI（例如头像、音效等）。
    /// </summary>
    public abstract class MspEnginePlugin : MonoBehaviour
    {
        public abstract void Display(MspConversation conversation, int lineIndex);
        public abstract void Clear();
    }
}

