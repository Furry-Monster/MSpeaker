using MSpeaker.Runtime.Utils;

namespace MSpeaker.Runtime
{
    /// <summary>
    /// 示例：用于演示 {{PlayerName}} 这类 invocation。
    /// </summary>
    public static class ExampleMspFunctions
    {
        [MspDialogueFunction]
        public static string PlayerName()
        {
            return "Player";
        }
    }
}