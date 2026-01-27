using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MSpeaker.Runtime.Views
{
    [RequireComponent(typeof(Button))]
    public sealed class MspChoiceButton : MonoBehaviour
    {
        public UnityEvent OnChoiceClick = new();

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (_button == null) _button = GetComponent<Button>();
            _button.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick()
        {
            OnChoiceClick.Invoke();
        }
    }
}