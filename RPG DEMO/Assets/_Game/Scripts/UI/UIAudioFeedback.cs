using Game.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>공통 UI 버튼에 마우스 호버음과 클릭음을 붙입니다.</summary>
    public sealed class UIAudioFeedback : MonoBehaviour, IPointerEnterHandler
    {
        private Button button;

        public void Initialize(Button targetButton)
        {
            if (button != null)
                button.onClick.RemoveListener(PlayClick);

            button = targetButton;
            if (button != null)
                button.onClick.AddListener(PlayClick);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable())
                AudioManager.Instance?.PlayUiHover();
        }

        private void PlayClick()
        {
            AudioManager.Instance?.PlayUiClick();
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(PlayClick);
        }
    }
}
