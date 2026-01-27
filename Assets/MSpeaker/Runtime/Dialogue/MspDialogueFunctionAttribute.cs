using System;

namespace MSpeaker.Dialogue
{
    /// <summary>
    /// 标记静态方法，使其可以通过 {{FuncName}} 在 .msp 中被调用。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MspDialogueFunctionAttribute : Attribute
    {
    }
}

