using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSpeaker.Runtime.Parser;
using MSpeaker.Runtime.Plugins;
using MSpeaker.Runtime.Views;
using UnityEngine;
using UnityEngine.Events;

namespace MSpeaker.Runtime
{
    public abstract class MspDialogueEngineBase : MonoBehaviour
    {
        protected MspEnginePlugin[] enginePlugins;

        public UnityEvent PersistentOnConversationStart = new UnityEvent();
        public UnityEvent PersistentOnConversationEnd = new UnityEvent();

        [HideInInspector] public UnityEvent OnConversationStart = new UnityEvent();
        [HideInInspector] public UnityEvent OnConversationEnd = new UnityEvent();

        public List<MspConversation> ParsedConversations { get; protected set; }
        protected MspConversation _currentConversation;

        protected int _lineIndex;
        protected bool _linePlaying;
        protected bool _isPaused;
        private Coroutine _displayCoroutine;

        public UnityEvent OnConversationPaused = new UnityEvent();
        public UnityEvent OnConversationResumed = new UnityEvent();

        [Header("Dialogue Views")]
        [SerializeField] protected MspDialogueViewBase dialogueView;

        public MspDialogueViewBase View => dialogueView;

        [Header("Function Invocations")]
        [SerializeField] private bool searchAllAssemblies;
        [SerializeField] private List<string> includedAssemblies = new List<string>();

        public void StartConversation(MspDialogueAsset dialogueAsset, int startIndex = 0)
        {
            if (dialogueAsset == null) throw new ArgumentNullException(nameof(dialogueAsset));

            ParsedConversations = MspDialogueParser.Parse(dialogueAsset);

            if (startIndex < 0 || startIndex >= ParsedConversations.Count)
                throw new ArgumentOutOfRangeException(nameof(startIndex),
                    "Expected value is between 0 and conversations count (inclusive)");

            enginePlugins = GetComponents<MspEnginePlugin>();
            SwitchConversation(ParsedConversations[startIndex]);
        }

        public void SwitchConversation(MspConversation conversation)
        {
            if (conversation == null) throw new ArgumentNullException(nameof(conversation));

            StopConversation();
            _currentConversation = conversation;

            OnConversationStart.AddListener(PersistentOnConversationStart.Invoke);
            OnConversationEnd.AddListener(PersistentOnConversationEnd.Invoke);

            OnConversationStart.Invoke();
            _displayCoroutine = StartCoroutine(DisplayDialogue());
        }

        public void StopConversation()
        {
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
                _displayCoroutine = null;
            }

            if (dialogueView != null)
                dialogueView.ClearView(enginePlugins);

            _linePlaying = false;
            _lineIndex = 0;
            _currentConversation = null;
            _isPaused = false;

            OnConversationEnd.Invoke();
            OnConversationStart.RemoveAllListeners();
            OnConversationEnd.RemoveAllListeners();
        }

        public void PauseConversation()
        {
            if (_currentConversation == null || _isPaused) return;
            _isPaused = true;
            if (dialogueView != null) dialogueView.Pause();
            OnConversationPaused.Invoke();
        }

        public void ResumeConversation()
        {
            if (_currentConversation == null || !_isPaused) return;
            _isPaused = false;
            if (dialogueView != null) dialogueView.Resume();
            OnConversationResumed.Invoke();
        }

        public bool IsConversationPaused() => _isPaused;

        public void JumpTo(string conversationName)
        {
            if (ParsedConversations == null || ParsedConversations.Count == 0)
                throw new InvalidOperationException("没有正在进行的对话，无法 JumpTo。");

            MspConversation conversation = ParsedConversations.Find(c => c.Name == conversationName);
            if (conversation == null)
                throw new ArgumentException($"未找到名为 \"{conversationName}\" 的会话。", nameof(conversationName));

            SwitchConversation(conversation);
        }

        protected virtual IEnumerator DisplayDialogue()
        {
            if (dialogueView == null)
            {
                MspDialogueLogger.LogError(-1, "DialogueView 未设置，无法显示对话。", this);
                yield break;
            }

            _linePlaying = true;

            if (_currentConversation?.Choices != null && _currentConversation.Choices.Count > 0)
            {
                var foundChoice = _currentConversation.Choices.FirstOrDefault(x => x.Value == _lineIndex);
                if (foundChoice.Key != null && _lineIndex == foundChoice.Value)
                    dialogueView.DisplayChoices(this, _currentConversation, ParsedConversations);
            }

            dialogueView.SetView(_currentConversation, _lineIndex);

            if (enginePlugins != null)
            {
                foreach (MspEnginePlugin plugin in enginePlugins)
                    plugin.Display(_currentConversation, _lineIndex);
            }

            InvokeFunctions(_currentConversation.Lines[_lineIndex].LineContent?.Invocations);

            yield return new WaitUntil(() => !dialogueView.IsStillDisplaying() || _isPaused);

            if (_isPaused)
                yield return new WaitUntil(() => !_isPaused);

            _linePlaying = false;
        }

        protected virtual void InvokeFunctions(Dictionary<int, string> functionInvocations)
        {
            if (functionInvocations == null || functionInvocations.Count == 0) return;

            MethodInfo[] methods = GetDialogueMethods().ToArray();
            if (methods.Length == 0) return;

            // 依次按索引执行，保证字符串插入顺序稳定
            int insertedOffset = 0;
            foreach (var kv in functionInvocations.OrderBy(x => x.Key))
            {
                string invocation = kv.Value?.Trim();
                if (string.IsNullOrEmpty(invocation)) continue;

                // 暂不支持行内传参形态 {{Foo(a,b)}}；保留括号部分会导致找不到方法
                int parenIndex = invocation.IndexOf('(');
                if (parenIndex >= 0)
                {
                    MspDialogueLogger.LogWarning(-1, $"暂不支持带参数的行内 invocation：{{{{{invocation}}}}}（仅支持 {{Foo}}）");
                    continue;
                }

                foreach (MethodInfo method in methods)
                {
                    if (!string.Equals(method.Name, invocation, StringComparison.Ordinal))
                        continue;

                    object[] args = null;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(this))
                        args = new object[] { this };
                    else if (parameters.Length == 0)
                        args = null;
                    else
                    {
                        MspDialogueLogger.LogWarning(-1,
                            $"Invocation \"{invocation}\" 找到了方法，但参数不匹配：仅支持 () 或 ({nameof(MspDialogueEngineBase)} engine)。");
                        continue;
                    }

                    if (method.ReturnType == typeof(string))
                    {
                        string replaced = (string)method.Invoke(null, args);
                        replaced ??= string.Empty;

                        var lineContent = _currentConversation.Lines[_lineIndex].LineContent;
                        int insertIndex = Mathf.Clamp(kv.Key + insertedOffset, 0, (lineContent.Text ?? string.Empty).Length);
                        lineContent.Text = (lineContent.Text ?? string.Empty).Insert(insertIndex, replaced);
                        insertedOffset += replaced.Length;

                        dialogueView.SetView(_currentConversation, _lineIndex);
                    }
                    else
                    {
                        method.Invoke(null, args);
                    }

                    break;
                }
            }
        }

        public void TryDisplayNextLine()
        {
            if (_linePlaying) return;
            if (_currentConversation == null) return;
            if (dialogueView == null) return;

            dialogueView.ClearView(enginePlugins);

            if (_lineIndex < _currentConversation.Lines.Count - 1)
            {
                _lineIndex++;
                StartCoroutine(DisplayDialogue());
            }
            else
            {
                StopConversation();
            }
        }

        protected IEnumerable<MethodInfo> GetDialogueMethods()
        {
            var assemblies = new List<Assembly>();
            Assembly[] allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            if (searchAllAssemblies)
            {
                assemblies.AddRange(allAssemblies);
            }
            else
            {
                foreach (Assembly asm in allAssemblies)
                {
                    string name = asm.GetName().Name;
                    if (name == "Assembly-CSharp" ||
                        includedAssemblies.Contains(name) ||
                        asm == Assembly.GetExecutingAssembly())
                        assemblies.Add(asm);
                }
            }

            var methods = new List<MethodInfo>();
            foreach (Assembly asm in assemblies)
            {
                IEnumerable<MethodInfo> found = asm.GetTypes()
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    .Where(m => m.GetCustomAttributes(typeof(MspDialogueFunctionAttribute), false).Length > 0);
                methods.AddRange(found);
            }

            return methods;
        }
    }
}

