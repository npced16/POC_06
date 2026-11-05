using System;
using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Hotbar;
using IronRing.Items;
using IronRing.Presentation.Interior;

namespace IronRing.Onboarding
{
    /// <summary>
    /// 스펙 001 FR-018~FR-021, FR-023. 안내 배치: 지정 칸 3개를 노랗게 깜빡이고 나머지 칸을 회색으로 흐리며,
    /// ItemPlacement.PlacementGate로 지정 칸에 지정 재료만 놓게 한다. 퀵슬롯 새 칸의 틀도 깜빡인다.
    /// OnboardingScreen이 만들고 매 프레임 Tick을 부른다.
    /// </summary>
    public sealed class OnboardingGuide
    {
        private readonly Color _guideColor;
        private readonly Color _dimColor;
        private readonly float _pulseSeconds;
        private readonly Func<Vector3, bool> _isCovered;
        private readonly Dictionary<int, string> _remaining = new();
        private readonly Dictionary<int, string> _pattern = new();
        private readonly Dictionary<SpriteRenderer, Color> _originalColors = new();
        private string[] _pendingPattern;
        private string _hubId;
        private bool _shown;
        private string _slotItemId;
        private HotbarCellView _slotView;
        private Color _slotOriginal;
        private ItemPlacement _placement;
        private IHubInterior _interior;
        private IViewMode _view;
        private HubInteriorView _interiorView;

        public bool IsActive => _remaining.Count > 0;

        /// <summary>guideColor(지정 칸 강조색), dimColor(나머지 칸 흐림색), pulseSeconds(깜빡임 한 주기 초), isCovered(월드 위치가 화면 UI에 가려지는지)를 받아 둔다.</summary>
        public OnboardingGuide(Color guideColor, Color dimColor, float pulseSeconds, Func<Vector3, bool> isCovered)
        {
            _guideColor = guideColor;
            _dimColor = dimColor;
            _pulseSeconds = Mathf.Max(0.1f, pulseSeconds);
            _isCovered = isCovered;
            Active = this;
        }

        /// <summary>마지막으로 만든 안내 배치(OnboardingScreen이 가진 것). 개발자 상황 재현이 칸을 고를 때 쓴다.</summary>
        public static OnboardingGuide Active { get; private set; }

        /// <summary>
        /// hubId 마을에서 안내 칸과 같은 규칙(역에 가까운 줄, 가운데, 이웃 빈 칸, UI에 안 가려짐)으로 가로 빈 칸 length개를 찾는다.
        /// 왼쪽부터 칸 번호 목록을, 없으면 null을 돌려준다.
        /// </summary>
        public List<int> FreeRun(string hubId, int length)
        {
            Resolve();
            return FindRun(hubId, length);
        }

        /// <summary>
        /// pattern(왼쪽부터 칸마다 놓을 재료 id)으로 안내 배치를 예약한다.
        /// 마을 내부가 열려 있으면 다음 Tick에서 칸을 고르고, 아니면 열릴 때까지 기다린다.
        /// </summary>
        public void Begin(params string[] pattern)
        {
            Stop();
            _pendingPattern = pattern;
        }

        /// <summary>itemId 재료가 들어 있는 퀵슬롯 칸의 틀을 깜빡이게 한다(FR-021). null이면 강조를 끈다.</summary>
        public void HighlightSlot(string itemId)
        {
            RestoreSlot();
            _slotItemId = itemId;
        }

        /// <summary>안내 배치를 끝낸다. 칸 색과 놓기 제한을 원래대로 되돌린다.</summary>
        public void Stop()
        {
            _pendingPattern = null;
            RestoreCells();
            _remaining.Clear();
            _pattern.Clear();
            if (_placement != null) _placement.PlacementGate = null;
        }

        /// <summary>
        /// 매 프레임 부른다. 예약된 안내 배치를 시작하고, 지금 고른 재료가 남은 지정 재료이면 지정 칸 강조와 나머지 흐림을 그린다.
        /// </summary>
        public void Tick()
        {
            Resolve();
            if (_pendingPattern != null && _view != null && _view.InteriorOpen) StartPending(_view.InteriorHubId);
            float pulse = Pulse();
            if (!IsActive) return;
            string selected = PlacementGhost.Instance != null && PlacementGhost.Instance.Selected.IsValid ? PlacementGhost.Instance.Selected.Id : null;
            bool show = _view != null && _view.InteriorOpen && _view.InteriorHubId == _hubId && selected != null && _remaining.ContainsValue(selected);
            if (show) PaintCells(pulse);
            else if (_shown) RestoreCells();
        }

        /// <summary>
        /// 매 프레임 퀵슬롯이 칸을 다 그린 뒤(LateUpdate) 부른다. 퀵슬롯 강조 칸의 틀을 깜빡인다(FR-021).
        /// 퀵슬롯은 해금 직후 매 프레임 틀 색을 다시 칠하므로 그 뒤에 덮어써야 강조가 보인다.
        /// </summary>
        public void LateTick() => TickSlot(Pulse());

        /// <summary>_pulseSeconds 주기로 0과 1 사이를 오가는 깜빡임 값을 돌려준다.</summary>
        private float Pulse() => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / _pulseSeconds);

        /// <summary>hubId 마을 cellIndex 지정 칸에 재료가 놓이면 그 칸 강조를 지우고, 3칸이 모두 차면 안내 배치를 끝낸다(FR-020).</summary>
        public void OnItemPlaced(string hubId, int cellIndex, string itemId)
        {
            if (hubId != _hubId || !_remaining.Remove(cellIndex)) return;
            RestoreRenderer(CellView(cellIndex));
            if (!IsActive) Stop();
        }

        /// <summary>안내 배치 중 hubId 마을 cellIndex 지정 칸의 재료가 회수되면 그 칸을 다시 남은 지정 칸으로 되돌린다.</summary>
        public void OnItemRemoved(string hubId, int cellIndex, string itemId)
        {
            if (!IsActive || hubId != _hubId || !_pattern.TryGetValue(cellIndex, out string required)) return;
            _remaining[cellIndex] = required;
        }

        /// <summary>서비스와 씬 뷰를 아직 못 찾았으면 찾아 둔다.</summary>
        private void Resolve()
        {
            if (_placement == null) _placement = GameServices.Get<IItemPlacement>() as ItemPlacement;
            _interior ??= GameServices.Get<IHubInterior>();
            _view ??= GameServices.Get<IViewMode>();
            if (_interiorView == null) _interiorView = UnityEngine.Object.FindAnyObjectByType<HubInteriorView>();
        }

        /// <summary>hubId 마을에서 예약된 패턴의 지정 칸을 고르고 놓기 제한을 건다. 고를 칸이 없으면 로그만 남긴다(FR-023).</summary>
        private void StartPending(string hubId)
        {
            string[] pattern = _pendingPattern;
            _pendingPattern = null;
            List<int> run = FindRun(hubId, pattern.Length);
            if (run == null || _placement == null) { Debug.Log("ONBOARDING: guide no free run hub=" + hubId); return; }
            _hubId = hubId;
            for (int i = 0; i < run.Count; i++) { _remaining[run[i]] = pattern[i]; _pattern[run[i]] = pattern[i]; }
            _placement.PlacementGate = Allows;
            Debug.Log("ONBOARDING: guide cells=" + string.Join(",", run) + " items=" + string.Join(",", pattern));
        }

        /// <summary>안내 배치 중 (hubId, cellIndex)에 itemId를 놓아도 되는지 돌려준다. 지정 칸의 지정 재료만 허락한다(FR-019).</summary>
        private bool Allows(string hubId, int cellIndex, string itemId)
        {
            if (!IsActive) return true;
            return hubId == _hubId && _remaining.TryGetValue(cellIndex, out string required) && required == itemId;
        }

        /// <summary>
        /// hubId 마을에서 가로로 이어진 빈 칸 length개를 찾는다. 역(열차 앵커)에 가까운 줄부터 보고, 줄 안에서는 가운데에 가까운 자리를 고른다.
        /// 고른 칸의 상하좌우 이웃도 모두 비어 있어야 한다(이미 지은 묶음과 섞이지 않게). 왼쪽부터 칸 번호 목록을, 없으면 null을 돌려준다.
        /// </summary>
        private List<int> FindRun(string hubId, int length)
        {
            if (_interior == null || _interiorView == null) return null;
            IReadOnlyList<IInteriorCell> cells = _interior.Cells(hubId);
            if (cells == null || cells.Count == 0) return null;
            var positions = new Dictionary<int, Vector3>();
            for (int i = 0; i < cells.Count; i++)
            {
                Transform anchor = _interiorView.CellAnchor(hubId, i);
                if (anchor != null) positions[i] = anchor.position;
            }
            float size = CellSize(positions);
            if (size <= 0f) return null;
            // 칸 중심이 .5로 끝나면 RoundToInt가 짝수 쪽으로 반올림해 키가 겹치므로, 최소 좌표 기준으로 FloorToInt(+0.5)를 쓴다.
            Vector2 origin = new Vector2(float.MaxValue, float.MaxValue);
            foreach (Vector3 position in positions.Values) origin = Vector2.Min(origin, position);
            var grid = new Dictionary<Vector2Int, int>();
            foreach (KeyValuePair<int, Vector3> pair in positions)
            {
                grid[new Vector2Int(Mathf.FloorToInt((pair.Value.x - origin.x) / size + 0.5f), Mathf.FloorToInt((pair.Value.y - origin.y) / size + 0.5f))] = pair.Key;
            }
            Transform train = _interiorView.TrainAnchor(hubId);
            float trainY = train != null ? (train.position.y - origin.y) / size : float.MinValue;
            var rows = new List<int>();
            foreach (Vector2Int key in grid.Keys) if (!rows.Contains(key.y)) rows.Add(key.y);
            rows.Sort((a, b) => Mathf.Abs(a - trainY).CompareTo(Mathf.Abs(b - trainY)));

            foreach (int row in rows)
            {
                int minX = int.MaxValue, maxX = int.MinValue;
                foreach (Vector2Int key in grid.Keys) if (key.y == row) { minX = Mathf.Min(minX, key.x); maxX = Mathf.Max(maxX, key.x); }
                float middle = (minX + maxX) * 0.5f;
                List<int> best = null;
                float bestDistance = float.MaxValue;
                for (int x = minX; x + length - 1 <= maxX; x++)
                {
                    List<int> run = RunAt(grid, cells, x, row, length);
                    if (run != null && run.Exists(index => Covered(positions[index], size))) run = null;
                    float distance = Mathf.Abs(x + (length - 1) * 0.5f - middle);
                    if (run != null && distance < bestDistance) { best = run; bestDistance = distance; }
                }
                if (best != null) return best;
            }
            return null;
        }

        /// <summary>grid의 (x, row)부터 오른쪽 length칸과 그 상하좌우 이웃이 모두 빈 칸이면 왼쪽부터 칸 번호 목록을, 아니면 null을 돌려준다.</summary>
        private static List<int> RunAt(Dictionary<Vector2Int, int> grid, IReadOnlyList<IInteriorCell> cells, int x, int row, int length)
        {
            var run = new List<int>();
            for (int i = 0; i < length; i++)
            {
                if (!grid.TryGetValue(new Vector2Int(x + i, row), out int index) || cells[index].State != InteriorCellState.Empty) return null;
                run.Add(index);
            }
            for (int i = -1; i <= length; i++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    bool inRun = dy == 0 && i >= 0 && i < length;
                    bool corner = (i == -1 || i == length) && dy != 0;
                    if (inRun || corner) continue;
                    if (grid.TryGetValue(new Vector2Int(x + i, row + dy), out int neighbor) && cells[neighbor].State != InteriorCellState.Empty) return null;
                }
            }
            return run;
        }

        /// <summary>center 칸(한 변 size)의 가운데나 네 모서리 중 하나라도 화면 UI에 가려지면 true를 돌려준다.</summary>
        private bool Covered(Vector3 center, float size)
        {
            if (_isCovered == null) return false;
            float half = size * 0.5f;
            return _isCovered(center) || _isCovered(center + new Vector3(-half, -half)) || _isCovered(center + new Vector3(half, -half))
                || _isCovered(center + new Vector3(-half, half)) || _isCovered(center + new Vector3(half, half));
        }

        /// <summary>positions의 칸 위치들에서 가장 가까운 두 칸 사이 거리(칸 한 변 길이)를 돌려준다. 칸이 하나 이하면 0.</summary>
        private static float CellSize(Dictionary<int, Vector3> positions)
        {
            float size = float.MaxValue;
            var list = new List<Vector3>(positions.Values);
            for (int i = 0; i < list.Count && i < 40; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    float distance = Vector2.Distance(list[i], list[j]);
                    if (distance > 0.0001f && distance < size) size = distance;
                }
            }
            return size == float.MaxValue ? 0f : size;
        }

        /// <summary>pulse(0~1)로 지정 칸은 강조색으로 깜빡이고, 나머지 칸은 흐림색으로 칠한다(FR-018).</summary>
        private void PaintCells(float pulse)
        {
            if (_interior == null) return;
            IReadOnlyList<IInteriorCell> cells = _interior.Cells(_hubId);
            for (int i = 0; i < cells.Count; i++)
            {
                InteriorCellView view = CellView(i);
                if (view == null) continue;
                bool guided = _remaining.ContainsKey(i);
                Color tile = guided ? Color.Lerp(Color.white, _guideColor, 0.6f + 0.4f * pulse) : _dimColor;
                Color line = guided ? Color.Lerp(Color.white, _guideColor, pulse) : _dimColor;
                Tint(view.TileRenderer, tile);
                Tint(view.BuildableMarker, line);
                if (guided && view.BuildableMarker != null) view.BuildableMarker.enabled = true;
            }
            _shown = true;
        }

        /// <summary>칠했던 모든 칸의 색을 원래대로 되돌린다.</summary>
        private void RestoreCells()
        {
            foreach (KeyValuePair<SpriteRenderer, Color> pair in _originalColors) if (pair.Key != null) pair.Key.color = pair.Value;
            _originalColors.Clear();
            _shown = false;
        }

        /// <summary>view 한 칸의 타일·선 색을 원래대로 되돌리고 기억해 둔 색을 지운다.</summary>
        private void RestoreRenderer(InteriorCellView view)
        {
            if (view == null) return;
            foreach (SpriteRenderer renderer in new[] { view.TileRenderer, view.BuildableMarker })
            {
                if (renderer != null && _originalColors.TryGetValue(renderer, out Color color)) { renderer.color = color; _originalColors.Remove(renderer); }
            }
        }

        /// <summary>renderer의 원래 색을 처음 한 번 기억해 두고 color로 칠한다.</summary>
        private void Tint(SpriteRenderer renderer, Color color)
        {
            if (renderer == null) return;
            if (!_originalColors.ContainsKey(renderer)) _originalColors[renderer] = renderer.color;
            renderer.color = color;
        }

        /// <summary>현재 안내 마을의 cellIndex 칸 뷰를 돌려준다. 없으면 null.</summary>
        private InteriorCellView CellView(int cellIndex)
        {
            Transform anchor = _interiorView != null ? _interiorView.CellAnchor(_hubId, cellIndex) : null;
            return anchor != null ? anchor.GetComponent<InteriorCellView>() : null;
        }

        /// <summary>강조할 퀵슬롯 칸을 찾아 pulse(0~1)로 틀 색을 깜빡인다. 재료가 칸에서 빠지면 다시 찾는다.</summary>
        private void TickSlot(float pulse)
        {
            if (_slotItemId == null || HotbarScreen.Instance == null) return;
            if (_slotView == null || _slotView.Entry.Id != _slotItemId)
            {
                RestoreSlot(false);
                foreach (HotbarCellView view in HotbarScreen.Instance.GetComponentsInChildren<HotbarCellView>(true))
                {
                    if (view.Entry.IsValid && view.Entry.Id == _slotItemId && view.Frame != null) { _slotView = view; _slotOriginal = view.Frame.color; break; }
                }
            }
            if (_slotView != null) _slotView.Frame.color = Color.Lerp(_slotOriginal, _guideColor, pulse);
        }

        /// <summary>퀵슬롯 칸 틀을 원래 색으로 되돌린다. forget이면 강조할 재료도 지운다.</summary>
        private void RestoreSlot(bool forget = true)
        {
            if (_slotView != null && _slotView.Frame != null) _slotView.Frame.color = _slotOriginal;
            _slotView = null;
            if (forget) _slotItemId = null;
        }
    }
}
