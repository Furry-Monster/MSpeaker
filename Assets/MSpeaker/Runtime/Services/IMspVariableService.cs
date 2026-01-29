using System;
using System.Collections.Generic;

namespace MSpeaker.Runtime.Services
{
    public interface IMspVariableService
    {
        IReadOnlyDictionary<string, string> Variables { get; }

        string GetString(string key, string defaultValue = null);
        int GetInt(string key, int defaultValue = 0);
        float GetFloat(string key, float defaultValue = 0f);
        bool GetBool(string key, bool defaultValue = false);
        object GetValue(string key);

        void SetValue(string key, object value);
        bool HasVariable(string key);
        void RemoveVariable(string key);
        void Clear();

        event Action<string, object, object> OnVariableChanged;
    }
}