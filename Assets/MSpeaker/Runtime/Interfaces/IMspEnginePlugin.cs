using System.Collections.Generic;
using MSpeaker.Runtime.Parser;

namespace MSpeaker.Runtime.Interfaces
{
    public enum MspPluginPriority
    {
        Early = 0,
        Default = 100,
        Late = 200
    }

    public interface IMspPluginContext
    {
        MspConversation CurrentConversation { get; }
        MspLine CurrentLine { get; }
        int CurrentLineIndex { get; }
        bool IsPaused { get; }
        IMspDialogueEngine Engine { get; }

        string GetMetadata(string key, string defaultValue = null);
        bool HasMetadata(string key);
    }

    public readonly struct MspPluginResult
    {
        public static readonly MspPluginResult Continue = new(false);
        public static readonly MspPluginResult WaitForCompletion = new(true);

        public bool ShouldWait { get; }

        private MspPluginResult(bool shouldWait) => ShouldWait = shouldWait;
    }

    public interface IMspEnginePlugin
    {
        int Priority { get; }
        bool IsComplete { get; }

        void OnConversationStart(IMspPluginContext context);
        void OnConversationEnd(IMspPluginContext context);
        void OnBeforeLineDisplay(IMspPluginContext context, MspLine line);
        MspPluginResult OnLineDisplay(IMspPluginContext context);
        void OnLineComplete(IMspPluginContext context);
        void OnPause(IMspPluginContext context);
        void OnResume(IMspPluginContext context);
        void OnBeforeChoicesDisplay(IMspPluginContext context, IReadOnlyList<MspChoice> choices);
        void OnChoiceSelected(IMspPluginContext context, MspChoice choice);
        void OnClear();
    }
}