using System;

using UnityEngine;

using IronRing.Presentation.Interior;

namespace IronRing.Hud.Hotbar
{
    /// <summary>스토리 015. Resources/Hotbar/Hotbar.json: 핫바 기본 배정, 팔레트의 시설 목록, 드래그·문구 시간, 캡처 데모 칸.</summary>
    [Serializable]
    public sealed class HotbarData
    {
        [SerializeField] private SlotData[] _slots = Array.Empty<SlotData>();
        [SerializeField] private bool _fillMissingFromCatalog = true;
        [SerializeField] private string[] _facilities = Array.Empty<string>();
        [SerializeField] private float _dragThresholdPixels = 8f;
        [SerializeField] private float _toastSeconds = 2.5f;
        [SerializeField] private float _noticeSeconds = 1.8f;
        [SerializeField] private int _sortingOrder = 120;
        [SerializeField] private DemoData _demo = new DemoData();

        public SlotData[] Slots => _slots ?? Array.Empty<SlotData>();
        /// <summary>데이터 id가 카탈로그에 없으면 카탈로그의 남은 항목으로 채울지.</summary>
        public bool FillMissingFromCatalog => _fillMissingFromCatalog;
        public string[] Facilities => _facilities ?? Array.Empty<string>();
        public float DragThresholdPixels => Mathf.Max(1f, _dragThresholdPixels);
        public float ToastSeconds => _toastSeconds;
        public float NoticeSeconds => _noticeSeconds;
        public int SortingOrder => _sortingOrder;
        public DemoData Demo => _demo ?? new DemoData();

        /// <summary>Resources/Hotbar/Hotbar.json을 읽는다. 없으면 예외.</summary>
        public static HotbarData Load()
        {
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>("Hotbar/Hotbar");
            if (asset == null) throw new InvalidOperationException("Missing hotbar data: Resources/Hotbar/Hotbar.json");
            return JsonUtility.FromJson<HotbarData>(asset.text);
        }

        [Serializable]
        public sealed class SlotData
        {
            [SerializeField] private string _kind;
            [SerializeField] private string _id;
            /// <summary>"facility"면 시설, 그 밖은 아이템(타일 포함).</summary>
            public PlacementEntry ToEntry() => new PlacementEntry(_kind == "facility" ? PlacementKind.Facility : PlacementKind.Item, _id);
        }

        [Serializable]
        public sealed class CellRef
        {
            [SerializeField] private bool _upper;
            [SerializeField] private int _row;
            [SerializeField] private int _column;
            public bool Upper => _upper;
            public int Row => _row;
            public int Column => _column;
        }

        [Serializable]
        public sealed class DemoData
        {
            [SerializeField] private int _waitFrames = 20;
            [SerializeField] private CellRef _ghostCell = new CellRef();
            [SerializeField] private CellRef _blockedCell = new CellRef();
            [SerializeField] private CellRef _dragCell = new CellRef();
            [SerializeField] private int _dragSlot = 1;
            [SerializeField] private CellRef[] _lCells = Array.Empty<CellRef>();
            public int WaitFrames => _waitFrames;
            public CellRef GhostCell => _ghostCell;
            public CellRef BlockedCell => _blockedCell;
            public CellRef DragCell => _dragCell;
            public int DragSlot => _dragSlot;
            public CellRef[] LCells => _lCells ?? Array.Empty<CellRef>();
        }
    }
}
