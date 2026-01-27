using System.Collections.Generic;

namespace MSpeaker.Runtime.Utils
{
    /// <summary>
    /// 对话系统全局变量：在解析阶段会把 $Key 替换为 Value。
    /// </summary>
    public static class MspDialogueGlobals
    {
        public static readonly Dictionary<string, string> GlobalVariables = new();
    }
}