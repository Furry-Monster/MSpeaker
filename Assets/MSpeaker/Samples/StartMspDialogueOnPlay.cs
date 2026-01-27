using MSpeaker.Runtime;
using UnityEngine;

namespace Samples
{
    public sealed class StartMspDialogueOnPlay : MonoBehaviour
    {
        [SerializeField] private MspSimpleDialogueEngine engine;
        [SerializeField] private MspDialogueAsset dialogue;

        private void Start()
        {
            engine.StartConversation(dialogue);
        }
    }
}