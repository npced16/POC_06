using System;
using System.Collections.Generic;

using IronRing.Contracts;
using IronRing.Habitats;

namespace IronRing.Shop
{
    /// <summary>
    /// 스펙 010. 녹슨 다리 찢어진 설계도 상점(IBlueprintShop). 순수 C#.
    /// 새 게임과 바퀴 정산 때 미완성·미공개 조건이 남은·전체 단서가 없는 건물 최대 OfferCount종을 추첨해 그 바퀴 동안 유지한다.
    /// 조각 한 장은 그 건물의 아직 모르는 타일 id 하나와 총 필요 수량을 공개하고, 그 상품을 이번 바퀴에 품절시킨다.
    /// 도감·돈·장부·선로 개통은 쓰는 순간에 조회해 설치기 순서에 기대지 않는다.
    /// </summary>
    public sealed class BlueprintShop : IBlueprintShop, ISaveParticipant, IDisposable
    {
        private const string HOLD_REASON = "fragment-shop";
        /// <summary>녹슨 다리 상점을 처음 마칠 때 여는 거점.</summary>
        public const string NEXT_HUB_ID = "hub-2";

        private readonly FragmentShopSettings _settings;
        private readonly HabitatCatalog _catalog;
        private readonly ITrackRuntime _track;
        private readonly Func<IHabitatCodex> _codex;
        private readonly Func<IMoney> _money;
        private readonly Func<ILedger> _ledger;
        private readonly Func<ILineOpener> _line;
        private readonly Func<string, bool> _battleHint;
        private readonly Func<string, string> _text;
        private readonly Random _random;
        private readonly List<string> _offers = new List<string>();
        private readonly HashSet<string> _soldOut = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _revealed = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private int _stockLap;

        /// <inheritdoc />
        public bool IsOpen { get; private set; }
        /// <inheritdoc />
        public IReadOnlyList<string> Offers => _offers;
        /// <summary>지금 진열을 추첨한 바퀴(완료 바퀴 수).</summary>
        public int StockLap => _stockLap;

        /// <inheritdoc />
        public event Action Opened;
        /// <inheritdoc />
        public event Action Changed;
        /// <inheritdoc />
        public event Action Closed;
        /// <inheritdoc />
        public event Action<string, string> Purchased;

        /// <summary>
        /// 설정·카탈로그·선로와 지연 조회 함수를 받아 상점을 만들고 바퀴 정산을 구독한다.
        /// battleHint(id)는 전투에서 그 건물의 전체 단서를 얻었는지, text(key)는 ko.json 문구를 돌려준다. 진열은 Draw를 불러야 생긴다.
        /// </summary>
        public BlueprintShop(FragmentShopSettings settings, HabitatCatalog catalog, ITrackRuntime track, Func<IHabitatCodex> codex,
            Func<IMoney> money, Func<ILedger> ledger, Func<ILineOpener> line, Func<string, bool> battleHint, Func<string, string> text, Random random)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _track = track ?? throw new ArgumentNullException(nameof(track));
            _codex = codex ?? (() => null);
            _money = money ?? (() => null);
            _ledger = ledger ?? (() => null);
            _line = line ?? (() => null);
            _battleHint = battleHint ?? (_ => false);
            _text = text ?? (k => k);
            _random = random ?? new Random();
            _track.LapCompleted += OnLapCompleted;
        }

        /// <summary>
        /// 지금 후보를 섞어 앞의 최대 OfferCount종을 진열하고 품절을 비운다.
        /// 진열 바퀴를 현재 완료 바퀴로 바꾸고 Changed를 낸다.
        /// </summary>
        public void Draw()
        {
            var candidates = new List<string>();
            foreach (HabitatType type in _catalog.Types) if (IsCandidate(type.Id)) candidates.Add(type.Id);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            _offers.Clear();
            for (int i = 0; i < candidates.Count && i < _settings.OfferCount; i++) _offers.Add(candidates[i]);
            _soldOut.Clear();
            _stockLap = _track.Lap;
            UnityEngine.Debug.Log($"SHOP: stock lap={_stockLap} offers={string.Join(",", _offers)} candidates={candidates.Count}");
            Changed?.Invoke();
        }

        /// <summary>바퀴 정산 때 그 바퀴 진열을 아직 만들지 않았으면 한 번만 다시 추첨한다.</summary>
        private void OnLapCompleted(int lap)
        {
            if (lap == _stockLap) return;
            Draw();
        }

        /// <summary>
        /// recipeId가 판매 후보인지 판정한다. 가격이 있고, 직접 완성·도감 등록·전체 단서(도감·전투)가 없고,
        /// 공개하지 않은 타일 id가 남아 있어야 true다. 필요한 타일의 해금 여부는 보지 않는다.
        /// </summary>
        public bool IsCandidate(string recipeId)
        {
            HabitatType type = _catalog.Get(recipeId);
            if (type == null || !_settings.Prices.ContainsKey(recipeId)) return false;
            IHabitatCodex codex = _codex();
            if (codex != null && (codex.HasBuilt(recipeId) || codex.IsUnlocked(recipeId))) return false;
            if (codex is HabitatCodex clues && clues.HasHint(recipeId)) return false;
            if (_battleHint(recipeId)) return false;
            return Hidden(type).Count > 0;
        }

        /// <inheritdoc />
        public int PriceOf(string recipeId) => recipeId != null && _settings.Prices.TryGetValue(recipeId, out int price) ? price : 0;

        /// <inheritdoc />
        public bool IsSoldOut(string recipeId) => recipeId != null && _soldOut.Contains(recipeId);

        /// <inheritdoc />
        public string UnavailableReason(string recipeId)
        {
            if (!_settings.IsValid || !_offers.Contains(recipeId)) return _text("shop_reason_unavailable");
            if (IsSoldOut(recipeId)) return _text("shop_reason_sold_out");
            if (!IsCandidate(recipeId)) return _text("shop_reason_known");
            IMoney money = _money();
            if (money == null || money.Balance < PriceOf(recipeId)) return _text("shop_reason_money");
            return string.Empty;
        }

        /// <inheritdoc />
        public bool TryBuy(string recipeId)
        {
            if (!IsOpen || !_settings.IsValid || recipeId == null || !_offers.Contains(recipeId) || IsSoldOut(recipeId)) return false;
            if (!IsCandidate(recipeId)) return false;
            int price = PriceOf(recipeId);
            IMoney money = _money();
            if (price <= 0 || money == null || !money.TrySpend(price, "fragment:" + recipeId)) return false;
            _ledger()?.Record(LedgerKind.Purchase, -price, recipeId);
            List<string> hidden = Hidden(_catalog.Get(recipeId));
            string tileId = hidden[_random.Next(hidden.Count)];
            if (!_revealed.TryGetValue(recipeId, out List<string> known)) _revealed[recipeId] = known = new List<string>();
            known.Add(tileId);
            _soldOut.Add(recipeId);
            UnityEngine.Debug.Log($"SHOP: buy {recipeId} price={price} reveal={tileId}x{RequiredCountOf(recipeId, tileId)} known={known.Count}/{TileCounts(_catalog.Get(recipeId)).Count} balance={money.Balance}");
            Purchased?.Invoke(recipeId, tileId);
            Changed?.Invoke();
            return true;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> RevealedTiles(string recipeId) =>
            recipeId != null && _revealed.TryGetValue(recipeId, out List<string> known) ? known : (IReadOnlyList<string>)Array.Empty<string>();

        /// <inheritdoc />
        public int RequiredCountOf(string recipeId, string tileId)
        {
            HabitatType type = _catalog.Get(recipeId);
            if (type == null) return 0;
            foreach (KeyValuePair<string, int> pair in TileCounts(type)) if (pair.Key == tileId) return pair.Value;
            return 0;
        }

        /// <inheritdoc />
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _track.RequestHold(HOLD_REASON);
            UnityEngine.Debug.Log($"SHOP: open lap={_track.Lap} offers={string.Join(",", _offers)}");
            Opened?.Invoke();
        }

        /// <inheritdoc />
        public void Continue()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ILineOpener line = _line();
            if (line != null && !line.IsOpen(NEXT_HUB_ID)) line.Open(NEXT_HUB_ID);
            _track.ReleaseHold(HOLD_REASON);
            UnityEngine.Debug.Log("SHOP: continue");
            Closed?.Invoke();
        }

        /// <summary>
        /// type 레시피의 서로 다른 타일 id와 총 필요 수량을 레시피 순서로 돌려준다.
        /// 묶음은 그 타일 하나와 필요 칸 수, 혼합은 타일별 최소 수, 패턴은 템플릿에서 그 타일이 나온 칸 수를 센다.
        /// </summary>
        public static List<KeyValuePair<string, int>> TileCounts(HabitatType type)
        {
            var counts = new List<KeyValuePair<string, int>>();
            if (type == null) return counts;
            if (type.Kind == HabitatKind.Cluster)
            {
                counts.Add(new KeyValuePair<string, int>(type.TileItemId, type.RequiredCount));
                return counts;
            }
            if (type.Kind == HabitatKind.Set)
            {
                foreach (HabitatTileRequirement tile in type.SetTiles) counts.Add(new KeyValuePair<string, int>(tile.TileItemId, tile.MinCount));
                return counts;
            }
            foreach (string tile in type.TileIds())
            {
                int index = counts.FindIndex(pair => pair.Key == tile);
                if (index < 0) counts.Add(new KeyValuePair<string, int>(tile, 1));
                else counts[index] = new KeyValuePair<string, int>(tile, counts[index].Value + 1);
            }
            return counts;
        }

        /// <summary>type 레시피의 타일 id 가운데 아직 조각으로 공개하지 않은 것을 레시피 순서로 돌려준다.</summary>
        private List<string> Hidden(HabitatType type)
        {
            IReadOnlyList<string> known = RevealedTiles(type.Id);
            var hidden = new List<string>();
            foreach (KeyValuePair<string, int> pair in TileCounts(type))
                if (!Contains(known, pair.Key)) hidden.Add(pair.Key);
            return hidden;
        }

        /// <summary>list에 id가 있으면 true를 반환한다.</summary>
        private static bool Contains(IReadOnlyList<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == id) return true;
            return false;
        }

        /// <summary>바퀴 정산 구독을 풀고 이벤트를 비운다. 열려 있으면 열차 붙잡기를 푼다.</summary>
        public void Dispose()
        {
            _track.LapCompleted -= OnLapCompleted;
            if (IsOpen) _track.ReleaseHold(HOLD_REASON);
            IsOpen = false;
            Opened = null;
            Changed = null;
            Closed = null;
            Purchased = null;
        }

        /// <summary>스펙 010. 슬롯 파일 안의 키.</summary>
        public string SaveKey => "fragmentShop";

        /// <summary>스펙 010. 복원 순서(도감 50 다음).</summary>
        public int Order => 51;

        /// <summary>
        /// 진열 바퀴·진열 id·품절 id(진열 순서)와 건물 id 순으로 정렬한 공개 타일 짝을 JSON으로 돌려준다. 상태를 바꾸지 않는다.
        /// </summary>
        public string Capture()
        {
            var state = new ShopSave { StockLap = _stockLap, Offers = _offers.ToArray() };
            var soldOut = new List<string>();
            foreach (string id in _offers) if (_soldOut.Contains(id)) soldOut.Add(id);
            state.SoldOut = soldOut.ToArray();
            var ids = new List<string>(_revealed.Keys);
            ids.Sort(StringComparer.Ordinal);
            state.Revealed = new RevealSave[ids.Count];
            for (int i = 0; i < ids.Count; i++) state.Revealed[i] = new RevealSave { RecipeId = ids[i], Tiles = _revealed[ids[i]].ToArray() };
            return UnityEngine.JsonUtility.ToJson(state);
        }

        /// <summary>Capture가 만든 json으로 진열·품절·공개 타일을 바꾸고 Changed를 낸다. 집합과 사전은 배열에서 다시 만든다.</summary>
        public void Restore(string json)
        {
            ShopSave state = UnityEngine.JsonUtility.FromJson<ShopSave>(json);
            _stockLap = state.StockLap;
            _offers.Clear();
            foreach (string id in state.Offers) if (_catalog.Get(id) != null && !_offers.Contains(id)) _offers.Add(id);
            _soldOut.Clear();
            foreach (string id in state.SoldOut) if (_offers.Contains(id)) _soldOut.Add(id);
            _revealed.Clear();
            foreach (RevealSave pair in state.Revealed)
            {
                if (pair == null || _catalog.Get(pair.RecipeId) == null) continue;
                var tiles = new List<string>();
                foreach (string tile in pair.Tiles) if (!tiles.Contains(tile)) tiles.Add(tile);
                _revealed[pair.RecipeId] = tiles;
            }
            UnityEngine.Debug.Log($"SHOP: restored lap={_stockLap} offers={string.Join(",", _offers)} soldOut={string.Join(",", state.SoldOut)} revealed={_revealed.Count}");
            Changed?.Invoke();
        }

        /// <summary>스펙 010. 저장 형식.</summary>
        [Serializable]
        private sealed class ShopSave
        {
            public int StockLap;
            public string[] Offers = Array.Empty<string>();
            public string[] SoldOut = Array.Empty<string>();
            public RevealSave[] Revealed = Array.Empty<RevealSave>();
        }

        /// <summary>스펙 010. 건물 id와 공개 타일 id 배열의 짝.</summary>
        [Serializable]
        private sealed class RevealSave
        {
            public string RecipeId;
            public string[] Tiles = Array.Empty<string>();
        }
    }
}
