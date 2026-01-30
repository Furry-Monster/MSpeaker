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
            // 好感度
            MspDialogueGlobals.GlobalVariables["shikiAffection"] = "30";
            // 天数
            MspDialogueGlobals.GlobalVariables["dayCount"] = "1";
            // 是否获得魔法书
            MspDialogueGlobals.GlobalVariables["hasMagicBook"] = "false";
            // 是否已经遇见 Shiki
            MspDialogueGlobals.GlobalVariables["metShiki"] = "false";
            // 玩家姓名
            MspDialogueGlobals.GlobalVariables["playerName"] = "Player";
            // 是否加入社团
            MspDialogueGlobals.GlobalVariables["joinedClub"] = "false";

            engine.StartConversation(dialogue);
        }
    }
}