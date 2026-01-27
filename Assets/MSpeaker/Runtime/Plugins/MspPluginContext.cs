using MSpeaker.Runtime.Interfaces;
using MSpeaker.Runtime.Parser;

namespace MSpeaker.Runtime.Plugins
{
    public class MspPluginContext : IMspPluginContext
    {
        public MspConversation CurrentConversation { get; private set; }
        public MspLine CurrentLine { get; private set; }
        public int CurrentLineIndex { get; private set; }
        public bool IsPaused { get; private set; }
        public IMspDialogueEngine Engine { get; private set; }

        public void Update(
            IMspDialogueEngine engine,
            MspConversation conversation,
            int lineIndex,
            bool isPaused)
        {
            Engine = engine;
            CurrentConversation = conversation;
            CurrentLineIndex = lineIndex;
            CurrentLine = conversation?.Lines != null && lineIndex >= 0 && lineIndex < conversation.Lines.Count
                ? conversation.Lines[lineIndex]
                : null;
            IsPaused = isPaused;
        }

        public string GetMetadata(string key, string defaultValue = null)
        {
            if (string.IsNullOrEmpty(key) || CurrentLine?.LineContent?.Metadata == null)
                return defaultValue;

            return CurrentLine.LineContent.Metadata.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public bool HasMetadata(string key)
        {
            if (string.IsNullOrEmpty(key) || CurrentLine?.LineContent?.Metadata == null)
                return false;

            return CurrentLine.LineContent.Metadata.ContainsKey(key);
        }
    }
}
