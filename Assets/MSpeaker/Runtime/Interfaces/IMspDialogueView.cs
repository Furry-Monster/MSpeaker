using System.Collections.Generic;
using MSpeaker.Runtime.Parser;
using UnityEngine.Events;

namespace MSpeaker.Runtime.Interfaces
{
    public interface IMspDialogueView
    {
        UnityEvent OnSetView { get; }
        UnityEvent OnLineComplete { get; }

        bool IsStillDisplaying();
        void SetView(MspConversation conversation, int lineIndex);
        void ClearView();
        void SkipViewEffect();
        void Pause();
        void Resume();
        bool IsPaused();
        void DisplayChoices(IMspDialogueEngine engine, MspConversation conversation, List<MspConversation> parsedConversations);
    }
}
