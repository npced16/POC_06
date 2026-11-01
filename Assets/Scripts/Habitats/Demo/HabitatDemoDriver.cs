using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Habitats.Demo
{
    /// <summary>
    /// 스토리 016/020 캡처용 스크립트 데모(--habitat-demo, 단계 --habitat-stage complete|incomplete, 기본 complete). 실행 인자 또는 진행 중인
    /// 브리지 캡처의 extraArgs에서 읽는다(MainRoot와 같은 SessionState 방식). 데모 거점(데이터, 기본 hub-1)의 내부 화면이 열리면(안 열려 있으면
    /// IInteriorNavigation.Open) 데이터의 배치 목록을 IItemPlacement로 놓는다. complete = _placements(같은 타일 묶음·혼합 묶음·패턴 하나씩 완성),
    /// incomplete = _incompletePlacements(혼합 묶음 하나가 타일별 최소 수를 못 채움, 나머지 둘은 완성). 그 뒤 서식지가 완성되면 INpcRecruiter.OnHubVisited로 방문을 일으켜 NPC를 합류시킨다.
    /// 합류 확률(0.5)은 그대로 두고, 합류할 때까지 데이터의 횟수만큼 방문을 반복한다(방문 한 번당 판정 한 번 규칙은 지킨다).
    /// </summary>
    public sealed class HabitatDemoDriver : MonoBehaviour
    {
        private const string FLAG = "--habitat-demo";
        private const string STAGE_FLAG = "--habitat-stage";
        private const string STAGE_INCOMPLETE = "incomplete";
        private static HabitatDemoDriver _instance;
        private Settings _settings;
        private string _stage;
        private int _waited;
        private bool _opened;
        private bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        private static void Create()
        {
            string[] args = Arguments();
            if (_instance != null || !args.Contains(FLAG)) return;
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>("Habitats/HabitatDemo");
            if (asset == null) { UnityEngine.Debug.LogError("HABITAT DEMO: Resources/Habitats/HabitatDemo.json 없음"); return; }
            _instance = new GameObject(nameof(HabitatDemoDriver)).AddComponent<HabitatDemoDriver>();
            _instance._settings = JsonUtility.FromJson<Settings>(asset.text);
            int stageAt = Array.IndexOf(args, STAGE_FLAG);
            _instance._stage = stageAt >= 0 && stageAt + 1 < args.Length ? args[stageAt + 1] : "complete";
            Application.runInBackground = true;
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
            IHabitatClusters clusters = GameServices.Get<IHabitatClusters>();
            IHabitatSystem habitat = GameServices.Get<IHabitatSystem>();
            INpcRecruiter npcs = GameServices.Get<INpcRecruiter>();
            if (interior == null || placement == null || clusters == null || npcs == null)
            {
                UnityEngine.Debug.LogError("HABITAT DEMO: 필요한 서비스가 등록되지 않았습니다(IHubInterior/IItemPlacement/IHabitatClusters/INpcRecruiter).");
                return;
            }
            IReadOnlyList<IInteriorCell> cells = interior.Cells(hubId);
            Placement[] placements = _stage == STAGE_INCOMPLETE ? _settings._incompletePlacements : _settings._placements;
            UnityEngine.Debug.Log($"HABITAT DEMO: 단계 {_stage}, 배치 {placements?.Length ?? 0}개");
            foreach (Placement p in placements ?? Array.Empty<Placement>())
            {
                IInteriorCell cell = cells.FirstOrDefault(c => c.IsUpper == p._upper && c.Row == p._row && c.Column == p._column);
                bool ok = cell != null && placement.TryPlace(hubId, cell.Index, p._itemId);
                UnityEngine.Debug.Log($"HABITAT DEMO: 배치 {p._itemId} → {(p._upper ? "위" : "아래")}({p._row},{p._column}) 칸 {cell?.Index} = {ok}");
            }
            string done = string.Join(", ", clusters.CompletedAt(hubId).Select(c => c.TypeId + "[" + string.Join(",", c.Cells) + "]"));
            UnityEngine.Debug.Log($"HABITAT DEMO {hubId}: 완성 서식지 {done} complete={habitat?.IsHabitatComplete(hubId)} flag={habitat?.IsEventFlagSet(hubId)}");

            // 스토리 023: 합류는 승객 하차로만 일어나므로, 이 거점에서 내릴 수 있는 승객을 먼저 태운다.
            string rider = IronRing.Npc.NpcInstaller.Passengers?.BoardForHub(hubId);
            UnityEngine.Debug.Log($"HABITAT DEMO {hubId}: 승객 탑승 {rider ?? "(없음)"}");
            int before = npcs.NpcsAt(hubId).Count;
            for (int visit = 1; visit <= Math.Max(1, _settings._visitAttempts); visit++)
            {
                npcs.OnHubVisited(hubId);
                if (npcs.NpcsAt(hubId).Count > before)
                {
                    UnityEngine.Debug.Log($"HABITAT DEMO {hubId}: 방문 {visit}회째 NPC 합류 → {string.Join(", ", npcs.NpcsAt(hubId))}");
                    return;
                }
            }
            UnityEngine.Debug.LogWarning($"HABITAT DEMO {hubId}: 방문 {_settings._visitAttempts}회 동안 합류한 NPC가 없습니다.");
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
            GameServices.Ready -= Create;
            if (_instance == this) _instance = null;
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class Settings
        {
            public string _hubId = "hub-1";
            public int _waitFrames = 20;
            public int _visitAttempts = 12;
            public Placement[] _placements;
            public Placement[] _incompletePlacements;
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
