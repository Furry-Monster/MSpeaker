using System.Collections.Generic;
using MSpeaker.Runtime.Parser;
using UnityEngine.Events;

namespace MSpeaker.Runtime.Interfaces
{
    public interface IMspDialogueEngine
    {
        List<MspConversation> ParsedConversations { get; }
        IMspDialogueView View { get; }

        UnityEvent OnConversationStart { get; }
        UnityEvent OnConversationEnd { get; }
        UnityEvent OnConversationPaused { get; }
        UnityEvent OnConversationResumed { get; }

        void StartConversation(MspDialogueAsset dialogueAsset, int startIndex = 0);
        void SwitchConversation(MspConversation conversation);
        void StopConversation();
        void PauseConversation();
        void ResumeConversation();
        void JumpTo(string conversationName);
        void TryDisplayNextLine();
        bool IsConversationPaused();
    }
}
