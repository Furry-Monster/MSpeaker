using System.Collections.Generic;
using UnityEngine;

namespace MSpeaker.Runtime.Parser
{
    public sealed class MspConversation
    {
        public string Name;
        public List<MspLine> Lines;

        /// <summary>
        /// Choice -> lineIndex。到达指定 lineIndex 时显示该 Choice。
        /// </summary>
        public Dictionary<MspChoice, int> Choices;

        /// <summary>
        /// 标签 -> lineIndex。用于 Goto 跳转。
        /// </summary>
        public Dictionary<string, int> Labels;

        /// <summary>
        /// 条件分支信息。lineIndex -> 条件分支数据。
        /// </summary>
        public Dictionary<int, MspConditionalBlock> ConditionalBlocks;
    }

    public sealed class MspLine
    {
        public string Speaker;
        public Sprite SpeakerImage;
        public MspLineContent LineContent;

        /// <summary>
        /// 行类型：普通行、标签、条件开始、条件结束等
        /// </summary>
        public MspLineType LineType;

        /// <summary>
        /// 标签名称（当 LineType 为 Label 时）
        /// </summary>
        public string LabelName;

        /// <summary>
        /// 循环信息（当 LineType 为 LoopStart 时）
        /// </summary>
        public MspLoopInfo LoopInfo;
    }

    public enum MspLineType
    {
        Normal, // 普通对话行
        Label, // 标签行 {{Label(name)}}
        LoopStart, // 循环开始 {{Loop(count)}}
        LoopEnd, // 循环结束
        IfStart, // 条件开始 {{If(condition)}} 或 {{IfVar(name,value)}}
        Else, // Else 分支 {{Else}}
        EndIf, // 条件结束 {{EndIf}}
        Goto // 跳转 {{Goto(label)}}
    }

    public sealed class MspLineContent
    {
        public string Text;
        public Dictionary<int, MspFunctionInvocation> Invocations; // 改为支持参数的调用
        public Dictionary<string, string> Metadata;
    }

    /// <summary>
    /// 函数调用信息，支持参数
    /// </summary>
    public sealed class MspFunctionInvocation
    {
        public string FunctionName;
        public List<MspFunctionArgument> Arguments;
    }

    /// <summary>
    /// 函数参数，支持类型转换
    /// </summary>
    public sealed class MspFunctionArgument
    {
        public string RawValue;
        public MspArgumentType Type;
        public object ConvertedValue;
    }

    public enum MspArgumentType
    {
        String,
        Integer,
        Float,
        Boolean,
        Variable
    }

    /// <summary>
    /// 条件分支块信息
    /// </summary>
    public sealed class MspConditionalBlock
    {
        public MspConditionType ConditionType;
        public string ConditionExpression; // 条件表达式
        public int IfStartLineIndex; // If 开始的行索引
        public int ElseLineIndex; // Else 行索引（-1 表示没有 Else）
        public int EndIfLineIndex; // EndIf 行索引
    }

    public enum MspConditionType
    {
        If, // {{If(condition)}}
        IfVar // {{IfVar(name,value)}}
    }

    /// <summary>
    /// 循环信息
    /// </summary>
    public sealed class MspLoopInfo
    {
        public int LoopCount; // 循环次数
        public int LoopStartLineIndex; // 循环开始行索引
        public int LoopEndLineIndex; // 循环结束行索引
    }

    public sealed class MspChoice
    {
        public string ChoiceName;
        public string LeadingConversationName;
        public Dictionary<string, string> Metadata;

        /// <summary>
        /// 条件表达式，如果设置则只有满足条件时才显示此选项
        /// </summary>
        public string ConditionExpression;
    }
}