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
        [SerializeField] private TMP_Text _yield;
        [SerializeField] private Image _resourceIcon;
        [SerializeField] private Image _incomeIcon;
        [SerializeField] private TMP_Text _resourceText;
        [SerializeField] private TMP_Text _incomeText;
        [SerializeField] private TMP_Text _npc;
        [Tooltip("스토리 030: '가르쳐 주는 이: 직업' 한 줄(Small). board·teach로 열린 종류만 보인다.")]
        [SerializeField] private TMP_Text _teacher;
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

        /// <summary>잠긴 카드에 lockedText와 열린 재료 이름 및 배열을 보이고, 나머지 보상 정보는 숨긴다.</summary>
        public void ShowLocked(string lockedText, string materials)
        {
            ShowLocked(lockedText);
            _meta.text = materials;
            _meta.gameObject.SetActive(true);
            _diagram.gameObject.SetActive(true);
            if (_silhouette != null) _silhouette.transform.SetAsFirstSibling();
        }

        /// <summary>name과 knownVisitor만 공개하고 잠긴 건물의 재료 배열은 숨긴다.</summary>
        public void ShowKnownVisitor(string name, string knownVisitor)
        {
            ShowLocked(name);
            _npc.text = knownVisitor;
            _npc.gameObject.SetActive(true);
        }

        /// <summary>선로에서 얻은 설계 단서로 이름·제작법·마을 일은 보이고 방문자는 감춘다.</summary>
        public void ShowHint(string name, string meta, string work, string role, string unknownVisitor)
        {
            ShowUnlocked(name, meta, work, unknownVisitor, role, false);
        }

        /// <summary>해금 카드: 이름·종류 줄·보너스·NPC를 쓰고 완성 표시를 켜거나 끈다. 배열은 Diagram에 따로 채운다.</summary>
        public void ShowUnlocked(string name, string meta, string bonus, string npc, bool complete) => ShowUnlocked(name, meta, bonus, npc, null, complete);

        /// <summary>스토리 030. teacher("가르쳐 주는 이: …")가 비면 그 줄을 끈다.</summary>
        public void ShowUnlocked(string name, string meta, string bonus, string npc, string teacher, bool complete)
        {
            SetYield(null);
            SetProduction(null, null, null, null);
            if (_teacher != null)
            {
                _teacher.text = teacher ?? string.Empty;
                _teacher.gameObject.SetActive(!string.IsNullOrEmpty(teacher));
            }
            _name.text = name;
            _meta.text = meta;
            _bonus.text = bonus;
            _npc.text = npc;
            SetUnlockedParts(true);
            if (_badge != null) _badge.SetActive(complete);
            ClearDiagram();
        }

        /// <summary>text를 생산 정보에 표시하고, 값이 없으면 해당 줄을 숨긴다.</summary>
        public void SetYield(string text)
        {
            if (_yield == null) return;
            _yield.text = text ?? string.Empty;
            _yield.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>생산 및 수입 스프라이트와 문구를 각 행에 반영하고 값이 없는 행을 숨긴다.</summary>
        public void SetProduction(Sprite resourceSprite, string resourceText, Sprite incomeSprite, string incomeText)
        {
            if (_resourceIcon != null)
            {
                _resourceIcon.sprite = resourceSprite;
                _resourceIcon.gameObject.SetActive(!string.IsNullOrEmpty(resourceText) && resourceSprite != null);
            }
            if (_resourceText != null)
            {
                _resourceText.text = resourceText ?? string.Empty;
                _resourceText.gameObject.SetActive(!string.IsNullOrEmpty(resourceText));
            }
            if (_incomeIcon != null)
            {
                if (incomeSprite != null) _incomeIcon.sprite = incomeSprite;
                _incomeIcon.gameObject.SetActive(!string.IsNullOrEmpty(incomeText) && _incomeIcon.sprite != null);
            }
            if (_incomeText != null)
            {
                _incomeText.text = string.IsNullOrEmpty(incomeText) ? string.Empty :
                    (_incomeIcon == null || _incomeIcon.sprite == null ? "마르크 " + incomeText : incomeText);
                _incomeText.gameObject.SetActive(!string.IsNullOrEmpty(incomeText));
            }
        }

        /// <summary>배열 격자의 열 수(패턴이면 행 길이, 아니면 0으로 기본값).</summary>
        public void SetColumns(int columns)
        {
            if (_diagramGrid == null) return;
            _diagramGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _diagramGrid.constraintCount = Mathf.Max(1, columns > 0 ? columns : _flowColumns);
        }

        /// <summary>unlocked에 따라 카드의 공개 정보와 실루엣 표시를 전환한다.</summary>
        private void SetUnlockedParts(bool unlocked)
        {
            if (!unlocked) { SetYield(null); SetProduction(null, null, null, null); }
            _meta.gameObject.SetActive(unlocked);
            _bonus.gameObject.SetActive(unlocked);
            _npc.gameObject.SetActive(unlocked);
            if (_teacher != null && !unlocked) _teacher.gameObject.SetActive(false);
            _diagram.gameObject.SetActive(unlocked);
            if (_silhouette != null) _silhouette.SetActive(!unlocked);
        }

        /// <summary>기존 배열 아이콘을 숨기고 제거해 카드의 배열을 비운다.</summary>
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
