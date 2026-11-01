using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Train.Equipment
{
    /// <summary>
    /// 스토리 034 데이터(Resources/Train/TrainEquipment.json). 열차 칸 수·칸당 그리드(가로×세로), 장비 목록(id·가로 칸 수·효과 수치),
    /// 데모 단계별 배치. 모든 수치는 이 파일에서 조정한다.
    /// </summary>
    [Serializable]
    public sealed class TrainEquipmentData
    {
        /// <summary>Resources 경로(확장자 없음).</summary>
        public const string RESOURCE = "Train/TrainEquipment";

        [SerializeField] private int _cars = 3;
        [SerializeField] private int _columns = 3;
        [SerializeField] private int _rows = 2;
        [SerializeField] private EquipmentDefinition[] _equipment = Array.Empty<EquipmentDefinition>();
        [SerializeField] private DemoLoadout[] _demoLoadouts = Array.Empty<DemoLoadout>();

        /// <summary>열차 칸 수(1 이상).</summary>
        public int Cars => Mathf.Max(1, _cars);
        /// <summary>칸당 가로 칸 수(1 이상).</summary>
        public int Columns => Mathf.Max(1, _columns);
        /// <summary>칸당 세로 칸 수(1 이상).</summary>
        public int Rows => Mathf.Max(1, _rows);
        /// <summary>장비 정의(데이터 순서 = 핫바 순서).</summary>
        public IReadOnlyList<EquipmentDefinition> Equipment => _equipment;

        /// <summary>id의 장비 정의. 없으면 null.</summary>
        public EquipmentDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (EquipmentDefinition definition in _equipment) if (definition.Id == id) return definition;
            return null;
        }

        /// <summary>stage 이름의 데모 배치. 없으면 null.</summary>
        public DemoLoadout Demo(string stage)
        {
            foreach (DemoLoadout demo in _demoLoadouts) if (demo != null && demo.Stage == stage) return demo;
            return null;
        }

        /// <summary>JSON을 읽고 검증한다(빈 id·잘못된 폭·중복 id는 예외).</summary>
        public static TrainEquipmentData Parse(string json)
        {
            TrainEquipmentData data = JsonUtility.FromJson<TrainEquipmentData>(json) ?? throw new ArgumentException("Empty train equipment data.");
            data._equipment = data._equipment ?? Array.Empty<EquipmentDefinition>();
            data._demoLoadouts = data._demoLoadouts ?? Array.Empty<DemoLoadout>();
            var seen = new HashSet<string>();
            foreach (EquipmentDefinition definition in data._equipment)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Id) || definition.Width < 1 || definition.Width > data.Columns)
                    throw new ArgumentException("Invalid train equipment definition.");
                if (!seen.Add(definition.Id)) throw new ArgumentException("Duplicate train equipment id: " + definition.Id);
            }
            return data;
        }

        /// <summary>Resources에서 읽는다. 파일이 없으면 null과 오류 로그.</summary>
        public static TrainEquipmentData Load()
        {
            TextAsset json = Resources.Load<TextAsset>(RESOURCE);
            if (json == null) { Debug.LogError("Missing Story 034 data: Resources/" + RESOURCE + ".json"); return null; }
            return Parse(json.text);
        }
    }

    /// <summary>스토리 034 장비 하나의 정의. 효과는 개수만큼 더해진다.</summary>
    [Serializable]
    public sealed class EquipmentDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private int _width = 1;
        [SerializeField] private float _shotsPerSecond;
        [SerializeField] private int _damage;
        [SerializeField] private int _hull;
        [SerializeField] private float _speedBonus;
        [SerializeField] private float _repairPerSecond;
        [SerializeField] private int _cost;

        /// <summary>장비 id(이름 문자열 키는 "equip_" + id).</summary>
        public string Id => _id;
        /// <summary>가로로 차지하는 칸 수.</summary>
        public int Width => _width;
        /// <summary>자동 사격 초당 발사 수(0 = 무기 아님).</summary>
        public float ShotsPerSecond => _shotsPerSecond;
        /// <summary>한 발 피해.</summary>
        public int Damage => _damage;
        /// <summary>최대 내구도 가산.</summary>
        public int Hull => _hull;
        /// <summary>통과 속도 가산 비율.</summary>
        public float SpeedBonus => _speedBonus;
        /// <summary>습격 중 초당 내구도 회복.</summary>
        public float RepairPerSecond => _repairPerSecond;
        /// <summary>스토리 038. 놓을 때 쓰는 고철(0 = 무료).</summary>
        public int Cost => System.Math.Max(0, _cost);
    }

    /// <summary>스토리 034 데모 단계 하나의 장비 배치.</summary>
    [Serializable]
    public sealed class DemoLoadout
    {
        [SerializeField] private string _stage;
        [SerializeField] private DemoPiece[] _pieces = Array.Empty<DemoPiece>();

        /// <summary>단계 이름(placed·start·mid·win·lose).</summary>
        public string Stage => _stage;
        /// <summary>놓을 장비들(순서대로 놓는다).</summary>
        public IReadOnlyList<DemoPiece> Pieces => _pieces ?? Array.Empty<DemoPiece>();
    }

    /// <summary>스토리 034 데모 배치의 장비 한 개.</summary>
    [Serializable]
    public sealed class DemoPiece
    {
        [SerializeField] private string _id;
        [SerializeField] private int _car;
        [SerializeField] private int _column;
        [SerializeField] private int _row;

        /// <summary>장비 id.</summary>
        public string Id => _id;
        /// <summary>열차 칸.</summary>
        public int Car => _car;
        /// <summary>시작 열.</summary>
        public int Column => _column;
        /// <summary>행.</summary>
        public int Row => _row;
    }
}
