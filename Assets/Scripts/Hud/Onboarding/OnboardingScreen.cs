using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Onboarding;
using IronRing.Presentation.Interior;

namespace IronRing.Hud.Onboarding
{
    /// <summary>
    /// 스펙 001 FR-006~FR-010. 항상 켜진 화면 "Onboarding". OnboardingDirector의 Shown에 마스코트 표정과 문구를 화면 아래 왼쪽 팝업으로 띄우고,
    /// 안내형이면 확인 버튼을 보이며, Closed에 팝업을 숨긴다. 매 프레임 Director.Tick을 불러 상태 변화를 살피게 한다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class OnboardingScreen : MonoBehaviour
    {
        private const string TOPICS_PATH = "Onboarding/OnboardingTopics";

        [Header("팝업")]
        [SerializeField] private GameObject _box;
        [SerializeField] private Image _mascot;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Button _confirm;

        [Header("마스코트 표정")]
        [SerializeField] private Sprite _mascotDefault;
        [SerializeField] private Sprite _mascotHappy;
        [SerializeField] private Sprite _mascotAlert;

        [Header("안내 배치")]
        [Tooltip("지정 칸과 퀵슬롯 강조색")]
        [SerializeField] private Color _guideColor = new Color(1f, 0.82f, 0.1f, 1f);
        [Tooltip("안내 배치 중 나머지 칸을 흐리는 색")]
        [SerializeField] private Color _dimColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        [Tooltip("강조 깜빡임 한 주기(초)")]
        [SerializeField] private float _pulseSeconds = 0.8f;
        [Tooltip("안내 칸을 고를 때 이 비율보다 화면을 넓게 덮는 UI는 배경으로 보고 무시")]
        [SerializeField, Range(0f, 1f)] private float _ignoreUiAreaRatio = 0.5f;

        [Header("귀환 강조")]
        [Tooltip("귀환 안내 때 카메라를 역 쪽으로 옮기는 시간(초)")]
        [SerializeField] private float _focusSeconds = 0.6f;
        [Tooltip("귀환 안내 때 카메라 크기 비율(1보다 작을수록 확대)")]
        [SerializeField, Range(0.1f, 1f)] private float _focusZoomRatio = 0.7f;

        [Header("테스트")]
        [Tooltip("에디터에서 Play Mode를 시작할 때 본 항목 기록을 지워 처음부터 순서대로 띄운다(빌드에서는 무시)")]
        [SerializeField] private bool _resetProgressInEditor = true;

        [Header("등장 효과")]
        [Tooltip("팝업이 튀어나오는 시간(초)")]
        [SerializeField] private float _popSeconds = 0.3f;
        [Tooltip("등장 시작 크기 비율")]
        [SerializeField] private float _popStartScale = 0.6f;

        private OnboardingDirector _director;
        private OnboardingGuide _guide;
        private OnboardingCameraFocus _focus;
        private IItemPlacement _placement;
        private float _popStarted = -1f;
        private string _unlockedTile;
        private string[] _unlockGuide;
        private readonly List<RaycastResult> _uiHits = new();
        private readonly Vector3[] _corners = new Vector3[4];

        void Awake()
        {
            _box.SetActive(false);
            _confirm.onClick.AddListener(OnConfirmClicked);
            // 시작할 때 마을 내부가 먼저 열려도 첫 안내가 뜨도록, 다른 Start보다 앞선 Awake에서 기록을 지운다(FR-025).
            if (Application.isEditor && _resetProgressInEditor) ResetProgress();
        }

        void OnDestroy()
        {
            if (_director == null) return;
            _director.Shown -= OnShown;
            _director.Closed -= OnClosed;
            if (_placement != null) { _placement.ItemPlaced -= OnItemPlaced; _placement.ItemRemoved -= OnItemRemoved; }
            _guide?.Stop();
        }

        void Update()
        {
            if (_director == null) Bind();
            _director?.Tick();
            if (_guide != null) TickUnlockPick();
            _guide?.Tick();
            _focus?.Tick();
            TickPop();
        }

        void LateUpdate() => _guide?.LateTick();

        /// <summary>등록된 OnboardingDirector를 찾아 Shown·Closed를 구독하고 안내 배치를 만든다. 이미 떠 있는 항목이 있으면 바로 그린다.</summary>
        private void Bind()
        {
            _director = OnboardingInstaller.Director;
            if (_director == null) return;
            _director.Shown += OnShown;
            _director.Closed += OnClosed;
            _guide = new OnboardingGuide(_guideColor, _dimColor, _pulseSeconds, IsCoveredByUi);
            _focus = new OnboardingCameraFocus(_focusSeconds, _focusZoomRatio);
            _placement = GameServices.Get<IItemPlacement>();
            if (_placement != null) { _placement.ItemPlaced += OnItemPlaced; _placement.ItemRemoved += OnItemRemoved; }
            OnboardingTopic current = _director.Find(_director.Current);
            if (current != null) OnShown(current);
        }

        /// <summary>OnboardingTopics.json의 모든 항목 id로 본 항목 기록을 지운다(FR-025). Director가 아직 없어도 동작한다.</summary>
        private void ResetProgress()
        {
            var ids = new List<string>();
            TextAsset asset = Resources.Load<TextAsset>(TOPICS_PATH);
            foreach (OnboardingTopic topic in OnboardingTopicsData.Parse(asset != null ? asset.text : null).Topics) ids.Add(topic.Id);
            OnboardingProgress.ResetAll(ids);
            Debug.Log("ONBOARDING: progress reset (editor)");
        }

        /// <summary>topic의 표정 그림과 문구를 등장 효과와 함께 팝업에 보이고, 안내형일 때만 확인 버튼을 켠다. 안내 배치 패턴이 있으면 시작한다(FR-006, FR-007, FR-018, FR-021).</summary>
        private void OnShown(OnboardingTopic topic)
        {
            Sprite sprite = MascotOf(topic.Mascot);
            _mascot.sprite = sprite;
            _mascot.gameObject.SetActive(sprite != null);
            _text.text = Strings.Get(topic.TextKey);
            _confirm.gameObject.SetActive(!topic.IsAction);
            _box.SetActive(true);
            _popStarted = Time.unscaledTime;
            // 강조는 지금 떠 있는 팝업의 것만 보인다. 앞 팝업이 남긴 안내 칸·퀵슬롯 강조는 먼저 지운다.
            ClearHighlights();
            if (topic.Id == "unlock_material")
            {
                // 해금 안내는 새 재료 퀵슬롯을 깜빡이고, 팝업이 떠 있는 동안 그 재료를 집으면 안내 칸도 켠다.
                _unlockedTile = _director.LastUnlockedTile;
                _unlockGuide = topic.Guide;
                _guide.HighlightSlot(_unlockedTile);
            }
            else if (topic.Guide.Length > 0) _guide.Begin(topic.Guide);
            if (topic.Id == "return_town") _focus.Begin();
        }

        /// <summary>topicId 항목이 닫히면 팝업을 숨기고, 그 팝업의 안내 칸·퀵슬롯 강조를 모두 끈다(FR-010, FR-021).</summary>
        private void OnClosed(string topicId)
        {
            _box.SetActive(false);
            ClearHighlights();
        }

        /// <summary>안내 배치(칸 강조·놓기 제한), 퀵슬롯 강조, 귀환 카메라 강조를 끄고 해금 안내 대기 상태(_unlockedTile, _unlockGuide)를 지운다.</summary>
        private void ClearHighlights()
        {
            _guide.Stop();
            _guide.HighlightSlot(null);
            _focus.End();
            _unlockedTile = null;
            _unlockGuide = null;
        }

        /// <summary>
        /// 해금 안내가 떠 있는 동안 새 재료(_unlockedTile)를 집으면 예약한 안내 배치(_unlockGuide)를 한 번 시작한다(FR-021).
        /// 퀵슬롯 강조는 팝업이 닫힐 때까지 그대로 둔다.
        /// </summary>
        private void TickUnlockPick()
        {
            if (_unlockGuide == null || _unlockedTile == null || PlacementGhost.Instance == null) return;
            PlacementEntry selected = PlacementGhost.Instance.Selected;
            if (!selected.IsValid || selected.Id != _unlockedTile) return;
            if (_unlockGuide.Length > 0) _guide.Begin(_unlockGuide);
            _unlockGuide = null;
        }

        /// <summary>hubId 마을 cellIndex 칸에 itemId가 놓이면 안내 배치에 알린다(FR-020).</summary>
        private void OnItemPlaced(string hubId, int cellIndex, string itemId) => _guide?.OnItemPlaced(hubId, cellIndex, itemId);

        /// <summary>hubId 마을 cellIndex 칸의 itemId가 회수되면 안내 배치에 알려 그 칸을 다시 지정 칸으로 돌린다.</summary>
        private void OnItemRemoved(string hubId, int cellIndex, string itemId) => _guide?.OnItemRemoved(hubId, cellIndex, itemId);

        /// <summary>팝업이 뜬 뒤 _popSeconds 동안 _popStartScale에서 살짝 넘쳤다가 1로 돌아오도록 상자 크기를 바꾼다.</summary>
        private void TickPop()
        {
            if (_popStarted < 0f) return;
            const float OVERSHOOT = 1.70158f;
            float t = Mathf.Clamp01((Time.unscaledTime - _popStarted) / Mathf.Max(0.01f, _popSeconds));
            float eased = 1f + (OVERSHOOT + 1f) * Mathf.Pow(t - 1f, 3f) + OVERSHOOT * Mathf.Pow(t - 1f, 2f);
            _box.transform.localScale = Vector3.one * Mathf.LerpUnclamped(_popStartScale, 1f, eased);
            if (t >= 1f) _popStarted = -1f;
        }

        /// <summary>
        /// world 위치가 화면 밖이거나 화면 UI(팝업 상자, 퀵슬롯 등 클릭을 받는 UI)에 가려지면 true를 돌려준다(FR-018 안내 칸 고르기).
        /// 팝업 상자는 등장 효과 중에도 원래 크기로 따지고, 화면의 _ignoreUiAreaRatio 넘게 덮는 UI는 배경으로 보고 무시한다.
        /// </summary>
        private bool IsCoveredByUi(Vector3 world)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z < 0f || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height) return true;

            var boxRect = (RectTransform)_box.transform;
            Canvas root = _box.GetComponentInParent<Canvas>().rootCanvas;
            Camera uiCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            // 등장 효과로 줄어든 크기 대신 부모 기준 원래 크기 상자로 판정한다.
            if (_box.activeInHierarchy && RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)boxRect.parent, screen, uiCamera, out Vector2 local))
            {
                Rect full = new Rect(boxRect.rect.position + (Vector2)boxRect.localPosition, boxRect.rect.size);
                if (full.Contains(local)) return true;
            }

            if (EventSystem.current == null) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = screen };
            _uiHits.Clear();
            EventSystem.current.RaycastAll(pointer, _uiHits);
            float ignoreArea = Screen.width * Screen.height * _ignoreUiAreaRatio;
            foreach (RaycastResult hit in _uiHits)
            {
                if (hit.gameObject.transform is not RectTransform rect) continue;
                rect.GetWorldCorners(_corners);
                Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, _corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(uiCamera, _corners[2]);
                if (Mathf.Abs((max.x - min.x) * (max.y - min.y)) < ignoreArea) return true;
            }
            return false;
        }

        /// <summary>확인 버튼을 누르면 Director에 떠 있는 안내형 항목을 닫게 한다(FR-008).</summary>
        private void OnConfirmClicked() => _director?.Confirm();

        /// <summary>mascot 표정 이름(default, happy, alert)에 맞는 그림을 돌려준다. 모르는 이름이거나 그림이 없으면 기본 표정.</summary>
        private Sprite MascotOf(string mascot) => mascot switch
        {
            "happy" => _mascotHappy != null ? _mascotHappy : _mascotDefault,
            "alert" => _mascotAlert != null ? _mascotAlert : _mascotDefault,
            _ => _mascotDefault,
        };
    }
}
