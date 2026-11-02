using System;
using System.Collections.Generic;
using System.Linq;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Track.Demo
{
    /// <summary>
    /// 스토리 051 데모. 실행 인자 `--loop-demo`(브리지 캡처 extraArgs도 읽는다). LoopInstaller가 Ready에서 만든다.
    /// 건설(RestEvents.json _demoBuild 첫 완성, 050 HabitatCodex.MarkBuilt) → 출발 → 휴식지마다 _demoQueue 순서 사건, _demoChooseSeconds 뒤
    /// 고를 수 있는 첫 선택지 자동 선택, _demoContinueSeconds 뒤 Continue → 거점 복귀(건설 바퀴 2) → 재고 _demoPlaceItem을 거점 빈 칸에 배치.
    /// 로그: "LOOP: demo …", "LOOP: place …", 끝에 "LOOP: demo done".
    /// </summary>
    public sealed class LoopDemoDriver
    {
        private const string FLAG = "--loop-demo";
        private const float START_DELAY = 1.5f;
        private const float DEPART_DELAY = 1f;
        private const float PLACE_DELAY = 1.5f;

        private readonly PhaseLoop _loop;
        private readonly ITileStock _stock;
        private readonly ITrackRuntime _track;
        private float _wait;
        private int _state;
        private bool _done;

        /// <summary>--loop-demo가 실행 인자나 진행 중인 브리지 캡처 extraArgs에 있는지.</summary>
        public static bool IsRequested() => DemoArguments().Contains(FLAG);

        /// <summary>설치자가 만든 루프·재고·선로를 주입한다.</summary>
        public LoopDemoDriver(PhaseLoop loop, ITileStock stock, ITrackRuntime track)
        {
            _loop = loop ?? throw new ArgumentNullException(nameof(loop));
            _stock = stock ?? throw new ArgumentNullException(nameof(stock));
            _track = track ?? throw new ArgumentNullException(nameof(track));
            _loop.EventStarted += ResetWait;
            _loop.EventResolved += ResetWait;
            UnityEngine.Application.runInBackground = true;
            UnityEngine.Debug.Log($"LOOP: demo start queue=[{string.Join(",", _loop.Data._demoQueue ?? Array.Empty<string>())}]");
        }

        private void ResetWait() => _wait = 0f;

        /// <summary>매 프레임.</summary>
        public void Tick(float dt)
        {
            if (_done) return;
            _wait += Math.Max(0f, dt);
            switch (_state)
            {
                case 0:
                    if (_wait < START_DELAY) return;
                    Build();
                    foreach (string id in _loop.Data._demoQueue ?? Array.Empty<string>()) _loop.ForceNext(id);
                    _state = 1; _wait = 0f;
                    return;
                case 1:
                    if (_wait < DEPART_DELAY || _loop.Phase != LoopPhase.Build || !_loop.CanDepart) return;
                    UnityEngine.Debug.Log($"LOOP: demo depart lap={_loop.Lap}");
                    _loop.Depart();
                    _state = 2; _wait = 0f;
                    return;
                case 2:
                    Circulate();
                    return;
                case 3:
                    if (_wait < PLACE_DELAY) return;
                    Place();
                    Finish();
                    return;
            }
        }

        private void Build()
        {
            string typeId = _loop.Data._demoBuild;
            IronRing.Habitats.HabitatCodex codex = IronRing.Habitats.HabitatsInstaller.Codex;
            if (string.IsNullOrEmpty(typeId) || codex == null) { UnityEngine.Debug.LogWarning("LOOP: demo build skipped (도감 없음)"); return; }
            bool first = codex.MarkBuilt(typeId);
            UnityEngine.Debug.Log($"LOOP: demo build {typeId} first={first} tiles=[{string.Join(",", codex.UnlockedTileIds)}]");
        }

        private void Circulate()
        {
            if (_loop.IsActive)
            {
                if (!_loop.HasResolved)
                {
                    if (_wait >= _loop.Data._demoChooseSeconds) _loop.Choose(_loop.FirstAvailable());
                }
                else if (_wait >= _loop.Data._demoContinueSeconds) _loop.Continue();
                return;
            }
            if (_loop.Phase == LoopPhase.Build && _loop.Lap >= 2)
            {
                UnityEngine.Debug.Log($"LOOP: demo returned lap={_loop.Lap} stock=[{string.Join(",", _stock.Snapshot().Select(kv => kv.Key + "=" + kv.Value))}]");
                GameServices.Get<IInteriorNavigation>()?.Open(_loop.BaseHubId);
                _state = 3; _wait = 0f;
                return;
            }
            if (_loop.Phase == LoopPhase.Circulate && _loop.CanDepart) _loop.Depart(); // 열린 다른 마을 정차
        }

        private void Place()
        {
            string hub = _loop.BaseHubId;
            string item = _loop.Data._demoPlaceItem;
            IHubInterior interior = GameServices.Get<IHubInterior>();
            IItemPlacement placement = GameServices.Get<IItemPlacement>();
            IMoney money = GameServices.Get<IMoney>();
            if (interior == null || placement == null || string.IsNullOrEmpty(item)) { UnityEngine.Debug.LogWarning("LOOP: place skipped (서비스 없음)"); return; }
            IReadOnlyList<IInteriorCell> cells = interior.Cells(hub);
            int before = _stock.CountOf(item);
            int moneyBefore = money?.Balance ?? 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].State != InteriorCellState.Empty || !placement.CanPlace(hub, i, item)) continue;
                if (!placement.TryPlace(hub, i, item)) continue;
                bool fromStock = (placement as IronRing.Items.ItemPlacement)?.IsStockPaid(hub, i) ?? false;
                UnityEngine.Debug.Log($"LOOP: place {item} cell={i} fromStock={fromStock} stock {before}->{_stock.CountOf(item)} money {moneyBefore}->{money?.Balance ?? 0}");
                return;
            }
            UnityEngine.Debug.LogWarning($"LOOP: place none — {hub}에 {item}을 놓을 빈 칸이 없습니다(stock {before}).");
        }

        private void Finish()
        {
            _done = true;
            _loop.EventStarted -= ResetWait;
            _loop.EventResolved -= ResetWait;
            UnityEngine.Debug.Log("LOOP: demo done");
        }

        /// <summary>실행 인자와 진행 중인 브리지 캡처의 extraArgs를 합친다(캡처가 끝난 뒤의 값은 무시).</summary>
        private static string[] DemoArguments()
        {
            string extra = "";
#if UNITY_EDITOR
            string phase = UnityEditor.SessionState.GetString("IronRing.Bridge.Phase", "");
            if (phase == "enter-play" || phase == "capture")
                extra = UnityEditor.SessionState.GetString("IronRing.Bridge.Capture.ExtraArgs", "");
#endif
            return Environment.GetCommandLineArgs().Concat(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        }
    }
}
