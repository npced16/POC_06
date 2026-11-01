using System.Collections;
using System.Collections.Generic;

using TMPro;
using UnityEngine;

using IronRing.Contracts.Presentation;
using IronRing.Train.Scrap;

namespace IronRing.Presentation.Travel
{
    /// <summary>스토리 038. 클릭으로 인부를 보낼 고철 더미 표시(콜라이더와 함께 붙는다).</summary>
    public sealed class ScrapPileTarget : MonoBehaviour
    {
        /// <summary>ScrapFarming 더미 순번.</summary>
        public int Index;
    }

    /// <summary>
    /// 스토리 038. ScrapFarming 상태를 탑뷰 프리미티브로 그린다: 고철 더미(남은 양만큼 쌓인 사각형 + "고철 n" 라벨, 클릭 콜라이더),
    /// 인부(작은 원, 도망치면 붉게, 들고 있으면 위에 고철 조각, 캐는 중이면 진행 막대). 배달하면 "+n"이 떠오른다. 게임 상태는 바꾸지 않는다.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class ScrapWorkerView : MonoBehaviour
    {
        private const int MaxPieces = 6;

        [SerializeField] private StopTopView _view;

        private ScrapFarming _farming;
        private TravelCatalog _catalog;
        private Transform _root;
        private readonly List<Transform> _piles = new List<Transform>();
        private readonly List<TMP_Text> _pileLabels = new List<TMP_Text>();
        private readonly List<Transform> _workers = new List<Transform>();

        private IEnumerator Start()
        {
            if (_view == null) _view = GetComponent<StopTopView>();
            if (!StopViewMode.IsTop || _view == null) yield break;
            while (!GameServices.IsReady) yield return null;
            _farming = ScrapInstaller.Farming;
            _catalog = _view.Catalog;
            if (_farming == null || _catalog == null) yield break;
            _root = new GameObject("ScrapLayer").transform;
            _root.SetParent(transform, false);
            _farming.StopBegan += Build;
            _farming.StopEnded += OnStopEnded;
            _farming.Delivered += OnDelivered;
            _farming.Fled += OnFled;
        }

        private void OnDestroy()
        {
            if (_farming == null) return;
            _farming.StopBegan -= Build;
            _farming.StopEnded -= OnStopEnded;
            _farming.Delivered -= OnDelivered;
            _farming.Fled -= OnFled;
        }

        private int Order => _catalog.SortingBase + 20;

        private void Build()
        {
            TravelPrimitives.Clear(_root);
            _piles.Clear(); _pileLabels.Clear(); _workers.Clear();
            TravelPrimitives prims = _view.Prims;
            float p = _catalog.ScrapPieceSize;
            foreach (ScrapPile pile in _farming.Piles)
            {
                var go = new GameObject("Pile" + pile.Index);
                go.transform.SetParent(_root, false);
                go.transform.localPosition = new Vector3(pile.X, pile.Y, 0f);
                for (int k = 0; k < MaxPieces; k++)
                {
                    // 아래 3 · 가운데 2 · 위 1 피라미드.
                    int row = k < 3 ? 0 : k < 5 ? 1 : 2;
                    int col = k < 3 ? k - 1 : k < 5 ? k - 3 : 0;
                    float x = row == 1 ? (col == 0 ? -0.5f : 0.5f) * p : col * p * 1.05f;
                    prims.Rect(go.transform, "Piece" + k, x, row * p * 0.7f - p * 0.35f, p, p * 0.75f, k % 2 == 0 ? _catalog.Scrap : _catalog.ScrapRust, Order + row);
                }
                _pileLabels.Add(prims.Label(go.transform, "Label", 0f, -p * 1.4f, "", 2.4f, _catalog.SignText, Order + 4));
                go.AddComponent<BoxCollider2D>().size = new Vector2(p * 4f, p * 3.5f);
                go.AddComponent<ScrapPileTarget>().Index = pile.Index;
                _piles.Add(go.transform);
            }
            foreach (ScrapWorker worker in _farming.Workers)
            {
                var go = new GameObject("Worker" + worker.Index);
                go.transform.SetParent(_root, false);
                prims.Circle(go.transform, "Body", 0f, 0f, _catalog.WorkerSize, _catalog.Worker, Order + 6);
                prims.Circle(go.transform, "Head", 0f, 0f, _catalog.WorkerSize * 0.45f, _catalog.Brass, Order + 7);
                prims.Rect(go.transform, "Carry", 0f, _catalog.WorkerSize * 0.7f, p * 0.7f, p * 0.5f, _catalog.ScrapRust, Order + 8);
                prims.Rect(go.transform, "Progress", 0f, -_catalog.WorkerSize * 0.8f, _catalog.WorkerSize * 1.4f, 0.06f, _catalog.SignText, Order + 8);
                _workers.Add(go.transform);
            }
            LateUpdate();
        }

        private void OnStopEnded(bool departed, int bonus) => TravelPrimitives.Clear(_root);

        private void OnDelivered(int worker, int amount)
        {
            if (worker < 0 || worker >= _workers.Count || _workers[worker] == null) return;
            Vector3 at = _root.InverseTransformPoint(_workers[worker].position);
            TMP_Text label = _view.Prims.Label(_root, "Gain", at.x, at.y + 0.6f, "+" + amount, 3f, _catalog.SignText, Order + 12);
            StartCoroutine(Rise(label));
        }

        private IEnumerator Rise(TMP_Text label)
        {
            float t = 0f;
            while (t < 1f && label != null)
            {
                t += Time.deltaTime;
                label.transform.localPosition += Vector3.up * Time.deltaTime * 0.8f;
                yield return null;
            }
            if (label != null) Destroy(label.gameObject);
        }

        private void OnFled(int worker) => IronRing.Hud.Ui.ToastScreen.Show(StopTopView.Strings("stop_worker_flee", "인부가 적을 피해 열차로 달아난다"), 1.2f);

        private void LateUpdate()
        {
            if (_farming == null || !_farming.IsActive) return;
            for (int i = 0; i < _piles.Count && i < _farming.Piles.Count; i++)
            {
                ScrapPile pile = _farming.Piles[i];
                int shown = Mathf.CeilToInt(MaxPieces * pile.Amount / (float)Mathf.Max(1, pile.Max));
                for (int k = 0; k < MaxPieces; k++)
                {
                    Transform piece = _piles[i].Find("Piece" + k);
                    if (piece != null) piece.gameObject.SetActive(k < shown);
                }
                if (_pileLabels[i] != null) _pileLabels[i].text = string.Format(StopTopView.Strings("stop_pile_label", "고철 {0}"), pile.Amount);
            }
            for (int i = 0; i < _workers.Count && i < _farming.Workers.Count; i++)
            {
                ScrapWorker worker = _farming.Workers[i];
                Transform t = _workers[i];
                bool mining = worker.State == WorkerState.Mining;
                float bob = mining ? Mathf.Abs(Mathf.Sin(Time.time * 10f + i)) * 0.06f : 0f;
                t.localPosition = new Vector3(worker.X, worker.Y + bob, 0f);
                foreach (SpriteRenderer r in t.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (r.name == "Body") r.color = worker.State == WorkerState.Fleeing ? _catalog.WorkerFlee : _catalog.Worker;
                    else if (r.name == "Carry") r.gameObject.SetActive(worker.Carry > 0);
                    else if (r.name == "Progress")
                    {
                        r.gameObject.SetActive(mining);
                        float ratio = (worker.Carry + worker.MineProgress) / _farming.Data.CarryMax;
                        r.transform.localScale = new Vector3(_catalog.WorkerSize * 1.4f * Mathf.Clamp01(ratio), 0.06f, 1f);
                    }
                }
            }
        }
    }
}
