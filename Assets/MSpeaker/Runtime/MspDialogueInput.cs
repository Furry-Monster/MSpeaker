using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MSpeaker.Runtime
{
    /// <summary>
    /// 一个很轻量的输入脚本：点击/空格推进对话；若当前 View 在播放效果则先跳过效果。
    /// </summary>
    public sealed class MspDialogueInput : MonoBehaviour
    {
        [SerializeField] private MspDialogueEngineBase engine;
        [SerializeField] private KeyCode advanceKey = KeyCode.Space;
        [SerializeField] private bool mouseLeftClick = true;

        private void Update()
        {
            if (engine == null) return;

            var pressed = WasPressedThisFrame();

            if (!pressed) return;

            if (engine.View != null && engine.View.IsStillDisplaying())
                engine.View.SkipViewEffect();
            else
                engine.TryDisplayNextLine();
        }

        private bool WasPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var pressed = false;

            // 键盘
            if (Keyboard.current != null)
            {
                pressed = advanceKey switch
                {
                    KeyCode.Space => Keyboard.current.spaceKey.wasPressedThisFrame,
                    KeyCode.Return or KeyCode.KeypadEnter =>
                        (Keyboard.current.enterKey?.wasPressedThisFrame ?? false) ||
                        (Keyboard.current.numpadEnterKey?.wasPressedThisFrame ?? false),
                    KeyCode.Escape => Keyboard.current.escapeKey.wasPressedThisFrame,
                    _ => Keyboard.current.spaceKey.wasPressedThisFrame
                };
            }

            // 鼠标
            if (!pressed && mouseLeftClick && Mouse.current != null)
                pressed = Mouse.current.leftButton.wasPressedThisFrame;

            return pressed;
#else
            // 旧输入系统（Legacy Input Manager）
            bool pressed = Input.GetKeyDown(advanceKey);
            if (!pressed && mouseLeftClick)
                pressed = Input.GetMouseButtonDown(0);
            return pressed;
#endif
        }

        public void Advance()
        {
            if (engine == null) return;
            if (engine.View != null && engine.View.IsStillDisplaying())
                engine.View.SkipViewEffect();
            else
                engine.TryDisplayNextLine();
        }
    }
}