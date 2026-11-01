using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Npc.Demo
{
    /// <summary>
    /// 스토리 017 캡처용 스크립트 데모: `--npc-demo [--npc-stage homes|panel|encouraged]`. 실행 인자 또는 진행 중인 브리지 캡처의
    /// extraArgs(SessionState "IronRing.Bridge.Capture.ExtraArgs")에서 읽는다(HabitatDemoDriver와 같은 방식).
    /// 데모 거점(데이터 Resources/Npc/NpcHomesDemo.json, 기본 hub-1)의 내부 화면이 열리면 가스등 4칸 L자(위 구역)와 유리 4칸 일자(아래 구역)를
    /// IItemPlacement로 놓고, 대상 NPC(점등부·원예사)가 모두 합류할 때까지 INpcRecruiter.OnHubVisited를 최대 데이터 횟수(24)만큼 부른다.
    /// 그 뒤 집을 "NPC DEMO: homes → id:type[cells]"로 남기고 StageReady를 낸다. 단계 panel/encouraged는 HUD가 StageReady에서 패널을 연다.
    /// encouraged는 여기서 첫 NPC를 격려하고 결과를 남긴다.
    /// </summary>
    public sealed class NpcHomesDemoDriver : MonoBehaviour
    {
        private const string FLAG = "--npc-demo";
        private const string STAGE_FLAG = "--npc-stage";
        private const string SETTINGS_PATH = "Npc/NpcHomesDemo";
        private static NpcHomesDemoDriver _instance;
        private static Action<string> _stageReady;
        private Settings _settings;
        private int _waited;
        private bool _opened;
        private bool _done;

        /// <summary>데모 단계(homes|panel|encouraged). --npc-demo가 없으면 null, 단계 인자가 없으면 "homes".</summary>
        public static string Stage { get; private set; }

        /// <summary>합류가 끝나 StageReady를 낸 뒤 true.</summary>
        public static bool IsStageReady { get; private set; }

        /// <summary>패널을 열 NPC(대상 NPC 중 먼저 합류한 쪽, 없으면 거점의 첫 NPC). StageReady 전에는 null.</summary>
        public static string FirstNpcId { get; private set; }

        /// <summary>
        /// 합류가 끝난 뒤 한 번, 첫 NPC id와 함께 발생한다(Stage로 단계를 읽는다). 이미 끝났으면 구독 즉시 호출된다.
        /// encouraged 단계는 격려를 실행한 뒤에 발생한다.
        /// </summary>
        public static event Action<string> StageReady
        {
            add { _stageReady += value; if (IsStageReady) value?.Invoke(FirstNpcId); }
            remove { _stageReady -= value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Ready -= Create;
            _instance = null;
            _stageReady = null;
            Stage = null;
            IsStageReady = false;
            FirstNpcId = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            ReadStage();
            GameServices.Ready -= Create;
            GameServices.Ready += Create;
        }

        /// <summary>인자에서 단계를 읽는다(플래그가 없으면 Stage는 null).</summary>
        private static void ReadStage()
        {
            string[] args = Arguments();
            if (!args.Contains(FLAG)) return;
            int at = Array.IndexOf(args, STAGE_FLAG);
            Stage = at >= 0 && at + 1 < args.Length ? args[at + 1] : "homes";
        }

        private static void Create()
        {
            if (Stage == null) ReadStage();
            if (_instance != null || Stage == null) return;
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>(SETTINGS_PATH);
            if (asset == null) { UnityEngine.Debug.LogError("NPC DEMO: Resources/" + SETTINGS_PATH + ".json 없음"); return; }
            _instance = new GameObject(nameof(NpcHomesDemoDriver)).AddComponent<NpcHomesDemoDriver>();
            _instance._settings = JsonUtility.FromJson<Settings>(asset.text);
            Application.runInBackground = true;
            UnityEngine.Debug.Log("NPC DEMO: 단계 " + Stage);
        }

        void Update()
        {
            if (_done || _settings == null) return;
            string hubId = _settings._hubId;
            IViewMode view = GameServices.Get<IViewMode>();
            if (view == null) return;
            if (!view.InteriorOpen || view.InteriorHubId != hubId)
            {
                if (!_opened) { _opened = true; GameServices.Get<IInteriorNavigation>()?.Open(hubId); }
                return;
            }
            if (_waited++ < _settings._waitFrames) return;
            _done = true;
            try { Run(hubId); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }

        private void Run(string hubId)
        {
            IHubInterior interior = GameServices.Get<IHubInterior>();
            IItemPlacement placement = GameServices.Get<IItemPlacement>();
            IItemInventory items = GameServices.Get<IItemInventory>();
            INpcRecruiter npcs = GameServices.Get<INpcRecruiter>();
            INpcHomes homes = GameServices.Get<INpcHomes>();
            INpcInteraction interaction = GameServices.Get<INpcInteraction>();
            if (interior == null || placement == null || items == null || npcs == null || homes == null || interaction == null)
            {
                UnityEngine.Debug.LogError("NPC DEMO: 필요한 서비스가 없습니다(IHubInterior/IItemPlacement/IItemInventory/INpcRecruiter/INpcHomes/INpcInteraction).");
                return;
            }

            Place(hubId, interior, placement, items);

            string[] targets = _settings._targetNpcIds ?? Array.Empty<string>();
            int visits = 0;
            while (visits < _settings._maxVisits && !targets.All(id => npcs.NpcsAt(hubId).Contains(id)))
            {
                visits++;
                npcs.OnHubVisited(hubId);
            }
            IReadOnlyList<string> joined = npcs.NpcsAt(hubId);
            UnityEngine.Debug.Log($"NPC DEMO {hubId}: 방문 {visits}회, 합류 {string.Join(", ", joined)}");
            if (!targets.All(joined.Contains))
                UnityEngine.Debug.LogWarning($"NPC DEMO {hubId}: 방문 {_settings._maxVisits}회 안에 대상({string.Join(", ", targets)})이 모두 합류하지 않았습니다.");

            foreach (NpcHome home in homes.HomesAt(hubId))
                UnityEngine.Debug.Log($"NPC DEMO: homes → {home.NpcId}:{home.HabitatTypeId}[{string.Join(",", home.Cells)}]");

            FirstNpcId = joined.FirstOrDefault(targets.Contains) ?? joined.FirstOrDefault();
            if (FirstNpcId != null)
                UnityEngine.Debug.Log($"NPC DEMO: 첫 NPC {FirstNpcId} '{interaction.DisplayName(FirstNpcId)}' 대사 \"{interaction.CurrentLine(FirstNpcId)}\"");

            // 코디네이터 결정: encouraged 단계의 격려는 HUD가 버튼과 같은 경로로 누른다(결과 문장이 패널에 보이도록). 드라이버 직접 실행은 데이터로 켤 수 있다.
            if (Stage == "encouraged" && FirstNpcId != null && _settings._driverEncourages)
            {
                IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
                float before = hubs?.GetProsperity(hubId) ?? 0f;
                bool ok = interaction.TryInteract(FirstNpcId, _settings._encourageOptionId, out string result);
                NpcInteractionOption option = interaction.Options(FirstNpcId).FirstOrDefault(o => o.Id == _settings._encourageOptionId);
                UnityEngine.Debug.Log($"NPC DEMO: 격려 {FirstNpcId} = {ok} \"{result}\" 번영 {before} → {hubs?.GetProsperity(hubId)}, " +
                                      $"버튼 활성={option?.Enabled} 이유=\"{option?.Reason}\"");
            }

            IsStageReady = true;
            _stageReady?.Invoke(FirstNpcId);
        }

        /// <summary>데이터의 배치를 놓는다. 모자란 타일은 인벤토리에 넣고, 잔해 칸은 조사해 빈 터로 만든다(데모 준비).</summary>
        private void Place(string hubId, IHubInterior interior, IItemPlacement placement, IItemInventory items)
        {
            Placement[] list = _settings._placements ?? Array.Empty<Placement>();
            foreach (IGrouping<string, Placement> group in list.GroupBy(p => p._itemId))
            {
                int missing = group.Count() - items.Count(group.Key);
                if (missing > 0) items.Add(group.Key, missing);
            }
            IReadOnlyList<IInteriorCell> cells = interior.Cells(hubId);
            foreach (Placement p in list)
            {
                IInteriorCell cell = cells.FirstOrDefault(c => c.IsUpper == p._upper && c.Row == p._row && c.Column == p._column);
                if (cell != null && cell.State == InteriorCellState.Rubble)
                {
                    if (!interior.CanInvestigate(hubId, cell.Index)) interior.OnDocked(hubId);
                    if (interior.CanInvestigate(hubId, cell.Index)) interior.Investigate(hubId, cell.Index, out _);
                }
                bool ok = cell != null && placement.TryPlace(hubId, cell.Index, p._itemId);
                UnityEngine.Debug.Log($"NPC DEMO: 배치 {p._itemId} → {(p._upper ? "위" : "아래")}({p._row},{p._column}) 칸 {cell?.Index} = {ok}");
            }
        }

        /// <summary>실행 인자와 진행 중인 브리지 캡처의 extraArgs를 합친다(캡처가 끝난 뒤의 값은 무시).</summary>
        private static string[] Arguments()
        {
            string extra = "";
#if UNITY_EDITOR
            string phase = UnityEditor.SessionState.GetString("IronRing.Bridge.Phase", "");
            if (phase == "enter-play" || phase == "capture")
                extra = UnityEditor.SessionState.GetString("IronRing.Bridge.Capture.ExtraArgs", "");
#endif
            return Environment.GetCommandLineArgs().Concat(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class Settings
        {
            public string _hubId = "hub-1";
            public int _waitFrames = 20;
            public int _maxVisits = 24;
            public string[] _targetNpcIds;
            public string _encourageOptionId = "encourage";
            /// <summary>true면 드라이버가 격려를 직접 실행한다(기본 false: HUD가 누른다).</summary>
            public bool _driverEncourages = false;
            public Placement[] _placements;
        }

        [Serializable]
        private sealed class Placement
        {
            public string _itemId;
            public bool _upper;
            public int _row;
            public int _column;
        }
#pragma warning restore 0649
    }
}
