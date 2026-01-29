using System;
using System.Collections.Generic;

namespace MSpeaker.Runtime.Services
{
    public class MspVariableService : IMspVariableService
    {
        private readonly Dictionary<string, string> _variables = new();

        public IReadOnlyDictionary<string, string> Variables => _variables;

        public event Action<string, object, object> OnVariableChanged;

        public string GetString(string key, string defaultValue = null)
        {
            return string.IsNullOrEmpty(key)
                ? defaultValue
                : _variables.GetValueOrDefault(key, defaultValue);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            var str = GetString(key);
            return int.TryParse(str, out var result) ? result : defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            var str = GetString(key);
            return float.TryParse(str, out var result) ? result : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            var str = GetString(key);
            return bool.TryParse(str, out var result) ? result : defaultValue;
        }

        public object GetValue(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            key = key.TrimStart('$');

            if (!_variables.TryGetValue(key, out var value))
                return null;

            if (int.TryParse(value, out var intVal)) return intVal;
            if (float.TryParse(value, out var floatVal)) return floatVal;
            if (bool.TryParse(value, out var boolVal)) return boolVal;
            return value;
        }

        public void SetValue(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;

            var oldValue = _variables.TryGetValue(key, out var old) ? old : null;
            var newValue = value?.ToString();
            _variables[key] = newValue;

            if (oldValue != newValue)
                OnVariableChanged?.Invoke(key, oldValue, newValue);
        }

        public bool HasVariable(string key)
        {
            return !string.IsNullOrEmpty(key) && _variables.ContainsKey(key);
        }

        public void RemoveVariable(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (_variables.Remove(key, out var oldValue))
            {
                OnVariableChanged?.Invoke(key, oldValue, null);
            }
        }

        public void Clear()
        {
            _variables.Clear();
        }
    }
}