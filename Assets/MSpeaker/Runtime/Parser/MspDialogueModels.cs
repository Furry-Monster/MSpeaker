using System.Collections.Generic;

namespace MSpeaker.Runtime.Parser
{
    public sealed class MspConversation
    {
        public string Name;
        public List<MspLine> Lines;
        public Dictionary<MspChoice, int> Choices;
        public Dictionary<string, int> Labels;
        public Dictionary<int, MspConditionalBlock> ConditionalBlocks;
    }

    public sealed class MspLine
    {
        public string Speaker;
        public string SpeakerImagePath;
        public MspLineContent LineContent;
        public MspLineType LineType;
        public string LabelName;
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
        public int LoopCount; // 循环次数（运行时解析）
        public string LoopCountExpression; // 循环次数表达式（可能是数字或变量名，如 "5" 或 "$loopCount"）
        public int LoopStartLineIndex; // 循环开始行索引
        public int LoopEndLineIndex; // 循环结束行索引
    }

    public sealed class MspChoice
    {
        public string ChoiceName;
        public string LeadingConversationName;
        public Dictionary<string, string> Metadata;
        public string ConditionExpression;
    }

    public sealed class MspConditionInfo
    {
        public bool IsValid;
        public string LeftOperand;
        public string Operator;
        public string RightOperand;
        public bool IsVariableComparison;
    }
}