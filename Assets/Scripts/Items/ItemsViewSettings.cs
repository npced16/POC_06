using System;

using UnityEngine;

namespace IronRing.Items
{
    /// <summary>스토리 013의 표현·데모 튜닝 값(Resources/Items/ItemsView.json). 코드에 숫자를 두지 않는다.</summary>
    [Serializable]
    public sealed class ItemsViewSettings
    {
        [SerializeField] private int _itemSortingOrder = 40;
        [SerializeField] private float _itemScale = 0.85f;
        [SerializeField] private float _bannerSeconds = 4f;
        [SerializeField] private string _demoHubId = "hub-1";
        [SerializeField] private int _demoWaitFrames = 20;
        [SerializeField] private string[] _demoCrafts = Array.Empty<string>();
        [SerializeField] private string[] _demoGrants = Array.Empty<string>();
        [SerializeField] private string[] _demoPlacements = Array.Empty<string>();

        /// <summary>칸 위 아이템 스프라이트의 sortingOrder.</summary>
        public int ItemSortingOrder => _itemSortingOrder;
        /// <summary>칸 앵커 기준 아이템 스프라이트 배율.</summary>
        public float ItemScale => _itemScale;
        /// <summary>서식지 완성 배너 표시 시간(실시간 초).</summary>
        public float BannerSeconds => _bannerSeconds;
        /// <summary>--items-demo가 동작할 거점.</summary>
        public string DemoHubId => _demoHubId;
        /// <summary>내부 화면이 열린 뒤 데모가 기다리는 프레임 수.</summary>
        public int DemoWaitFrames => _demoWaitFrames;
        /// <summary>데모가 제작할 아이템.</summary>
        public string[] DemoCrafts => _demoCrafts ?? Array.Empty<string>();
        /// <summary>데모가 보상처럼 바로 지급할 아이템.</summary>
        public string[] DemoGrants => _demoGrants ?? Array.Empty<string>();
        /// <summary>데모가 빈 터에 차례로 놓을 아이템.</summary>
        public string[] DemoPlacements => _demoPlacements ?? Array.Empty<string>();

        /// <summary>Resources/Items/ItemsView.json을 읽는다. 없으면 기본값.</summary>
        public static ItemsViewSettings Load()
        {
            TextAsset asset = UnityEngine.Resources.Load<TextAsset>("Items/ItemsView");
            return asset == null ? new ItemsViewSettings() : JsonUtility.FromJson<ItemsViewSettings>(asset.text) ?? new ItemsViewSettings();
        }
    }
}
