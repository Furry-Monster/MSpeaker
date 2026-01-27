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

            bool pressed = WasPressedThisFrame();

            if (!pressed) return;

            if (engine.View != null && engine.View.IsStillDisplaying())
                engine.View.SkipViewEffect();
            else
                engine.TryDisplayNextLine();
        }

        private bool WasPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // 新输入系统（Project Settings -> Active Input Handling = Input System）
            bool pressed = false;

            // 键盘
            if (Keyboard.current != null)
            {
                // 目前仅对常用键做映射（足够解决你的报错并快速可用）
                switch (advanceKey)
                {
                    case KeyCode.Space:
                        pressed = Keyboard.current.spaceKey.wasPressedThisFrame;
                        break;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        pressed = (Keyboard.current.enterKey?.wasPressedThisFrame ?? false) ||
                                  (Keyboard.current.numpadEnterKey?.wasPressedThisFrame ?? false);
                        break;
                    case KeyCode.Escape:
                        pressed = Keyboard.current.escapeKey.wasPressedThisFrame;
                        break;
                    default:
                        // 其它 KeyCode 在新输入系统里没有一一对应；需要的话我可以加一个更完整的映射表
                        pressed = Keyboard.current.spaceKey.wasPressedThisFrame;
                        break;
                }
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

