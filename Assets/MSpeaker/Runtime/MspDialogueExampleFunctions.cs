namespace MSpeaker.Runtime
{
    /// <summary>
    /// 示例：用于演示 {{PlayerName}} 这类 invocation。
    /// 你可以删除该文件，或把函数移动到自己的项目代码里。
    /// </summary>
    public static class MspDialogueExampleFunctions
    {
        [MspDialogueFunction]
        public static string PlayerName()
        {
            return "玩家";
        }
    }
}

