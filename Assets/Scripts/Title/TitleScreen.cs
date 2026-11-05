using UnityEngine;
using UnityEngine.UI;

using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

using IronRing.Localization;
using IronRing.Save;

namespace IronRing.Title
{
    /// <summary>스펙 002 FR-003~FR-007. 타이틀 화면의 제목·버튼 문구, 이어하기 활성 판정, 새 게임·이어하기·종료 처리.</summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [Header("제목")]
        [SerializeField] private TMP_Text _gameTitle;
        [SerializeField] private TMP_Text _gameTitleShadow;

        [Header("버튼")]
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private TMP_Text _newGameLabel;
        [SerializeField] private TMP_Text _continueLabel;
        [SerializeField] private TMP_Text _quitLabel;

        [Header("슬롯 선택")]
        [SerializeField] private SlotSelectPanel _slotPanel;

        private void Awake()
        {
            _newGameButton.onClick.AddListener(OnNewGame);
            _continueButton.onClick.AddListener(OnContinue);
            _quitButton.onClick.AddListener(OnQuit);
        }

        private void OnEnable()
        {
            string title = Strings.Get("title_game_name");
            _gameTitle.text = title;
            _gameTitleShadow.text = title;
            _newGameLabel.text = Strings.Get("title_new_game");
            _continueLabel.text = Strings.Get("title_continue");
            _quitLabel.text = Strings.Get("title_quit");
            _continueButton.interactable = SaveSlots.HasAny();
        }

        /// <summary>새 게임 버튼 처리. SaveSlots.StartNew()로 새 게임을 시작한다(FR-005).</summary>
        private void OnNewGame() => SaveSlots.StartNew();

        /// <summary>이어하기 버튼 처리. 슬롯 선택 패널을 연다(FR-007).</summary>
        private void OnContinue() => _slotPanel.Open();

        /// <summary>종료 버튼 처리. 확인 없이 게임을 끝낸다. 편집기에서는 Play를 멈춘다(FR-006).</summary>
        private void OnQuit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
