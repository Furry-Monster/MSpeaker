using MSpeaker.Runtime.Parser;
using UnityEngine;

namespace MSpeaker.Runtime.Plugins
{
    /// <summary>
    /// Engine 插件。
    /// </summary>
    public abstract class MspEnginePlugin : MonoBehaviour
    {
        public abstract void Display(MspConversation conversation, int lineIndex);
        public abstract void Clear();
    }
}