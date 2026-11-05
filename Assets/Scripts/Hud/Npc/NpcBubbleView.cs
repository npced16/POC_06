using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace IronRing.Hud.Npc
{
    /// <summary>NPC 머리 옆에 뜨는 혼잣말 말풍선 하나. 글자 길이에 맞춰 크기가 변하며 클릭을 받지 않는다.</summary>
    public sealed class NpcBubbleView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private RectTransform _tail;
        [Tooltip("말풍선 꼬리를 모서리에서 안쪽으로 들이는 거리(캔버스 단위).")]
        [SerializeField] private float _tailInset = 14f;

        /// <summary>현재 글자에 맞춘 말풍선 크기(캔버스 단위)를 반환한다.</summary>
        public Vector2 Size => _rect.rect.size;

        /// <summary>text를 표시하고 말풍선을 켠 뒤 글자에 맞게 크기를 즉시 계산한다.</summary>
        public void Show(string text)
        {
            _text.text = text;
            gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
        }

        /// <summary>말풍선을 끈다.</summary>
        public void Hide() => gameObject.SetActive(false);

        /// <summary>position에 말풍선 아래 모서리를 놓는다. flipped가 true면 왼쪽으로 펼치고 꼬리도 오른쪽으로 옮긴다.</summary>
        public void Place(Vector2 position, bool flipped)
        {
            float side = flipped ? 1f : 0f;
            _rect.pivot = new Vector2(side, 0f);
            _rect.anchoredPosition = position;
            _tail.anchorMin = new Vector2(side, 0f);
            _tail.anchorMax = new Vector2(side, 0f);
            _tail.anchoredPosition = new Vector2(flipped ? -_tailInset : _tailInset, 0f);
        }
    }
}
