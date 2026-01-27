using MSpeaker.Runtime.Utils;
using UnityEngine;

namespace MSpeaker.Runtime
{
    /// <summary>
    /// 示例函数：用于演示各种函数调用功能，包括无参数和带参数的函数。
    /// </summary>
    public static class ExampleMspFunctions
    {
        private static int _loopIndex = 0;
        private static int _score = 0;

        [MspDialogueFunction]
        public static string PlayerName()
        {
            return "Player";
        }

        [MspDialogueFunction]
        public static string GetPlayerName()
        {
            return "Player";
        }

        [MspDialogueFunction]
        public static string GetTime()
        {
            return System.DateTime.Now.ToString("HH:mm:ss");
        }

        [MspDialogueFunction]
        public static string FormatMessage(string message, int number)
        {
            return $"{message} - {number}";
        }

        [MspDialogueFunction]
        public static void ShowMessage(string message)
        {
            Debug.Log($"[MSpeaker] Message: {message}");
        }

        [MspDialogueFunction]
        public static void AddScore(int points)
        {
            _score += points;
            Debug.Log($"[MSpeaker] Score increased by {points}. Current score: {_score}");
        }

        [MspDialogueFunction]
        public static void SetFlag(bool value)
        {
            Debug.Log($"[MSpeaker] Flag set to: {value}");
        }

        [MspDialogueFunction]
        public static void CreateItem(string itemName, int value, bool isRare)
        {
            Debug.Log($"[MSpeaker] Created item: {itemName}, Value: {value}, Rare: {isRare}");
        }

        [MspDialogueFunction]
        public static void UseVariable(string variableName)
        {
            Debug.Log($"[MSpeaker] Using variable: {variableName}");
        }

        [MspDialogueFunction]
        public static void ProcessData(string dataType, int count, bool process)
        {
            if (process)
            {
                Debug.Log($"[MSpeaker] Processing {count} items of type {dataType}");
            }
        }

        [MspDialogueFunction]
        public static int GetLoopIndex()
        {
            return _loopIndex;
        }

        [MspDialogueFunction]
        public static void SetLoopIndex(int index)
        {
            _loopIndex = index;
        }

        [MspDialogueFunction]
        public static void SetVariable(string name, string value)
        {
            MspDialogueGlobals.GlobalVariables[name] = value;
            Debug.Log($"[MSpeaker] Set variable {name} = {value}");
        }

        [MspDialogueFunction]
        public static MspDialogueEngineBase GetEngine(MspDialogueEngineBase engine)
        {
            return engine;
        }
    }
}