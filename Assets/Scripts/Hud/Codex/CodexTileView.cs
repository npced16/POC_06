using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace IronRing.Hud.Codex
{
    /// <summary>스토리 022. 도감 배열의 타일 한 칸 부품(Parts/TileIcon.prefab): 아이콘 Image와, 아이콘이 없을 때의 글자(또는 수).</summary>
    public sealed class CodexTileView : MonoBehaviour
    {
        [SerializeField] private Image _frame;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _count;
        [Tooltip("빈 칸(패턴의 '.')일 때 틀의 투명도.")]
        [SerializeField] private float _emptyAlpha = 0.35f;

        /// <summary>sprite가 있으면 아이콘, 없으면 text를 보인다. empty면 흐린 빈 칸.</summary>
        public void Show(Sprite sprite, string text, bool empty)
        {
            if (_icon != null) { _icon.sprite = sprite; _icon.enabled = !empty && sprite != null; }
            if (_count != null) _count.text = empty || sprite != null ? string.Empty : text ?? string.Empty;
            if (_frame != null) { Color c = _frame.color; c.a = empty ? _emptyAlpha : 1f; _frame.color = c; }
        }
    }
}
