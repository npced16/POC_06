using UnityEngine;

using IronRing.Hud.Ui;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 041. 건물 위 생산 상태 아이콘 조각(BuildingStatus.prefab, Story012Build가 만든다). BuildingView가 Show(iconId)로 바꾼다.
    /// 아이콘은 IconCatalog(codex 소유, Assets/Prefabs/UI/IconCatalog.asset)에서 읽고, 카탈로그·스프라이트가 없으면 아무것도 그리지 않는다.
    /// </summary>
    public sealed class BuildingStatusIcon : MonoBehaviour
    {
        [SerializeField] private IconCatalog _icons;
        [SerializeField] private SpriteRenderer _renderer;
        [Tooltip("이 id의 스프라이트가 없을 때 대신 쓰는 id(예: status_no_water → status_no_input).")]
        [SerializeField] private string _fallbackId = "status_no_input";

        /// <summary>현재 보이는 아이콘 id(숨김이면 null).</summary>
        public string Shown { get; private set; }

        /// <summary>iconId 아이콘을 보인다. null이거나 스프라이트가 없으면 숨긴다.</summary>
        public void Show(string iconId)
        {
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>(true);
            Sprite sprite = null;
            if (_icons != null && iconId != null) sprite = _icons.Get(iconId) ?? _icons.Get(_fallbackId);
            Shown = sprite != null ? iconId : null;
            if (_renderer != null) { _renderer.sprite = sprite; _renderer.enabled = sprite != null; }
        }
    }
}
