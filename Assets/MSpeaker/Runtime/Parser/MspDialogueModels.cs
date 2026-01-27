using System.Collections.Generic;
using UnityEngine;

namespace MSpeaker.Runtime.Parser
{
    public sealed class MspConversation
    {
        public string Name;
        public List<MspLine> Lines;

        /// <summary>
        /// Choice -> lineIndex。到达指定 lineIndex 时显示该 Choice。
        /// </summary>
        public Dictionary<MspChoice, int> Choices;
    }

    public sealed class MspLine
    {
        public string Speaker;
        public Sprite SpeakerImage;
        public MspLineContent LineContent;
    }

    public sealed class MspLineContent
    {
        public string Text;
        public Dictionary<int, string> Invocations;
        public Dictionary<string, string> Metadata;
    }

    public sealed class MspChoice
    {
        public string ChoiceName;
        public string LeadingConversationName;
        public Dictionary<string, string> Metadata;
    }
}

