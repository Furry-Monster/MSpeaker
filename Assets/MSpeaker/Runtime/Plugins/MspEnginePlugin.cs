using System.Collections.Generic;
using MSpeaker.Runtime.Interfaces;
using MSpeaker.Runtime.Parser;
using UnityEngine;

namespace MSpeaker.Runtime.Plugins
{
    public abstract class MspEnginePlugin : MonoBehaviour, IMspEnginePlugin
    {
        public virtual int Priority => (int)MspPluginPriority.Default;
        public virtual bool IsComplete => true;

        public virtual void OnConversationStart(IMspPluginContext context) { }
        public virtual void OnConversationEnd(IMspPluginContext context) { }
        public virtual void OnBeforeLineDisplay(IMspPluginContext context, MspLine line) { }
        public virtual MspPluginResult OnLineDisplay(IMspPluginContext context) => MspPluginResult.Continue;
        public virtual void OnLineComplete(IMspPluginContext context) { }
        public virtual void OnPause(IMspPluginContext context) { }
        public virtual void OnResume(IMspPluginContext context) { }
        public virtual void OnBeforeChoicesDisplay(IMspPluginContext context, IReadOnlyList<MspChoice> choices) { }
        public virtual void OnChoiceSelected(IMspPluginContext context, MspChoice choice) { }
        public virtual void OnClear() { }
    }
}
