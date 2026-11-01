using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Economy.Demo
{
    /// <summary>
    /// 스토리 041 데모(UI 없이 로그 "PROD: …"). 인자: --production-demo --production-stage build|tick|load (단계 없으면 build).
    ///   build: 거점 내부 화면을 열고(IInteriorNavigation) IItemPlacement로 석탄 야적장(석탄 4) + 보일러실(보일러 3·석탄 1) + 톱니공방(톱니 3·작업대 2)을
    ///          서로 닿지 않는 빈 칸 줄에 놓는다(배치 비용이 잔고에서 빠진다).
    ///   tick:  build 뒤 창고에 _demoSeedGoods를 _demoSeedCount개 넣고 Settlement.Settle을 _demoTicks번 → PROD 줄과 창고 수량.
    ///   load:  tick 뒤 ITransfer로 _demoLoadGoods를 _demoLoadCount개 화물칸에 싣는다.
    /// 칸 줄은 IInteriorAnchors의 월드 위치로 격자를 만들어 찾는다(같은 행에서 연속, 이미 쓴 칸과 상하좌우로 닿지 않음).
    /// </summary>
    public sealed class ProductionDemoDriver : IDisposable
    {
        /// <summary>단계 이름.</summary>
        public const string BUILD = "build", TICK = "tick", LOAD = "load";

        private static readonly string[][] Layouts =
        {
            new[] { "tile_coal", "tile_coal", "tile_coal", "tile_coal" },
            new[] { "tile_boiler", "tile_boiler", "tile_coal", "tile_boiler" },
            new[] { "tile_gear", "tile_gear", "tile_bench", "tile_bench", "tile_gear" },
        };

        private readonly string _stage;
        private readonly EconomySettings _settings;
        private readonly IItemPlacement _placement;
        private readonly IHubInterior _interior;
        private readonly IInteriorAnchors _anchors;
        private readonly IInteriorNavigation _navigation;
        private readonly Warehouse _warehouse;
        private readonly Transfer _transfer;
        private readonly ICargo _cargo;
        private readonly IMoney _money;
        private readonly Ledger _ledger;
        private readonly Settlement _settlement;
        private readonly Production _production;
        private float _wait;
        private bool _done;

        /// <summary>실행 인자에서 단계를 읽는다. --production-demo가 없으면 null.</summary>
        public static string ReadStage()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("--production-demo")) return null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--production-stage") return args[i + 1];
            return BUILD;
        }

        /// <summary>설치자가 만든 서비스를 주입한다.</summary>
        public ProductionDemoDriver(string stage, EconomySettings settings, IItemPlacement placement, IHubInterior interior, IInteriorAnchors anchors,
            IInteriorNavigation navigation, Warehouse warehouse, Transfer transfer, ICargo cargo, IMoney money, Ledger ledger, Settlement settlement, Production production)
        {
            _stage = stage; _settings = settings; _placement = placement; _interior = interior; _anchors = anchors; _navigation = navigation;
            _warehouse = warehouse; _transfer = transfer; _cargo = cargo; _money = money; _ledger = ledger; _settlement = settlement; _production = production;
            Debug.Log($"PROD: demo stage={_stage} balance={_money.Balance}");
        }

        /// <summary>매 프레임: 내부 화면이 열리면 한 번 단계를 실행한다.</summary>
        public void Tick(float dt)
        {
            if (_done) return;
            string hub = _settings.WarehouseHubId;
            IViewMode view = GameServices.Get<IViewMode>();
            if (view == null || !view.InteriorOpen || view.InteriorHubId != hub)
            {
                _wait += dt;
                if (_wait > 0.5f) { _wait = 0f; _navigation?.Open(hub); }
                return;
            }
            _done = true;
            try { Run(hub); }
            catch (Exception e) { Debug.LogError("PROD: demo failed " + e); }
        }

        private void Run(string hub)
        {
            Build(hub);
            if (_stage == BUILD) { Debug.Log($"PROD: demo build done balance={_money.Balance} status={StatusLine()}"); return; }
            if (_warehouse.TryAdd(_settings.DemoSeedGoods, _settings.DemoSeedCount))
                Debug.Log($"PROD: demo seed {_settings.DemoSeedGoods} x{_settings.DemoSeedCount}");
            for (int i = 0; i < _settings.DemoTicks; i++) _settlement.Settle(_ledger.CurrentLap);
            Debug.Log($"PROD: demo tick done warehouse {_production.Describe()} balance={_money.Balance} status={StatusLine()}");
            if (_stage != LOAD) return;
            bool ok = _transfer.TryLoad(_settings.DemoLoadGoods, _settings.DemoLoadCount);
            Debug.Log($"PROD: demo load {_settings.DemoLoadGoods} x{_settings.DemoLoadCount} ok={ok} warehouse={_warehouse.CountOf(_settings.DemoLoadGoods)} cargo={_cargo.CountOf(_settings.DemoLoadGoods)}");
        }

        private string StatusLine()
        {
            IReadOnlyList<HabitatCluster> clusters = GameServices.Get<IHabitatClusters>()?.CompletedAt(_production.HubId) ?? Array.Empty<HabitatCluster>();
            return string.Join(", ", clusters.Select((c, i) => c.TypeId + "=" + _production.Status(i)));
        }

        private void Build(string hub)
        {
            IReadOnlyList<IInteriorCell> cells = _interior.Cells(hub);
            var grid = new Dictionary<Vector2Int, int>();
            var at = new Dictionary<int, Vector2Int>();
            for (int i = 0; i < cells.Count; i++)
            {
                Transform anchor = _anchors?.CellAnchor(hub, i);
                if (anchor == null) continue;
                var g = new Vector2Int(Mathf.RoundToInt(anchor.position.x * 100f), Mathf.RoundToInt(anchor.position.y * 100f));
                grid[g] = i; at[i] = g;
            }
            // 칸 간격(가장 작은 x 차이)으로 정수 격자를 만든다.
            int step = at.Values.SelectMany(a => at.Values.Where(b => b.y == a.y && b.x > a.x).Select(b => b.x - a.x)).DefaultIfEmpty(100).Min();
            int stepY = at.Values.SelectMany(a => at.Values.Where(b => b.x == a.x && b.y > a.y).Select(b => b.y - a.y)).DefaultIfEmpty(100).Min();
            var used = new HashSet<int>();
            foreach (string[] layout in Layouts)
            {
                List<int> strip = FindStrip(cells, at, grid, step, stepY, layout.Length, used);
                if (strip == null) { Debug.LogWarning($"PROD: demo no room for {layout[0]} x{layout.Length}"); continue; }
                for (int k = 0; k < strip.Count; k++)
                {
                    bool ok = _placement.TryPlace(hub, strip[k], layout[k]);
                    used.Add(strip[k]);
                    if (!ok) Debug.LogWarning($"PROD: demo place {layout[k]} @{strip[k]} refused balance={_money.Balance}");
                }
                Debug.Log($"PROD: demo placed {string.Join(",", layout)} @ {string.Join(",", strip)} balance={_money.Balance}");
            }
        }

        private static List<int> FindStrip(IReadOnlyList<IInteriorCell> cells, Dictionary<int, Vector2Int> at, Dictionary<Vector2Int, int> grid,
            int step, int stepY, int length, HashSet<int> used)
        {
            bool Free(int i) => cells[i].State == InteriorCellState.Empty && !used.Contains(i);
            bool Touches(int i)
            {
                Vector2Int p = at[i];
                foreach (Vector2Int d in new[] { new Vector2Int(step, 0), new Vector2Int(-step, 0), new Vector2Int(0, stepY), new Vector2Int(0, -stepY) })
                    if (grid.TryGetValue(p + d, out int n) && used.Contains(n)) return true;
                return false;
            }
            foreach (int start in at.Keys.OrderByDescending(i => at[i].y).ThenBy(i => at[i].x))
            {
                var strip = new List<int>();
                for (int k = 0; k < length; k++)
                {
                    if (!grid.TryGetValue(at[start] + new Vector2Int(step * k, 0), out int c) || !Free(c) || Touches(c)) { strip = null; break; }
                    strip.Add(c);
                }
                if (strip != null) return strip;
            }
            return null;
        }

        /// <summary>아무것도 구독하지 않는다.</summary>
        public void Dispose() { _done = true; }
    }
}
