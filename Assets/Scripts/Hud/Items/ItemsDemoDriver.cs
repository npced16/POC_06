using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Items;

namespace IronRing.Hud.Items
{
    /// <summary>
    /// 스토리 013 캡처용 스크립트 데모(--items-demo). 브리지 캡처 중에만 읽는다(MainRoot의 --integration-demo와 같은 SessionState 방식).
    /// 내부 화면이 데모 거점(데이터, 기본 hub-1)으로 열리면: 두 아이템 제작 → (단계 craft면 끝)
    /// → 보상 아이템 지급 → 빈 터 확보(조사) → 세 아이템 배치 → 서식지 완성. 단계 place는 마지막 칸을 로그로 남긴다(옛 인벤토리 HUD 메뉴·배너는 스토리 022에서 제거).
    /// 실제 시스템 API만 호출하며 규칙을 우회하지 않는다. 자원이 모자라면 데모 지급으로 채우고 로그를 남긴다.
    /// </summary>
    public sealed class ItemsDemoDriver : MonoBehaviour
    {
        private static ItemsDemoDriver _instance;
        private ItemsViewSettings _settings;
        private string _stage;
        private int _waited;
        private bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        private static void Create()
        {
            if (_instance != null) return;
            string[] args = Arguments();
            if (!args.Contains("--items-demo")) return;
            _instance = new GameObject(nameof(ItemsDemoDriver)).AddComponent<ItemsDemoDriver>();
            _instance._settings = ItemsViewSettings.Load();
            _instance._stage = Argument(args, "--items-demo-stage") ?? "complete";
            Application.runInBackground = true;
        }

        void Update()
        {
            if (_done) return;
            IViewMode view = GameServices.Get<IViewMode>();
            if (view == null || !view.InteriorOpen || view.InteriorHubId != _settings.DemoHubId) return;
            if (_waited++ < _settings.DemoWaitFrames) return;
            _done = true;
            try { Run(_settings.DemoHubId); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }

        private void Run(string hubId)
        {
            IItemInventory items = GameServices.Get<IItemInventory>();
            IItemPlacement placement = GameServices.Get<IItemPlacement>();
            IHubInterior interior = GameServices.Get<IHubInterior>();
            IResourceInventory resources = GameServices.Get<IResourceInventory>();
            IHabitatSystem habitat = GameServices.Get<IHabitatSystem>();
            ItemCatalog catalog = ItemsInstaller.Catalog;

            foreach (string id in _settings.DemoCrafts)
            {
                ItemDefinition item = catalog.Get(id);
                foreach (var cost in item.Cost)
                {
                    int missing = cost.Value - resources.Get(cost.Key);
                    if (missing > 0) { resources.Add(cost.Key, missing); UnityEngine.Debug.Log($"ITEMS DEMO: 자원 데모 지급 {cost.Key} +{missing}"); }
                }
                UnityEngine.Debug.Log($"ITEMS DEMO: 제작 {id} = {items.TryCraft(id)}");
            }
            if (_stage == "craft") { Log(hubId, items, habitat); return; } // 스토리 022: 옛 인벤토리 HUD 제거, 제작 메뉴 화면은 없다

            foreach (string id in _settings.DemoGrants) items.Add(id, 1);
            List<int> empty = EnsureEmptyCells(interior, hubId, _settings.DemoPlacements.Length);
            int last = -1;
            for (int i = 0; i < _settings.DemoPlacements.Length && i < empty.Count; i++)
            {
                bool ok = placement.TryPlace(hubId, empty[i], _settings.DemoPlacements[i]);
                UnityEngine.Debug.Log($"ITEMS DEMO: 배치 {_settings.DemoPlacements[i]} → 칸 {empty[i]} = {ok}");
                if (ok) last = empty[i];
            }
            if (_stage == "place") UnityEngine.Debug.Log($"ITEMS DEMO: 마지막 배치 칸 {last}(옛 회수 메뉴는 스토리 022에서 제거)");
            Log(hubId, items, habitat);
        }

        /// <summary>빈 터가 count개가 될 때까지 실제 조사를 한다. 조사 횟수가 떨어지면 데모 한정으로 OnDocked를 다시 불러 채운다.</summary>
        private static List<int> EnsureEmptyCells(IHubInterior interior, string hubId, int count)
        {
            for (int guard = 0; guard < 200 && EmptyCells(interior, hubId).Count < count; guard++)
            {
                int rubble = -1;
                foreach (int i in TrackFirst(interior, hubId)) if (interior.Cells(hubId)[i].State == InteriorCellState.Rubble) { rubble = i; break; }
                if (rubble < 0) break;
                if (!interior.CanInvestigate(hubId, rubble))
                {
                    UnityEngine.Debug.Log("ITEMS DEMO: 조사 횟수 소진 → 데모 한정 OnDocked로 다시 채움");
                    interior.OnDocked(hubId);
                    if (!interior.CanInvestigate(hubId, rubble)) break;
                }
                InvestigationResult result = interior.Investigate(hubId, rubble, out string found);
                UnityEngine.Debug.Log($"ITEMS DEMO: 조사 칸 {rubble} = {result} {found}");
            }
            return EmptyCells(interior, hubId);
        }

        private static List<int> EmptyCells(IHubInterior interior, string hubId)
        {
            IReadOnlyList<IInteriorCell> cells = interior.Cells(hubId);
            return TrackFirst(interior, hubId).Where(i => cells[i].State == InteriorCellState.Empty).ToList();
        }

        /// <summary>선로에 가까운 줄부터(위 구역은 마지막 줄, 아래 구역은 첫 줄) 칸 인덱스를 돌려준다. 화면 위쪽 HUD에 가려지지 않게 한다.</summary>
        private static List<int> TrackFirst(IHubInterior interior, string hubId)
        {
            IReadOnlyList<IInteriorCell> cells = interior.Cells(hubId);
            int maxRow = cells.Count == 0 ? 0 : cells.Max(c => c.Row);
            return Enumerable.Range(0, cells.Count)
                .OrderBy(i => cells[i].IsUpper ? maxRow - cells[i].Row : cells[i].Row)
                .ThenBy(i => cells[i].IsUpper ? 0 : 1)
                .ThenBy(i => cells[i].Column)
                .ToList();
        }

        private static void Log(string hubId, IItemInventory items, IHabitatSystem habitat)
        {
            string counts = string.Join(", ", items.Snapshot().Select(e => e.Key + "=" + e.Value));
            UnityEngine.Debug.Log($"ITEMS DEMO {hubId}: items[{counts}] complete={habitat?.IsHabitatComplete(hubId)} flag={habitat?.IsEventFlagSet(hubId)}");
        }

        private static string Argument(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
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
    }
}
