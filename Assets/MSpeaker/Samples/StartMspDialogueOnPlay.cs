using MSpeaker.Runtime;
using UnityEngine;

public sealed class StartMspDialogueOnPlay : MonoBehaviour
{
    [SerializeField] private MspSimpleDialogueEngine engine;
    [SerializeField] private MspDialogueAsset dialogue;

    private void Start()
    {
        engine.StartConversation(dialogue);
    }
}
