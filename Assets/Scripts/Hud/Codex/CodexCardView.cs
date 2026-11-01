using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace IronRing.Hud.Codex
{
    /// <summary>
    /// 스토리 022. 도감 카드 부품(Parts/CodexCard.prefab): 이름·종류·칸 수·타일 배열 영역(GridLayoutGroup에 TileIcon 부품)·번영 보너스·NPC·"완성" 표시.
    /// 잠긴 카드는 "???"와 어두운 실루엣만 보인다. 표시만 하고 상태는 갖지 않는다.
    /// </summary>
    public sealed class CodexCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _meta;
        [SerializeField] private TMP_Text _bonus;
        [SerializeField] private TMP_Text _npc;
        [SerializeField] private GameObject _badge;
        [SerializeField] private GameObject _silhouette;
        [SerializeField] private RectTransform _diagram;
        [SerializeField] private GridLayoutGroup _diagramGrid;
        [Tooltip("배열 칸이 한 줄에 놓이는 기본 수(패턴이 아닐 때).")]
        [SerializeField] private int _flowColumns = 9;

        /// <summary>타일 아이콘을 넣을 배열 영역.</summary>
        public RectTransform Diagram => _diagram;

        /// <summary>잠긴 카드: lockedText와 실루엣만.</summary>
        public void ShowLocked(string lockedText)
        {
            _name.text = lockedText;
            SetUnlockedParts(false);
            if (_badge != null) _badge.SetActive(false);
            ClearDiagram();
        }

        /// <summary>해금 카드: 이름·종류 줄·보너스·NPC를 쓰고 완성 표시를 켜거나 끈다. 배열은 Diagram에 따로 채운다.</summary>
        public void ShowUnlocked(string name, string meta, string bonus, string npc, bool complete)
        {
            _name.text = name;
            _meta.text = meta;
            _bonus.text = bonus;
            _npc.text = npc;
            SetUnlockedParts(true);
            if (_badge != null) _badge.SetActive(complete);
            ClearDiagram();
        }

        /// <summary>배열 격자의 열 수(패턴이면 행 길이, 아니면 0으로 기본값).</summary>
        public void SetColumns(int columns)
        {
            if (_diagramGrid == null) return;
            _diagramGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _diagramGrid.constraintCount = Mathf.Max(1, columns > 0 ? columns : _flowColumns);
        }

        private void SetUnlockedParts(bool unlocked)
        {
            _meta.gameObject.SetActive(unlocked);
            _bonus.gameObject.SetActive(unlocked);
            _npc.gameObject.SetActive(unlocked);
            _diagram.gameObject.SetActive(unlocked);
            if (_silhouette != null) _silhouette.SetActive(!unlocked);
        }

        private void ClearDiagram()
        {
            for (int i = _diagram.childCount - 1; i >= 0; i--)
            {
                GameObject child = _diagram.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }
    }
}
