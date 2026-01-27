using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSpeaker.Runtime.Interfaces;
using MSpeaker.Runtime.Parser;
using MSpeaker.Runtime.Utils;
using UnityEngine;

namespace MSpeaker.Runtime.Services
{
    public class MspFunctionInvoker : IMspFunctionInvoker
    {
        private readonly IMspVariableService _variableService;
        private readonly bool _searchAllAssemblies;
        private readonly List<string> _includedAssemblies;

        private MethodInfo[] _cachedMethods;

        public MspFunctionInvoker(
            IMspVariableService variableService,
            bool searchAllAssemblies = false,
            List<string> includedAssemblies = null)
        {
            _variableService = variableService;
            _searchAllAssemblies = searchAllAssemblies;
            _includedAssemblies = includedAssemblies ?? new List<string>();
        }

        public void Invoke(Dictionary<int, MspFunctionInvocation> invocations, MspLineContent lineContent, IMspDialogueEngine engine)
        {
            if (invocations == null || invocations.Count == 0) return;

            var methods = GetCachedMethods();
            if (methods.Length == 0) return;

            var insertedOffset = 0;
            foreach (var kv in invocations.OrderBy(x => x.Key))
            {
                var invocation = kv.Value;
                if (invocation == null || string.IsNullOrEmpty(invocation.FunctionName)) continue;

                foreach (var method in methods)
                {
                    if (!string.Equals(method.Name, invocation.FunctionName, StringComparison.Ordinal))
                        continue;

                    var parameters = method.GetParameters();
                    var args = BuildMethodArguments(parameters, invocation.Arguments, engine);

                    if (args == null)
                    {
                        MspDialogueLogger.LogWarning(-1, $"Invocation \"{invocation.FunctionName}\" 参数不匹配。");
                        continue;
                    }

                    if (method.ReturnType == typeof(string))
                    {
                        var replaced = (string)method.Invoke(null, args) ?? string.Empty;
                        var insertIndex = Mathf.Clamp(kv.Key + insertedOffset, 0,
                            (lineContent.Text ?? string.Empty).Length);
                        lineContent.Text = (lineContent.Text ?? string.Empty).Insert(insertIndex, replaced);
                        insertedOffset += replaced.Length;
                    }
                    else
                    {
                        method.Invoke(null, args);
                    }

                    break;
                }
            }
        }

        public void ClearCache()
        {
            _cachedMethods = null;
        }

        private MethodInfo[] GetCachedMethods()
        {
            if (_cachedMethods != null) return _cachedMethods;

            var assemblies = new List<Assembly>();
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            if (_searchAllAssemblies)
            {
                assemblies.AddRange(allAssemblies);
            }
            else
            {
                foreach (var asm in allAssemblies)
                {
                    var asmName = asm.GetName().Name;
                    if (asmName == "Assembly-CSharp" ||
                        _includedAssemblies.Contains(asmName) ||
                        asm == Assembly.GetExecutingAssembly())
                        assemblies.Add(asm);
                }
            }

            var methods = new List<MethodInfo>();
            foreach (var asm in assemblies)
            {
                var found = asm.GetTypes()
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    .Where(m => m.GetCustomAttributes(typeof(MspDialogueFunctionAttribute), false).Length > 0);
                methods.AddRange(found);
            }

            _cachedMethods = methods.ToArray();
            return _cachedMethods;
        }

        private object[] BuildMethodArguments(ParameterInfo[] parameters, List<MspFunctionArgument> invocationArgs, IMspDialogueEngine engine)
        {
            if (parameters.Length == 0)
            {
                if (invocationArgs == null || invocationArgs.Count == 0)
                    return Array.Empty<object>();
                return null;
            }

            // 支持单参数引擎注入
            if (parameters.Length == 1 && typeof(IMspDialogueEngine).IsAssignableFrom(parameters[0].ParameterType))
                return new object[] { engine };

            if (invocationArgs == null || invocationArgs.Count != parameters.Length)
                return null;

            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var arg = invocationArgs[i];

                if (arg.Type == MspArgumentType.Variable)
                {
                    var varName = arg.ConvertedValue?.ToString();
                    var varValue = _variableService.GetValue("$" + varName);
                    if (varValue == null)
                    {
                        MspDialogueLogger.LogWarning(-1, $"变量 ${varName} 不存在。");
                        return null;
                    }
                    args[i] = ConvertValue(varValue, paramType);
                }
                else
                {
                    args[i] = ConvertValue(arg.ConvertedValue, paramType);
                }
            }

            return args;
        }

        private object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsAssignableFrom(value.GetType())) return value;
            if (targetType == typeof(string)) return value.ToString();

            if (targetType == typeof(int))
            {
                if (value is int i) return i;
                return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
            }

            if (targetType == typeof(float))
            {
                if (value is float f) return f;
                return float.TryParse(value.ToString(), out var parsed) ? parsed : 0f;
            }

            if (targetType == typeof(bool))
            {
                if (value is bool b) return b;
                return bool.TryParse(value.ToString(), out var parsed) && parsed;
            }

            return Convert.ChangeType(value, targetType);
        }
    }
}
