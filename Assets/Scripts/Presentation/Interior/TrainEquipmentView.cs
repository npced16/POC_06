using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Train.Equipment;

namespace IronRing.Presentation.Interior
{
    /// <summary>
    /// 스토리 034. 거점 내부 루트(HubInteriorRoot)에 붙는 열차 장비 표현. 씬의 TrainCar 아래 TrainCarGrid 인스턴스(칸 순번별)를 찾아
    /// ITrainLoadout 상태대로 EquipmentPiece 프리팹을 만들고 지운다(장비는 늘 보이고, 칸 틀은 장비 모드에서만).
    /// 열린 거점의 뷰가 T 키(모드 전환)·Esc(선택이 없을 때 모드 종료)를 받고, 장비 모드 동안 PlacementGhost의 배치 면(IPlacementSurface)이 된다.
    /// </summary>
    public sealed class TrainEquipmentView : MonoBehaviour, IPlacementSurface
    {
        private HubInteriorRoot _root;
        private ITrainLoadout _loadout;
        private IViewMode _view;
        private TrainEquipmentCatalog _catalog;
        private TrainCarGridView[] _grids;
        private readonly Dictionary<int, GameObject> _pieces = new Dictionary<int, GameObject>();
        private bool _hadSelection;

        private IEnumerator Start()
        {
            while (!GameServices.IsReady) yield return null;
            _root = GetComponent<HubInteriorRoot>();
            _loadout = GameServices.Get<ITrainLoadout>();
            _view = GameServices.Get<IViewMode>();
            _catalog = TrainEquipmentCatalog.Load();
            if (_root == null || _loadout == null || _view == null || _catalog == null)
            {
                Debug.LogWarning($"TrainEquipmentView({name}): 서비스·카탈로그가 없어 꺼집니다(TrainEquipmentBuild.Generate 실행 필요할 수 있음).");
                enabled = false;
                yield break;
            }
            _grids = new TrainCarGridView[_loadout.Cars];
            foreach (TrainCarGridView grid in GetComponentsInChildren<TrainCarGridView>(true))
                if (grid.Car >= 0 && grid.Car < _grids.Length) _grids[grid.Car] = grid;
            foreach (EquipmentSlot slot in _loadout.Slots) if (slot.IsAnchor) Spawn(slot);
            _loadout.EquipmentPlaced += Spawn;
            _loadout.EquipmentRemoved += Despawn;
            TrainEquipmentMode.Changed += OnModeChanged;
            _view.InteriorChanged += OnInteriorChanged;
            RefreshFrames();
        }

        private void OnDestroy()
        {
            if (_loadout != null) { _loadout.EquipmentPlaced -= Spawn; _loadout.EquipmentRemoved -= Despawn; }
            TrainEquipmentMode.Changed -= OnModeChanged;
            if (_view != null) _view.InteriorChanged -= OnInteriorChanged;
        }

        /// <summary>이 거점 내부가 지금 열려 있는지.</summary>
        public bool IsCurrent => _view != null && _root != null && _view.InteriorOpen && _view.InteriorHubId == _root.HubId;

        private void Update()
        {
            if (!IsCurrent) return;
            PlacementGhost ghost = PlacementGhost.Instance;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame) TrainEquipmentMode.Toggle();
            else if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && TrainEquipmentMode.Active && !_hadSelection) TrainEquipmentMode.Set(false);
            if (ghost != null)
            {
                if (TrainEquipmentMode.Active && !ReferenceEquals(ghost.Surface, this)) ghost.SetSurface(this);
                else if (!TrainEquipmentMode.Active && ReferenceEquals(ghost.Surface, this)) ghost.SetSurface(null);
            }
        }

        private void LateUpdate()
        {
            PlacementGhost ghost = PlacementGhost.Instance;
            _hadSelection = ghost != null && (ghost.Selected.IsValid || ghost.IsDragging);
        }

        private void OnModeChanged(bool active) => RefreshFrames();

        private void OnInteriorChanged(bool open)
        {
            if (!open) TrainEquipmentMode.Set(false);
            RefreshFrames();
        }

        private void RefreshFrames()
        {
            bool show = TrainEquipmentMode.Active && IsCurrent;
            if (_grids == null) return;
            foreach (TrainCarGridView grid in _grids) if (grid != null) grid.SetFramesVisible(show);
        }

        // ----- 조각 -----

        private void Spawn(EquipmentSlot slot)
        {
            int key = Key(slot.Car, slot.Column, slot.Row);
            Despawn(slot);
            TrainCarGridView grid = Grid(slot.Car);
            if (grid == null || _catalog.PiecePrefab == null) return;
            GameObject piece = Instantiate(_catalog.PiecePrefab, grid.Pieces, false);
            piece.name = $"Equip_{slot.EquipmentId}_{slot.Column}{slot.Row}";
            piece.transform.localPosition = TrainCarGridView.LocalCenter(slot.Column, slot.Row, slot.Width, _loadout.Columns, _loadout.Rows, _catalog.CellSize);
            piece.transform.localRotation = Quaternion.identity;
            SpriteRenderer renderer = piece.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null)
            {
                renderer.sprite = _catalog.Icon(slot.EquipmentId);
                renderer.sortingOrder = _catalog.PieceOrder;
                piece.transform.localScale = Vector3.one * Scale(renderer.sprite, slot.Width, _catalog.PieceFill);
            }
            _pieces[key] = piece;
        }

        private void Despawn(EquipmentSlot slot)
        {
            int key = Key(slot.Car, slot.Column, slot.Row);
            if (_pieces.TryGetValue(key, out GameObject piece) && piece != null) Destroy(piece);
            _pieces.Remove(key);
        }

        private float Scale(Sprite sprite, int width, float fill)
        {
            float units = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.x) : 1f;
            return _catalog.CellSize * Mathf.Max(1, width) * fill / units;
        }

        private TrainCarGridView Grid(int car) => _grids != null && car >= 0 && car < _grids.Length ? _grids[car] : null;

        private int Key(int car, int column, int row) => (car * _loadout.Rows + row) * _loadout.Columns + column;

        private bool Decode(int key, out int car, out int column, out int row)
        {
            car = column = row = -1;
            if (_loadout == null || key < 0 || key >= _loadout.Slots.Count) return false;
            EquipmentSlot slot = _loadout.Slots[key];
            car = slot.Car; column = slot.Column; row = slot.Row;
            return true;
        }

        private int Width(PlacementEntry entry)
        {
            TrainLoadout loadout = TrainEquipmentInstaller.Loadout;
            EquipmentDefinition definition = entry.IsValid && loadout != null ? loadout.Data.Get(entry.Id) : null;
            return definition != null ? definition.Width : 1;
        }

        // ----- IPlacementSurface -----

        /// <inheritdoc />
        public int Nearest(Vector3 world, PlacementEntry entry, out Vector3 snapped)
        {
            snapped = world;
            if (_grids == null) return -1;
            int width = Width(entry);
            float radius = _catalog.CellSize * _catalog.SnapRadius * Mathf.Max(1, width);
            float best = radius * radius;
            int found = -1;
            foreach (TrainCarGridView grid in _grids)
            {
                if (grid == null) continue;
                for (int row = 0; row < _loadout.Rows; row++)
                    for (int column = 0; column + width <= _loadout.Columns; column++)
                    {
                        Vector3 center = grid.transform.TransformPoint(TrainCarGridView.LocalCenter(column, row, width, _loadout.Columns, _loadout.Rows, _catalog.CellSize));
                        float distance = ((Vector2)(center - world)).sqrMagnitude;
                        if (distance > best) continue;
                        best = distance; found = Key(grid.Car, column, row); snapped = center;
                    }
            }
            return found;
        }

        /// <inheritdoc />
        public Vector3 Anchor(int key, PlacementEntry entry)
        {
            if (!Decode(key, out int car, out int column, out int row) || Grid(car) == null) return transform.position;
            return Grid(car).transform.TransformPoint(TrainCarGridView.LocalCenter(column, row, Width(entry), _loadout.Columns, _loadout.Rows, _catalog.CellSize));
        }

        /// <inheritdoc />
        public bool CanPlace(int key, PlacementEntry entry)
        {
            return entry.IsValid && entry.Kind == PlacementKind.Equipment && Decode(key, out int car, out int column, out int row)
                && _loadout.CanPlace(car, column, row, entry.Id);
        }

        /// <inheritdoc />
        public bool TryPlace(int key, PlacementEntry entry)
        {
            if (!CanPlace(key, entry) || !Decode(key, out int car, out int column, out int row)) return false;
            if (!_loadout.TryPlace(car, column, row, entry.Id)) return false;
            TrainEquipmentMode.Record(true, _loadout.Slots[key], entry.Id);
            return true;
        }

        /// <inheritdoc />
        public bool TryRemove(int key)
        {
            if (!Decode(key, out int car, out int column, out int row)) return false;
            EquipmentSlot slot = _loadout.Slots[key];
            if (slot.EquipmentId == null) return false;
            string id = slot.EquipmentId;
            int start = column;
            while (start > 0 && !_loadout.Slots[Key(car, start, row)].IsAnchor) start--;
            if (!_loadout.TryRemove(car, column, row)) return false;
            TrainEquipmentMode.Record(false, _loadout.Slots[Key(car, start, row)], id);
            return true;
        }

        /// <inheritdoc />
        public bool TryUndo() => TrainEquipmentMode.Undo(_loadout);

        /// <inheritdoc />
        public float GhostScale(PlacementEntry entry) => Scale(_catalog.Icon(entry.Id), Width(entry), _catalog.PieceFill);

        /// <inheritdoc />
        public int GhostOrder => _catalog != null ? _catalog.GhostOrder : 23;
    }
}
