using System.Collections.Generic;
using MSpeaker.Runtime.Interfaces;
using MSpeaker.Runtime.Parser;

namespace MSpeaker.Runtime.Services
{
    public interface IMspFunctionInvoker
    {
        void Invoke(Dictionary<int, MspFunctionInvocation> invocations, MspLineContent lineContent,
            IMspDialogueEngine engine);

        void ClearCache();
    }
}