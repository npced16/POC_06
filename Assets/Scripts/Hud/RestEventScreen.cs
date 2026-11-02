using UnityEngine;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud
{
    /// <summary>스토리 051. 휴식지 사건의 제목·상황·선택지와 선택 결과를 모달 프리팹에 표시한다.</summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class RestEventScreen : MonoBehaviour
    {
        private static RestEventScreen _instance;

        [Header("사건 내용")]
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private Button[] _choices = new Button[3];
        [Header("선택 결과")]
        [SerializeField] private GameObject _resultBox;
        [SerializeField] private TMP_Text _result;
        [SerializeField] private TMP_Text _detail;
        [SerializeField] private Button _continue;

        private IRestEvents _events;
        private UiScreens _screens;
        private string _screenId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>Ready 이후 IRestEvents와 캔버스의 RestEvent 화면을 찾아 이벤트를 연결한다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IRestEvents>() == null) return;
            UiScreens screens = UiScreens.Instance;
            RestEventScreen placed = screens != null ? screens.Find<RestEventScreen>() : null;
            if (placed == null || placed._title == null || placed._continue == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        /// <summary>screens와 사건 서비스를 저장하고 선택지·계속 버튼 및 사건 이벤트를 연결한다.</summary>
        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screenId = GetComponent<UiScreen>().Id;
            _events = GameServices.Get<IRestEvents>();
            for (int i = 0; i < _choices.Length; i++)
            {
                if (_choices[i] == null) continue;
                int index = i;
                _choices[i].onClick.AddListener(() => _events.Choose(index));
            }
            _continue.onClick.AddListener(_events.Continue);
            _events.EventStarted += OnEventStarted;
            _events.EventResolved += Refresh;
            _events.EventClosed += OnEventClosed;
            if (_events.IsActive) OnEventStarted();
            else _screens.Hide(_screenId);
        }

        /// <summary>새 사건이 시작되면 모달을 열고 제목·상황·선택지 상태를 그린다.</summary>
        private void OnEventStarted()
        {
            _screens.Show(_screenId);
            Refresh();
        }

        /// <summary>선택 전에는 사용 가능한 선택지 버튼, 선택 뒤에는 결과·상세·계속 버튼을 표시한다.</summary>
        private void Refresh()
        {
            _title.text = _events.Title;
            _body.text = _events.Text;
            bool resolved = _events.HasResolved;
            for (int i = 0; i < _choices.Length; i++)
            {
                Button button = _choices[i];
                if (button == null) continue;
                bool visible = !resolved && i < _events.Choices.Count;
                button.gameObject.SetActive(visible);
                if (!visible) continue;
                bool available = _events.IsChoiceAvailable(i);
                button.interactable = available;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    string reason = available ? string.Empty : _events.UnavailableReason(i);
                    label.text = _events.Choices[i] + (string.IsNullOrEmpty(reason) ? string.Empty : "\n" + reason);
                }
            }
            _resultBox.SetActive(resolved);
            if (!resolved) return;
            _result.text = _events.ResultText;
            _detail.text = _events.ResultDetail;
            _detail.gameObject.SetActive(!string.IsNullOrEmpty(_events.ResultDetail));
            TMP_Text continueLabel = _continue.GetComponentInChildren<TMP_Text>(true);
            if (continueLabel != null) continueLabel.text = Strings.Get("loop_continue");
        }

        /// <summary>사건이 닫히면 모달을 숨겨 뒤 화면 입력을 다시 허용한다.</summary>
        private void OnEventClosed() { _screens.Hide(_screenId); }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_events != null)
            {
                _events.EventStarted -= OnEventStarted;
                _events.EventResolved -= Refresh;
                _events.EventClosed -= OnEventClosed;
            }
            if (_instance == this) _instance = null;
        }
    }
}
