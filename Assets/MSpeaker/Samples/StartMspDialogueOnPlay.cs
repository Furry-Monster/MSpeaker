using MSpeaker.Runtime;
using MSpeaker.Runtime.Utils;
using UnityEngine;

namespace Samples
{
    public sealed class StartMspDialogueOnPlay : MonoBehaviour
    {
        [SerializeField] private MspSimpleDialogueEngine engine;
        [SerializeField] private MspDialogueAsset dialogue;

        private void Start()
        {
            MspDialogueGlobals.GlobalVariables["score"] = "50";
            MspDialogueGlobals.GlobalVariables["level"] = "1";
            MspDialogueGlobals.GlobalVariables["loopCount"] = "5";
            MspDialogueGlobals.GlobalVariables["playerName"] = "Player";
            MspDialogueGlobals.GlobalVariables["count"] = "3";

            engine.StartConversation(dialogue);
        }
    }
}