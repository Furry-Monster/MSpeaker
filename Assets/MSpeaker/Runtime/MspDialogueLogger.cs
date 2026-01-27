using UnityEngine;

namespace MSpeaker.Runtime
{
    internal static class MspDialogueLogger
    {
        public static void LogError(int lineNumber, string message, Object context = null)
        {
            string prefix = lineNumber > 0 ? $"[MSpeaker.Dialogue] (Line {lineNumber}) " : "[MSpeaker.Dialogue] ";
            if (context != null) Debug.LogError(prefix + message, context);
            else Debug.LogError(prefix + message);
        }

        public static void LogWarning(int lineNumber, string message, Object context = null)
        {
            string prefix = lineNumber > 0 ? $"[MSpeaker.Dialogue] (Line {lineNumber}) " : "[MSpeaker.Dialogue] ";
            if (context != null) Debug.LogWarning(prefix + message, context);
            else Debug.LogWarning(prefix + message);
        }
    }
}

