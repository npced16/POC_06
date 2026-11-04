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
using IronRing.Npc.Demo;
using IronRing.Presentation.Items;

namespace IronRing.Hud.Codex
{
    /// <summary>
    /// 스토리 022(CodexHud의 UGUI 포팅). 상황 화면 "Codex": 서식지 레시피 도감. 상단 바 도감 버튼(Toggle)·C 키로 열고 닫고,
    /// Esc·닫기 버튼으로 닫는다. 카탈로그의 모든 종류를 카드 격자(CodexCard 부품)로 보인다: 잠김은 "???"+실루엣, 해금은 이름·종류·칸 수·
    /// 타일 배열(TileIcon 부품)·번영 보너스·찾아오는 이(합류한 사람은 직업명, 나머지 후보는 "???", 스토리 025), 현재 거점에서 완성된 종류에는 "완성". 해금마다 토스트(ToastScreen)를 한 프레임 늦게,
    /// 겹치면 차례로 띄운다. 키·토스트 큐는 화면이 꺼져 있어도 ScreenTicker로 돈다. 게임 상태는 읽기만 한다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class CodexScreen : MonoBehaviour
    {
        private const string NPC_DEMO_STAGE = "codex";
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
        private INpcRecruiter _recruiter;
        private INpcInteraction _interaction;
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
            if (_codex is HabitatCodex hintCodex) hintCodex.HintUnlocked += OnHintUnlocked;
            _codex.TileUnlocked += OnTileUnlocked;
            if (_clusters != null) { _clusters.HabitatCompleted += OnClustersChanged; _clusters.HabitatBroken += OnClustersChanged; }
            if (_view != null) _view.InteriorChanged += OnInteriorChanged;
            ResolveNpcServices();
            SetOpen(false);
            ScreenTicker.Add(Tick);
            if (CodexDemoDriver.Stage != null) CodexDemoDriver.StageReady += Open;
            // 스토리 025: --npc-demo --npc-stage codex(hub-1에 점등부를 합류시킨 뒤 도감을 연다).
            if (NpcHomesDemoDriver.Stage == NPC_DEMO_STAGE) NpcHomesDemoDriver.StageReady += OnNpcDemoReady;
        }

        private void SetOpen(bool open)
        {
            if (open) _screens.Show(_screen.Id); else _screens.Hide(_screen.Id);
            if (open) Rebuild();
        }

        /// <summary>typeId의 새 레시피를 source에 맞게 알리되 combo는 로직 토스트에 맡기고, 열린 도감은 갱신한다.</summary>
        private void OnRecipeUnlocked(string typeId, string source)
        {
            string npcId = _codex.UnlockNpcOf(typeId);
            if (npcId != null && (source == HabitatCodex.SOURCE_BOARD || source == HabitatCodex.SOURCE_TEACH))
            {
                // 스토리 030: "탑승: <직업>이 <서식지> 설계도를 건넸다" / "<직업>이 <서식지> 설계도를 가르쳐 줬다"
                string toast = string.Format(Text(source == HabitatCodex.SOURCE_BOARD ? "codex_toast_board" : "codex_toast_teach"), NpcName(npcId), HabitatName(typeId));
                _toasts.Enqueue(toast);
                UnityEngine.Debug.Log("CODEX TOAST: " + toast);
            }
            else if (source != HabitatCodex.SOURCE_COMBO)
            {
                string key = source == HabitatCodex.SOURCE_DISCOVER ? "codex_toast_discover" : "codex_toast_new";
                _toasts.Enqueue(string.Format(Text(key), HabitatName(typeId)));
            }
            if (IsOpen) Rebuild();
        }

        /// <summary>tileIds가 열리면 화면에 보이는 잠긴 카드의 재료 이름과 아이콘을 다시 그린다.</summary>
        private void OnTileUnlocked(IReadOnlyList<string> tileIds, string byRecipeId) { if (IsOpen) Rebuild(); }

        /// <summary>typeId의 새 설계 단서를 알리고 열린 도감의 카드를 갱신한다.</summary>
        private void OnHintUnlocked(string typeId)
        {
            _toasts.Enqueue(string.Format(Text("codex_toast_hint"), HabitatName(typeId)));
            if (IsOpen) Rebuild();
        }

        private void OnClustersChanged(string hubId, string typeId) { if (IsOpen) Rebuild(); }
        private void OnInteriorChanged(bool open) { if (IsOpen) Rebuild(); }
        private void OnNpcJoined(string hubId, string npcId) { if (IsOpen) Rebuild(); }
        private void OnNpcDemoReady(string firstNpcId) => Open();

        /// <summary>스토리 025. 모집·대화 서비스를 늦게라도 얻는다(합류하면 "찾아오는 이"를 다시 그린다).</summary>
        private void ResolveNpcServices()
        {
            if (_recruiter == null)
            {
                _recruiter = GameServices.Get<INpcRecruiter>();
                if (_recruiter != null) _recruiter.NpcJoined += OnNpcJoined;
            }
            if (_interaction == null) _interaction = GameServices.Get<INpcInteraction>();
        }

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

        /// <summary>현재 거점의 완성 건물과 도감 해금 상태를 읽어 전체 카드 목록을 다시 그린다.</summary>
        private void Rebuild()
        {
            ResolveNpcServices();
            string hub = CurrentHub();
            _lastHub = hub;
            var completed = new HashSet<string>();
            if (_clusters != null && hub != null)
                foreach (HabitatCluster cluster in _clusters.CompletedAt(hub)) completed.Add(cluster.TypeId);
            int total = _codex is HabitatCodex concrete ? concrete.TotalCount : _catalog.Types.Count;
            int hints = _codex is HabitatCodex hintState ? hintState.HintedTypeIds.Count : 0;
            _title.text = string.Format(Text("codex_title"), _codex.UnlockedTypeIds.Count, total, hints);
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
                if (!_codex.IsUnlocked(type.Id))
                {
                    if (_codex is HabitatCodex clues && clues.HasHint(type.Id))
                    {
                        card.ShowHint(HabitatName(type.Id),
                            KindName(type.Kind) + "  ·  " + string.Format(Text("codex_cells"), CellCount(type)),
                            type.VillageWork, type.MaterialRole,
                            string.Format(Text("codex_npc"), Text("codex_visitor_unknown")));
                        FillDiagram(card, type, true);
                        continue;
                    }
                    card.ShowLocked(Text("codex_locked"));
                    continue;
                }
                card.ShowUnlocked(HabitatName(type.Id),
                    KindName(type.Kind) + "  ·  " + string.Format(Text("codex_cells"), CellCount(type)),
                    string.Format(Text("codex_bonus"), type.ProsperityBonus.ToString("0")),
                    string.Format(Text("codex_npc"), Visitors(type)),
                    type.VillageWork,
                    completed.Contains(type.Id));
                FillDiagram(card, type);
            }
        }

        /// <summary>
        /// 스토리 025. "찾아오는 이": 그 종류의 후보(INpcRecruiter.CandidatesOf) 가운데 합류한 사람(JoinedOf)은 직업명, 나머지는 "???"
        /// (예: "점등부, ???"). 숨은 조건은 보이지 않는다. 후보 목록이 없으면(옛 구현) 예전처럼 NPC 종류 이름.
        /// </summary>
        private string Visitors(HabitatType type)
        {
            IReadOnlyList<string> candidates = _recruiter?.CandidatesOf(type.Id);
            if (candidates == null || candidates.Count == 0) return Lookup("npc_" + type.NpcTypeId) ?? type.NpcTypeId;
            var joined = new HashSet<string>(_recruiter.JoinedOf(type.Id) ?? (IReadOnlyList<string>)new string[0]);
            var names = new List<string>();
            int unknown = 0;
            foreach (string id in candidates)
            {
                if (joined.Contains(id)) names.Add(_interaction != null ? _interaction.DisplayName(id) : id);
                else unknown++;
            }
            for (int i = 0; i < unknown; i++) names.Add(Text("codex_visitor_unknown"));
            return string.Join(", ", names);
        }

        /// <summary>스토리 030. board·teach로 열린 종류면 "가르쳐 주는 이: 직업", 아니면 null.</summary>
        private string Teacher(string typeId)
        {
            string source = _codex.UnlockSourceOf(typeId);
            string npcId = _codex.UnlockNpcOf(typeId);
            if (npcId == null || (source != HabitatCodex.SOURCE_BOARD && source != HabitatCodex.SOURCE_TEACH)) return null;
            return string.Format(Text("codex_teacher"), NpcName(npcId));
        }

        private string NpcName(string npcId)
        {
            ResolveNpcServices();
            return _interaction != null ? _interaction.DisplayName(npcId) : npcId;
        }

        /// <summary>type의 재료 배열을 그린다. revealAll이 true면 잠긴 재료도 설계 단서에 공개한다.</summary>
        private void FillDiagram(CodexCardView card, HabitatType type, bool revealAll = false)
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
                        else Icon(card, tile, revealAll);
                    }
                return;
            }
            card.SetColumns(0);
            if (type.Kind == HabitatKind.Set)
            {
                int fixedCount = 0;
                foreach (HabitatTileRequirement tile in type.SetTiles)
                {
                    for (int i = 0; i < tile.MinCount; i++) Icon(card, tile.TileItemId, revealAll);
                    fixedCount += tile.MinCount;
                }
                int rest = type.RequiredCount - fixedCount;
                if (rest > 0) Tile(card).Show(null, "+" + rest, false);
                return;
            }
            for (int i = 0; i < type.RequiredCount; i++) Icon(card, type.TileItemId, revealAll);
        }

        private CodexTileView Tile(CodexCardView card) => Instantiate(_tilePrefab, card.Diagram);

        /// <summary>tileItemId의 아이콘을 카드에 넣고 revealAll이 false인 잠긴 재료는 감춘다.</summary>
        private void Icon(CodexCardView card, string tileItemId, bool revealAll)
        {
            if (!revealAll && !_codex.IsTileUnlocked(tileItemId))
            {
                Tile(card).Show(null, Text("codex_locked"), false);
                return;
            }
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
            NpcHomesDemoDriver.StageReady -= OnNpcDemoReady;
            if (_recruiter != null) _recruiter.NpcJoined -= OnNpcJoined;
            if (_codex != null) _codex.RecipeUnlocked -= OnRecipeUnlocked;
            if (_codex is HabitatCodex hintCodex) hintCodex.HintUnlocked -= OnHintUnlocked;
            if (_codex != null) _codex.TileUnlocked -= OnTileUnlocked;
            if (_clusters != null) { _clusters.HabitatCompleted -= OnClustersChanged; _clusters.HabitatBroken -= OnClustersChanged; }
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
            _instance = null;
        }
    }
}
