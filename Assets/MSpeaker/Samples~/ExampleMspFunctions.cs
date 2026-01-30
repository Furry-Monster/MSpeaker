using System;
using System.Collections.Generic;
using MSpeaker.Runtime.Utils;

namespace Samples
{
    /// <summary>
    /// 示例函数：用于演示各种函数调用功能，包括无参数和带参数的函数。
    /// </summary>
    public static class ExampleMspFunctions
    {
        private static int _loopIndex = 0;

        [MspDialogueFunction]
        public static string GetPlayerName()
        {
            return MspDialogueGlobals.GlobalVariables.GetValueOrDefault("playerName", "Unknown");
        }

        [MspDialogueFunction]
        public static string GetTime()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        [MspDialogueFunction]
        public static string FormatMessage(string message, int number)
        {
            return $"{message} - {number}";
        }

        [MspDialogueFunction]
        public static void ShowMessage(string message)
        {
            // 示例函数：可以在这里实现实际的消息显示逻辑
        }

        [MspDialogueFunction]
        public static void AddScore(int points)
        {
            var current =
                int.TryParse(MspDialogueGlobals.GlobalVariables.GetValueOrDefault(
                        "score", "0"),
                    out var score)
                    ? score
                    : 0;
            MspDialogueGlobals.GlobalVariables["score"] =
                (current + points).ToString();
        }

        [MspDialogueFunction]
        public static void SetFlag(bool value)
        {
            // 示例函数：可以在这里实现实际的标志设置逻辑
        }

        [MspDialogueFunction]
        public static void CreateItem(string itemName, int value, bool isRare)
        {
            // 示例函数：可以在这里实现实际的道具创建逻辑
        }

        [MspDialogueFunction]
        public static void UseVariable(string variableName)
        {
            // 示例函数：可以在这里实现实际的变量使用逻辑
        }

        [MspDialogueFunction]
        public static void ProcessData(string dataType, int count, bool process)
        {
            if (process)
            {
                // 示例函数：可以在这里实现实际的数据处理逻辑
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
        }
    }
}