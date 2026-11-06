using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using IronRing.Hud.Ui;
using IronRing.Onboarding;

namespace IronRing.Hud.Onboarding
{
    /// <summary>
    /// 스펙 001 FR-012~FR-014. 항상 켜진 화면 "DevSituations". 에디터·개발 빌드에서 F9로 학습 항목 메뉴를 열고 닫으며,
    /// 위·아래 방향키로 행을 고르고 Enter나 클릭으로 그 상황을 만든다(OnboardingSituations.Run).
    /// 행 i는 OnboardingTopics.json을 order 순으로 정렬한 i번째 항목이다(빌더가 같은 순서로 행을 만든다).
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class DevSituationsScreen : MonoBehaviour
    {
        private static bool _isOpen;

        [Header("메뉴")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _rows;

        [Header("선택 표시")]
        [Tooltip("선택된 행 바탕색")]
        [SerializeField] private Color _selectedColor = new Color(1f, 0.82f, 0.1f, 1f);

        private readonly List<Button> _buttons = new();
        private readonly List<Color> _normalColors = new();
        private readonly List<string> _ids = new();
        private int _selected;

        /// <summary>메뉴가 열려 있으면 true. 다른 화면이 방향키·Enter·숫자키·Space를 무시할 때 본다(FR-017).</summary>
        public static bool IsOpen => _isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _isOpen = false;

        void Awake()
        {
            _panel.SetActive(false);
            _rows.GetComponentsInChildren(true, _buttons);
            for (int i = 0; i < _buttons.Count; i++)
            {
                int index = i;
                _normalColors.Add(_buttons[i].targetGraphic.color);
                _buttons[i].onClick.AddListener(() => Run(index));
            }
        }

        void OnDestroy() => _isOpen = false;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !(Application.isEditor || Debug.isDebugBuild)) return;
            if (keyboard.f9Key.wasPressedThisFrame) { SetOpen(!_isOpen); return; }
            if (!_isOpen) return;
            if (keyboard.upArrowKey.wasPressedThisFrame) Move(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Move(1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Run(_selected);
        }

        /// <summary>open이면 항목 id 목록을 order 순으로 다시 읽고 메뉴를 보이며, 아니면 숨긴다. IsOpen을 바꾼다(FR-012).</summary>
        private void SetOpen(bool open)
        {
            _isOpen = open;
            _panel.SetActive(open);
            if (!open) return;
            _ids.Clear();
            OnboardingDirector director = OnboardingInstaller.Director;
            if (director != null)
            {
                var topics = new List<OnboardingTopic>(director.Topics);
                topics.Sort((a, b) => a.Order.CompareTo(b.Order));
                foreach (OnboardingTopic topic in topics) _ids.Add(topic.Id);
            }
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _buttons.Count - 1));
            Paint();
        }

        /// <summary>선택 행을 step만큼 옮기고, 맨 위·맨 아래를 넘으면 반대 끝으로 돈다(FR-013).</summary>
        private void Move(int step)
        {
            if (_buttons.Count == 0) return;
            _selected = (_selected + step + _buttons.Count) % _buttons.Count;
            Paint();
        }

        /// <summary>선택 행만 _selectedColor로, 나머지는 원래 색으로 칠한다.</summary>
        private void Paint()
        {
            for (int i = 0; i < _buttons.Count; i++) _buttons[i].targetGraphic.color = i == _selected ? _selectedColor : _normalColors[i];
        }

        /// <summary>index 행을 고르고 메뉴를 닫은 뒤 그 항목 id로 상황을 만든다(FR-014).</summary>
        private void Run(int index)
        {
            if (index < 0 || index >= _ids.Count) return;
            _selected = index;
            SetOpen(false);
            OnboardingSituations.Run(_ids[index]);
        }
    }
}
