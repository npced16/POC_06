using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Habitats;
using IronRing.Habitats.Demo;
using IronRing.Hud.Hotbar;
using IronRing.Hud.Ui;
using IronRing.Localization;
using IronRing.Presentation.Items;

namespace IronRing.Hud.Codex
{
    /// <summary>
    /// 스토리 022(CodexHud의 UGUI 포팅). 상황 화면 "Codex": 서식지 레시피 도감. 상단 바 도감 버튼(Toggle)·C 키로 열고 닫고,
    /// Esc·닫기 버튼으로 닫는다. 카탈로그의 모든 종류를 카드 격자(CodexCard 부품)로 보인다: 잠김은 "???"+실루엣, 해금은 이름·종류·칸 수·
    /// 타일 배열(TileIcon 부품)·번영 보너스·찾아오는 NPC, 현재 거점에서 완성된 종류에는 "완성". 해금마다 토스트(ToastScreen)를 한 프레임 늦게,
    /// 겹치면 차례로 띄운다. 키·토스트 큐는 화면이 꺼져 있어도 ScreenTicker로 돈다. 게임 상태는 읽기만 한다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class CodexScreen : MonoBehaviour
    {
        private static CodexScreen _instance;
        /// <summary>현재 도감 화면. 초기화 전이면 null.</summary>
        public static CodexScreen Instance => _instance;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private Button _close;
        [SerializeField] private RectTransform _grid;
        [Tooltip("카드 부품(Parts/CodexCard.prefab).")]
        [SerializeField] private CodexCardView _cardPrefab;
        [Tooltip("타일 칸 부품(Parts/TileIcon.prefab).")]
        [SerializeField] private CodexTileView _tilePrefab;

        private UiScreens _screens;
        private UiScreen _screen;
        private IHabitatCodex _codex;
        private IHabitatClusters _clusters;
        private IViewMode _view;
        private ITrackRuntime _track;
        private IHubStateSystem _hubs;
        private HabitatCatalog _catalog;
        private ItemSpriteSet _sprites;
        private float _toastSeconds;
        private string _lastHub;
        private readonly Queue<string> _toasts = new Queue<string>();
        private float _nextToastAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>도감 서비스·서식지 카탈로그가 있고 캔버스에 화면이 있으면 한 번 바인딩한다.</summary>
        private static void Create()
        {
            if (_instance != null || GameServices.Get<IHabitatCodex>() == null || HabitatsInstaller.Catalog == null) return;
            UiScreens screens = UiScreens.Instance;
            CodexScreen placed = screens != null ? screens.Find<CodexScreen>() : null;
            if (placed == null || !placed.enabled || placed._cardPrefab == null || placed._tilePrefab == null || placed._grid == null) return;
            _instance = placed;
            placed.Initialize(screens);
        }

        /// <summary>도감이 열려 있는지.</summary>
        public static bool IsOpen => _instance != null && _instance._screens.IsOpen(_instance._screen.Id);

        /// <summary>도감을 열거나 닫는다(상단 바 버튼·C 키). 화면이 없으면 무시한다.</summary>
        public static void Toggle() { if (_instance != null) _instance.SetOpen(!IsOpen); }

        /// <summary>도감을 연다(데모용).</summary>
        public static void Open() { if (_instance != null) _instance.SetOpen(true); }

        /// <summary>도감을 닫는다.</summary>
        public static void Close() { if (_instance != null) _instance.SetOpen(false); }

        private void Initialize(UiScreens screens)
        {
            _screens = screens;
            _screen = GetComponent<UiScreen>();
            _codex = GameServices.Get<IHabitatCodex>();
            _clusters = GameServices.Get<IHabitatClusters>();
            _view = GameServices.Get<IViewMode>();
            _track = GameServices.Get<ITrackRuntime>();
            _hubs = GameServices.Get<IHubStateSystem>();
            _catalog = HabitatsInstaller.Catalog;
            _sprites = ItemSpriteSet.Load();
            _toastSeconds = HotbarData.Load().ToastSeconds;
            _hint.text = Text("codex_hint");
            TMP_Text closeLabel = _close.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null) closeLabel.text = Text("codex_close");
            _close.onClick.AddListener(Close);
            _codex.RecipeUnlocked += OnRecipeUnlocked;
            if (_clusters != null) { _clusters.HabitatCompleted += OnClustersChanged; _clusters.HabitatBroken += OnClustersChanged; }
            if (_view != null) _view.InteriorChanged += OnInteriorChanged;
            SetOpen(false);
            ScreenTicker.Add(Tick);
            if (CodexDemoDriver.Stage != null) CodexDemoDriver.StageReady += Open;
        }

        private void SetOpen(bool open)
        {
            if (open) _screens.Show(_screen.Id); else _screens.Hide(_screen.Id);
            if (open) Rebuild();
        }

        private void OnRecipeUnlocked(string typeId, string source)
        {
            string key = source == HabitatCodex.SOURCE_DISCOVER ? "codex_toast_discover" : "codex_toast_new";
            _toasts.Enqueue(string.Format(Text(key), HabitatName(typeId)));
            if (IsOpen) Rebuild();
        }

        private void OnClustersChanged(string hubId, string typeId) { if (IsOpen) Rebuild(); }
        private void OnInteriorChanged(bool open) { if (IsOpen) Rebuild(); }

        /// <summary>화면이 꺼져 있어도 도는 일: C·Esc 키, 해금 토스트 큐, 기준 거점이 바뀌면 다시 그리기.</summary>
        private void Tick()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.cKey.wasPressedThisFrame) Toggle();
                else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) Close();
            }
            // 큐에 넣은 다음 프레임 이후에 띄운다(같은 프레임의 "완성!" 토스트가 덮어쓰지 않게).
            float now = Time.realtimeSinceStartup;
            if (_toasts.Count > 0 && now >= _nextToastAt)
            {
                ToastScreen.Show(_toasts.Dequeue(), _toastSeconds);
                _nextToastAt = now + _toastSeconds;
            }
            if (IsOpen && CurrentHub() != _lastHub) Rebuild();
        }

        /// <summary>완성 표시 기준 거점: 열린 내부 화면, 아니면 정박한 거점, 아니면 마지막에 본 거점(처음엔 첫 거점).</summary>
        private string CurrentHub()
        {
            if (_view != null && _view.InteriorOpen && _view.InteriorHubId != null) return _view.InteriorHubId;
            ITrackNode node = _track?.CurrentNode;
            if (node != null && node.Kind == TrackNodeKind.Hub) return node.HubId;
            if (_lastHub != null) return _lastHub;
            return _hubs != null && _hubs.HubIds.Count > 0 ? _hubs.HubIds[0] : null;
        }

        // ----- 그리기 -----

        private void Rebuild()
        {
            string hub = CurrentHub();
            _lastHub = hub;
            var completed = new HashSet<string>();
            if (_clusters != null && hub != null)
                foreach (HabitatCluster cluster in _clusters.CompletedAt(hub)) completed.Add(cluster.TypeId);
            _title.text = string.Format(Text("codex_title"), _codex.UnlockedTypeIds.Count, _catalog.Types.Count);
            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                GameObject child = _grid.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            foreach (HabitatType type in _catalog.Types)
            {
                CodexCardView card = Instantiate(_cardPrefab, _grid);
                card.name = "Card_" + type.Id;
                if (!_codex.IsUnlocked(type.Id)) { card.ShowLocked(Text("codex_locked")); continue; }
                card.ShowUnlocked(HabitatName(type.Id),
                    KindName(type.Kind) + "  ·  " + string.Format(Text("codex_cells"), CellCount(type)),
                    string.Format(Text("codex_bonus"), type.ProsperityBonus.ToString("0")),
                    string.Format(Text("codex_npc"), Lookup("npc_" + type.NpcTypeId) ?? type.NpcTypeId),
                    completed.Contains(type.Id));
                FillDiagram(card, type);
            }
        }

        /// <summary>같은 타일: 아이콘×개수. 혼합: 타일별 아이콘×최소 수 + "+k 아무거나". 패턴: 템플릿 격자(빈 칸은 흐린 칸).</summary>
        private void FillDiagram(CodexCardView card, HabitatType type)
        {
            if (type.Kind == HabitatKind.Pattern)
            {
                int columns = 0;
                foreach (IReadOnlyList<string> row in type.Pattern) columns = Mathf.Max(columns, row.Count);
                card.SetColumns(columns);
                foreach (IReadOnlyList<string> row in type.Pattern)
                    for (int i = 0; i < columns; i++)
                    {
                        string tile = i < row.Count ? row[i] : HabitatType.DontCare;
                        if (tile == HabitatType.DontCare) Tile(card).Show(null, null, true);
                        else Icon(card, tile);
                    }
                return;
            }
            card.SetColumns(0);
            if (type.Kind == HabitatKind.Set)
            {
                int fixedCount = 0;
                foreach (HabitatTileRequirement tile in type.SetTiles)
                {
                    for (int i = 0; i < tile.MinCount; i++) Icon(card, tile.TileItemId);
                    fixedCount += tile.MinCount;
                }
                int rest = type.RequiredCount - fixedCount;
                if (rest > 0) Tile(card).Show(null, "+" + rest, false);
                return;
            }
            for (int i = 0; i < type.RequiredCount; i++) Icon(card, type.TileItemId);
        }

        private CodexTileView Tile(CodexCardView card) => Instantiate(_tilePrefab, card.Diagram);

        private void Icon(CodexCardView card, string tileItemId)
        {
            Sprite sprite = _sprites != null ? _sprites.Get(tileItemId) : null;
            string name = Lookup("item_" + tileItemId) ?? tileItemId;
            Tile(card).Show(sprite, name.Substring(0, 1), false);
        }

        private static int CellCount(HabitatType type)
        {
            if (type.Kind != HabitatKind.Pattern) return type.RequiredCount;
            int count = 0;
            foreach (IReadOnlyList<string> row in type.Pattern)
                foreach (string tile in row) if (tile != HabitatType.DontCare) count++;
            return count;
        }

        private static string KindName(HabitatKind kind) => Text(kind == HabitatKind.Pattern ? "codex_kind_pattern" : kind == HabitatKind.Set ? "codex_kind_set" : "codex_kind_cluster");
        private static string HabitatName(string typeId) => Lookup("habitat_type_" + typeId) ?? typeId;
        private static string Text(string key) => Lookup(key) ?? key;

        private static string Lookup(string key)
        {
            try { return Strings.Get(key); }
            catch (KeyNotFoundException) { return null; }
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_instance != this) return;
            ScreenTicker.Remove(Tick);
            CodexDemoDriver.StageReady -= Open;
            if (_codex != null) _codex.RecipeUnlocked -= OnRecipeUnlocked;
            if (_clusters != null) { _clusters.HabitatCompleted -= OnClustersChanged; _clusters.HabitatBroken -= OnClustersChanged; }
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            _instance = null;
        }
    }
}
