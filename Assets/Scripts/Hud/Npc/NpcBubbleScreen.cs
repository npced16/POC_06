using System.Collections.Generic;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Hud.Ui;
using IronRing.Presentation.HubPhase;

namespace IronRing.Hud.Npc
{
    /// <summary>
    /// 항상 켜진 화면 "NpcBubble". 마을 내부에 보이는 NPC가 정해진 간격마다 머리 오른쪽 위에 혼잣말 말풍선을 띄운다.
    /// 상황 화면이 열리거나 내부 화면이 닫히면 모두 숨긴다. 간격과 크기는 hub_phase_view.json에서 읽고 게임 상태는 바꾸지 않는다.
    /// </summary>
    [RequireComponent(typeof(UiScreen))]
    public sealed class NpcBubbleScreen : MonoBehaviour
    {
        [SerializeField] private NpcBubbleView _bubblePrefab;

        private readonly Dictionary<string, Speaker> _speakers = new Dictionary<string, Speaker>();
        private readonly List<ActiveBubble> _active = new List<ActiveBubble>();
        private readonly Stack<NpcBubbleView> _idle = new Stack<NpcBubbleView>();
        private readonly List<string> _npcIds = new List<string>();
        private readonly System.Random _random = new System.Random();
        private HubPhaseViewSettings _settings;
        private RectTransform _area;
        private INpcInteraction _interaction;
        private bool _blocked = true;

        private sealed class Speaker
        {
            public float NextAt;
            public string LastLine;
            public readonly List<string> Bag = new List<string>();
        }

        private sealed class ActiveBubble
        {
            public string NpcId;
            public NpcBubbleView View;
            public float Until;
            public Vector2 Head;
        }

        void Awake()
        {
            _settings = HubPhaseViewSettings.Load();
            _area = (RectTransform)transform;
        }

        void LateUpdate()
        {
            if (_interaction == null) _interaction = GameServices.Get<INpcInteraction>();
            _npcIds.Clear();
            InteriorNpcWalker.CollectVisibleNpcs(_npcIds);
            if (_interaction == null || _npcIds.Count == 0 || SituationOpen())
            {
                Block();
                return;
            }
            if (_blocked) Unblock();
            FollowActive();
            foreach (string npcId in _npcIds) Tick(npcId);
        }

        /// <summary>말풍선을 표시할 수 없는 상태로 바꾸고 떠 있는 말풍선을 모두 숨긴다.</summary>
        private void Block()
        {
            _blocked = true;
            for (int i = _active.Count - 1; i >= 0; i--) Release(i);
        }

        /// <summary>표시를 다시 시작하며 밀린 차례가 한꺼번에 터지지 않게 모든 NPC의 다음 시각을 새로 뽑는다.</summary>
        private void Unblock()
        {
            _blocked = false;
            foreach (Speaker speaker in _speakers.Values) speaker.NextAt = Time.time + FirstDelay();
        }

        /// <summary>떠 있는 말풍선을 NPC 머리에 맞춰 옮기고, 시간이 끝났거나 NPC가 사라진 말풍선을 숨긴다.</summary>
        private void FollowActive()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ActiveBubble bubble = _active[i];
                if (Time.time >= bubble.Until || !TryGetHead(bubble.NpcId, out Vector2 head))
                {
                    Release(i);
                    continue;
                }
                bubble.Head = head;
                Place(bubble.View, head);
            }
        }

        /// <summary>npcId의 차례가 되면 다음 시각을 정하고 말풍선을 띄운다. 처음 보는 NPC는 첫 시각만 정한다.</summary>
        private void Tick(string npcId)
        {
            if (!_speakers.TryGetValue(npcId, out Speaker speaker))
            {
                _speakers[npcId] = new Speaker { NextAt = Time.time + FirstDelay() };
                return;
            }
            if (Time.time < speaker.NextAt) return;
            speaker.NextAt = Time.time + NextRange(_settings.NpcMutterIntervalMin, _settings.NpcMutterIntervalMax);
            TrySpeak(npcId, speaker);
        }

        /// <summary>동시 표시 수와 겹침을 확인해 npcId의 말풍선을 띄운다. 조건이 맞지 않으면 이번 차례를 건너뛴다.</summary>
        private void TrySpeak(string npcId, Speaker speaker)
        {
            if (_active.Count >= _settings.NpcMutterMaxVisible) return;
            if (!TryGetHead(npcId, out Vector2 head)) return;
            foreach (ActiveBubble other in _active)
                if (other.NpcId == npcId || Vector2.Distance(other.Head, head) < _settings.NpcMutterMinSpacing) return;
            string line = NextLine(npcId, speaker);
            if (line == null) return;

            NpcBubbleView view = _idle.Count > 0 ? _idle.Pop() : Instantiate(_bubblePrefab, transform);
            view.Show(line);
            Place(view, head);
            _active.Add(new ActiveBubble { NpcId = npcId, View = view, Until = Time.time + _settings.NpcMutterSeconds, Head = head });
        }

        /// <summary>speaker의 주머니에서 다음 대사를 꺼낸다. 비면 다시 섞어 채우고, 대사가 없으면 null을 반환한다.</summary>
        private string NextLine(string npcId, Speaker speaker)
        {
            if (speaker.Bag.Count == 0)
            {
                speaker.Bag.AddRange(_interaction.MutterLines(npcId));
                if (speaker.Bag.Count == 0) return null;
                for (int i = speaker.Bag.Count - 1; i > 0; i--)
                {
                    int pick = _random.Next(i + 1);
                    (speaker.Bag[i], speaker.Bag[pick]) = (speaker.Bag[pick], speaker.Bag[i]);
                }
            }
            // 주머니를 새로 채운 직후 직전 대사가 또 나오지 않게 다음 칸과 바꾼다.
            int last = speaker.Bag.Count - 1;
            if (speaker.Bag[last] == speaker.LastLine && last > 0)
                (speaker.Bag[last], speaker.Bag[0]) = (speaker.Bag[0], speaker.Bag[last]);
            string line = speaker.Bag[last];
            speaker.Bag.RemoveAt(last);
            speaker.LastLine = line;
            return line;
        }

        /// <summary>npcId 머리의 화면 좌표를 이 화면의 로컬 좌표로 바꿔 head에 담는다. 화면 밖이면 false를 반환한다.</summary>
        private bool TryGetHead(string npcId, out Vector2 head)
        {
            head = Vector2.zero;
            if (!InteriorNpcWalker.TryGetHeadScreenPoint(npcId, out Vector2 screen)) return false;
            if (screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out head);
        }

        /// <summary>view를 head의 오른쪽 위에 놓는다. 오른쪽 끝을 넘으면 왼쪽으로 뒤집고 위쪽 끝은 넘지 않게 내린다.</summary>
        private void Place(NpcBubbleView view, Vector2 head)
        {
            Rect area = _area.rect;
            Vector2 size = view.Size;
            float margin = _settings.NpcMutterEdgeMargin;
            float offsetX = _settings.NpcMutterOffsetX;
            bool flipped = head.x + offsetX + size.x > area.xMax - margin;
            float x = flipped ? head.x - offsetX : head.x + offsetX;
            float y = Mathf.Min(head.y + _settings.NpcMutterOffsetY, area.yMax - margin - size.y);
            view.Place(new Vector2(x, y), flipped);
        }

        /// <summary>_active의 index 말풍선을 숨기고 다시 쓸 수 있게 _idle로 돌려놓는다.</summary>
        private void Release(int index)
        {
            NpcBubbleView view = _active[index].View;
            view.Hide();
            _idle.Push(view);
            _active.RemoveAt(index);
        }

        /// <summary>마을에 들어온 뒤 첫 혼잣말까지의 시간을 무작위로 반환해 동시 발화를 막는다.</summary>
        private float FirstDelay() => NextRange(0f, _settings.NpcMutterIntervalMax);

        /// <summary>min과 max 사이의 무작위 값을 반환한다.</summary>
        private float NextRange(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        /// <summary>항상 켜진 화면이 아닌 화면이 하나라도 열려 있으면 true를 반환한다.</summary>
        private static bool SituationOpen()
        {
            UiScreens screens = UiScreens.Instance;
            if (screens == null) return false;
            foreach (UiScreen screen in screens.Screens)
                if (screen != null && !screen.AlwaysOn && screens.IsOpen(screen.Id)) return true;
            return false;
        }
    }
}
