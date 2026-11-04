using System.Collections.Generic;

using UnityEngine;

using TMPro;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Items;
using IronRing.Presentation.Interior;

namespace IronRing.Presentation.Items
{
    /// <summary>
    /// 스토리 013. 내부 화면에서 배치된 아이템을 칸 위에 보여 준다(순수 연출, 코드로 렌더러를 만들지 않는다).
    /// ItemPlaced: 칸(IInteriorAnchors.CellAnchor)의 InteriorCellView.ItemSlot 아래에 012 카탈로그의 ItemPiece 프리팹을 인스턴스화하고
    /// 스프라이트만 아이템 카탈로그(ItemSpriteSet)에서 바꾼다. 카탈로그는 012와 같은 경로(씬 HubInteriorRoot의 직렬화 참조)로 얻는다.
    /// ItemRemoved: 그 인스턴스를 파괴한다.
    /// </summary>
    public sealed class ItemCellView : MonoBehaviour
    {
        private static ItemCellView _instance;
        private IItemPlacement _placement;
        private IInteriorAnchors _anchors;
        private ItemSpriteSet _sprites;
        private bool _warned;
        private readonly Dictionary<(string, int), GameObject> _shown = new Dictionary<(string, int), GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { GameServices.Ready -= Create; _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        private static void Create()
        {
            if (_instance != null || GameServices.Get<IItemPlacement>() == null) return;
            _instance = new GameObject(nameof(ItemCellView)).AddComponent<ItemCellView>();
            _instance.Initialize();
        }

        private void Initialize()
        {
            _placement = GameServices.Get<IItemPlacement>();
            _anchors = GameServices.Get<IInteriorAnchors>();
            _sprites = ItemSpriteSet.Load();
            if (_anchors == null) Warn("ItemCellView: IInteriorAnchors가 없어 칸 위 아이템 표시를 건너뜁니다.");
            if (_sprites == null) Warn("ItemCellView: Resources/Items/ItemSprites가 없습니다. SpriteGenItems.Generate를 실행하세요.");
            _placement.ItemPlaced += OnPlaced;
            _placement.ItemRemoved += OnRemoved;
        }

        private void OnPlaced(string hubId, int cellIndex, string itemId)
        {
            if (_anchors == null) _anchors = GameServices.Get<IInteriorAnchors>();
            Transform anchor = _anchors?.CellAnchor(hubId, cellIndex);
            if (anchor == null) { Warn($"ItemCellView: {hubId} 칸 {cellIndex}의 앵커가 없습니다."); return; }
            // UnityEngine.Object의 가짜 null 때문에 ?./??를 쓰지 않고 명시적으로 검사한다.
            HubInteriorRoot root = anchor.GetComponentInParent<HubInteriorRoot>();
            InteriorCatalog catalog = root != null ? root.Catalog : null;
            GameObject prefab = catalog != null ? catalog.ItemPiecePrefab : null;
            if (prefab == null) { Warn($"ItemCellView: {hubId} 내부의 InteriorCatalog에 ItemPiece 프리팹이 없습니다."); return; }
            InteriorCellView cell = anchor.GetComponent<InteriorCellView>();
            Transform slot = cell != null ? cell.ItemSlot : anchor;
            OnRemoved(hubId, cellIndex, itemId);
            GameObject piece = Instantiate(prefab, slot, false);
            piece.name = "ItemPiece_" + itemId;
            SpriteRenderer renderer = piece.GetComponentInChildren<SpriteRenderer>(true);
            ItemDefinition definition = ItemsInstaller.Catalog?.Get(itemId);
            bool textTile = definition != null && definition.IsTile;
            TextMeshPro label = piece.GetComponentInChildren<TextMeshPro>(true);
            if (label != null)
            {
                label.gameObject.SetActive(textTile);
                if (textTile)
                {
                    label.text = definition.Name;
                    label.ForceMeshUpdate();
                }
            }
            Sprite sprite = _sprites != null ? _sprites.Get(itemId) : null;
            if (renderer != null) { renderer.sprite = sprite; renderer.enabled = !textTile && sprite != null; }
            if (!textTile && sprite == null) Warn($"ItemCellView: {itemId} 조각에 스프라이트를 넣지 못했습니다.");
            _shown[(hubId, cellIndex)] = piece;
        }

        private void OnRemoved(string hubId, int cellIndex, string itemId)
        {
            if (!_shown.TryGetValue((hubId, cellIndex), out GameObject piece)) return;
            _shown.Remove((hubId, cellIndex));
            if (piece != null) Destroy(piece);
        }

        private void Warn(string message)
        {
            if (_warned) return;
            _warned = true;
            UnityEngine.Debug.LogWarning(message);
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_placement != null) { _placement.ItemPlaced -= OnPlaced; _placement.ItemRemoved -= OnRemoved; }
            if (_instance == this) _instance = null;
        }
    }
}
