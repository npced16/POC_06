using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;
using IronRing.Localization;

namespace IronRing.Presentation.Effects
{
    public sealed class StateCombatEffects : MonoBehaviour
    {
        private static StateCombatEffects _instance;
        [Header("Presentation")]
        [SerializeField] private EffectsSettings _settings;
        private IMapAnchors _anchors;
        private IHubStateSystem _hubs;
        private INpcRecruiter _npcs;
        private IFacilityBuilder _facilities;
        private IWaypointResolver _waypoints;
        private IEncounterPresentationSource _encounter;
        private ITrackNode _activeNode;
        private VisualElement _overlay;
        private readonly Dictionary<string, Transform> _fills = new Dictionary<string, Transform>();
        private readonly HashSet<string> _placed = new HashSet<string>();
        private readonly Dictionary<string, GameObject> _raiders = new Dictionary<string, GameObject>();
        private readonly List<FloatingMessage> _messages = new List<FloatingMessage>();
        private readonly List<Debris> _debris = new List<Debris>();

        /// <summary>플레이 세션마다 이전 인스턴스 참조를 비워 새 서비스 연결을 허용한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { _instance = null; }

        /// <summary>서비스 준비 이벤트에 연결하여 현재 씬의 연출 초기화를 예약한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { GameServices.Ready -= Create; GameServices.Ready += Create; }

        /// <summary>등록된 지도와 일곱 시스템을 조회하여 준비된 씬에 연출 객체를 한 번 생성한다.</summary>
        private static void Create()
        {
            if (_instance != null) return;
            IMapAnchors anchors = GameServices.Get<IMapAnchors>();
            ITrackRuntime track = GameServices.Get<ITrackRuntime>();
            IResourceInventory inventory = GameServices.Get<IResourceInventory>();
            IHabitatSystem habitat = GameServices.Get<IHabitatSystem>();
            INpcRecruiter npcs = GameServices.Get<INpcRecruiter>();
            IHubStateSystem hubs = GameServices.Get<IHubStateSystem>();
            IWaypointResolver waypoints = GameServices.Get<IWaypointResolver>();
            IFacilityBuilder facilities = GameServices.Get<IFacilityBuilder>();
            if (anchors == null || track == null || inventory == null || habitat == null || npcs == null || hubs == null || waypoints == null || facilities == null) return;
            _instance = new GameObject(nameof(StateCombatEffects)).AddComponent<StateCombatEffects>();
            _instance.Initialize(anchors, hubs, npcs, facilities, waypoints);
        }

        /// <summary>지도와 상태 시스템 입력을 저장하고 초기 상태를 그린 뒤 변경 이벤트를 구독한다.</summary>
        private void Initialize(IMapAnchors anchors, IHubStateSystem hubs, INpcRecruiter npcs, IFacilityBuilder facilities, IWaypointResolver waypoints)
        {
            _settings = UnityEngine.Resources.Load<EffectsSettings>("Story010Effects");
            _anchors = anchors; _hubs = hubs; _npcs = npcs; _facilities = facilities; _waypoints = waypoints;
            _encounter = GameServices.Get<IEncounterPresentationSource>();
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = KoreanUi.DefaultPanel;
            document.sortingOrder = 100;
            _overlay = document.rootVisualElement;
            KoreanUi.Apply(_overlay);
            _overlay.pickingMode = PickingMode.Ignore;
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0; _overlay.style.top = 0; _overlay.style.bottom = 0;
            foreach (string hubId in hubs.HubIds)
            {
                Transform anchor = anchors.HubAnchor(hubId);
                if (anchor == null) continue;
                SpriteRenderer background = MakeSprite("prosperity_bg", anchor.position + _settings.GaugeOffset, 30);
                background.transform.localScale = _settings.GaugeScale;
                SpriteRenderer fill = MakeSprite("prosperity_fill", background.transform.position, 31);
                _fills.Add(hubId, fill.transform);
                ProsperityChanged(hubId, hubs.GetProsperity(hubId));
                if (hubs.IsRuined(hubId)) SetRuined(hubId, false);
                foreach (string facilityId in facilities.FacilitiesAt(hubId)) FacilityBuilt(hubId, facilityId);
                foreach (string npcId in npcs.NpcsAt(hubId)) AddNpc(hubId, npcId, false);
            }
            hubs.ProsperityChanged += ProsperityChanged; hubs.HubRuined += HubRuined; hubs.RunLost += RunLost;
            npcs.NpcJoined += NpcJoined; facilities.FacilityBuilt += FacilityBuilt;
            waypoints.EncounterStarted += EncounterStarted; waypoints.EncounterEnded += EncounterEnded;
            if (_encounter != null) { _encounter.RoundResolved += RoundResolved; _encounter.EventResolved += EventResolved; }
            if (hubs.IsRunLost) RunLost();
        }

        /// <summary>spriteName의 아트를 position에 생성하고 order 정렬을 적용한 렌더러를 반환한다.</summary>
        private SpriteRenderer MakeSprite(string spriteName, Vector3 position, int order)
        {
            var renderer = new GameObject(spriteName).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.transform.position = position;
            renderer.sprite = _settings.Sprite(spriteName);
            renderer.sharedMaterial = _settings.SpriteMaterial;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>hubId의 번영 value를 설정 최대값으로 정규화하고 게이지 왼쪽을 고정해 채움을 갱신한다.</summary>
        private void ProsperityChanged(string hubId, float value)
        {
            if (!_fills.TryGetValue(hubId, out Transform fill)) return;
            float ratio = Mathf.Clamp01(value / _settings.ProsperityMaximum);
            Vector3 scale = _settings.GaugeScale; scale.x *= ratio;
            fill.localScale = scale;
            float width = fill.GetComponent<SpriteRenderer>().sprite.bounds.size.x;
            fill.position = _anchors.HubAnchor(hubId).position + _settings.GaugeOffset + Vector3.left * (1 - ratio) * _settings.GaugeScale.x * width / 2;
        }

        /// <summary>폐허가 된 hubId의 스프라이트와 잔해 연출을 갱신한다.</summary>
        private void HubRuined(string hubId) { SetRuined(hubId, true); }

        /// <summary>hubId 건물의 원본 아트에 대응하는 폐허를 적용하며 animate일 때 잔해와 문구를 생성한다.</summary>
        private void SetRuined(string hubId, bool animate)
        {
            Transform anchor = _anchors.HubAnchor(hubId);
            if (anchor == null) return;
            foreach (SpriteRenderer renderer in anchor.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.sprite == null) continue;
                Sprite ruined = _settings.Sprite(renderer.sprite.name + "_ruined");
                if (ruined != null) renderer.sprite = ruined;
            }
            if (!animate) return;
            Message(anchor.position, "hub_ruined");
            for (int index = 0; index < _settings.DebrisCount; index++)
            {
                float angle = index * Mathf.PI * 2 / _settings.DebrisCount;
                SpriteRenderer piece = MakeSprite("prosperity_fill", anchor.position, 40);
                piece.transform.localScale = Vector3.one * 0.15f;
                piece.color = new Color(0.7f, 0.48f, 0.27f);
                _debris.Add(new Debris { Renderer = piece, Velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * _settings.DebrisSpeed, Remaining = _settings.DebrisSeconds });
            }
        }

        /// <summary>hubId에 facilityId 시설을 중복 없이 생성하고 설정된 상대 배치를 적용한다.</summary>
        private void FacilityBuilt(string hubId, string facilityId)
        {
            Transform anchor = _anchors.HubAnchor(hubId);
            if (anchor == null || !_placed.Add(hubId + "/facility/" + facilityId)) return;
            MakeSprite(facilityId, anchor.position + _settings.FacilityOffset(facilityId), 15);
        }

        /// <summary>합류한 npcId를 hubId에 표시하고 한국어 한 줄 말풍선을 만든다.</summary>
        private void NpcJoined(string hubId, string npcId) { AddNpc(hubId, npcId, true); }

        /// <summary>hubId의 npcId를 중복 없이 배치하고 speak일 때 합류 문구를 띄운다.</summary>
        private void AddNpc(string hubId, string npcId, bool speak)
        {
            Transform anchor = _anchors.HubAnchor(hubId);
            if (anchor == null || !_placed.Add(hubId + "/npc/" + npcId)) return;
            Vector3 position = anchor.position + _settings.NpcOffset + Vector3.right * Mathf.Max(0, _npcs.NpcsAt(hubId).Count - 1) * _settings.NpcSpacing;
            MakeSprite("npc", position, 20);
            if (speak) Message(position + Vector3.up * 0.6f, "event_recruit");
        }

        /// <summary>node 경유지에 전투 시작 연출을 생성한다.</summary>
        private void EncounterStarted(ITrackNode node)
        {
            _activeNode = node;
            if (_encounter == null || !_encounter.IsCombat) return;
            Transform anchor = _anchors.NodeAnchor(node.Id);
            if (anchor == null) return;
            _raiders[node.Id] = MakeSprite("raider", anchor.position + _settings.RaiderOffset, 25).gameObject;
            Message(anchor.position + Vector3.up, "combat_start", "encounter");
        }

        /// <summary>현재 경유지에 round 라운드의 won 승패를 한국어로 표시하고 이전 전투 말풍선을 교체한다.</summary>
        private void RoundResolved(int round, bool won)
        {
            if (_activeNode == null) return;
            Transform anchor = _anchors.NodeAnchor(_activeNode.Id);
            if (anchor != null) Message(anchor.position + Vector3.up, won ? "combat_victory" : "combat_defeat", "encounter", $"{round}/{_encounter.TotalRounds}  ");
        }

        /// <summary>현재 경유지에 eventId 현지화 문구와 이벤트 아이콘을 표시한다.</summary>
        private void EventResolved(string eventId)
        {
            if (_activeNode == null) return;
            Transform anchor = _anchors.NodeAnchor(_activeNode.Id);
            if (anchor == null) return;
            Message(anchor.position + Vector3.up, eventId, "encounter");
            SpriteRenderer icon = MakeSprite("waypoint", anchor.position + _settings.RaiderOffset, 25);
            Destroy(icon.gameObject, _settings.MessageSeconds);
        }

        /// <summary>node의 습격자를 제거하고 outcome에 맞는 한국어 결과와 이벤트 아이콘을 표시한다.</summary>
        private void EncounterEnded(ITrackNode node, EncounterOutcome outcome)
        {
            if (_raiders.TryGetValue(node.Id, out GameObject raider)) { Destroy(raider); _raiders.Remove(node.Id); }
            Transform anchor = _anchors.NodeAnchor(node.Id);
            if (anchor == null) return;
            if (outcome == EncounterOutcome.EventResolved && _encounter != null) return;
            Message(anchor.position + Vector3.up, outcome == EncounterOutcome.EventResolved ? "event_quiet" : outcome == EncounterOutcome.Victory ? "combat_victory" : "combat_defeat", "encounter");
            if (outcome == EncounterOutcome.EventResolved)
            {
                SpriteRenderer icon = MakeSprite("waypoint", anchor.position + _settings.RaiderOffset, 25);
                Destroy(icon.gameObject, _settings.MessageSeconds);
            }
        }

        /// <summary>position에 key 현지화 문구와 prefix 숫자를 띄우며 같은 group의 이전 문구를 교체하고 수명 목록에 추가한다.</summary>
        private void Message(Vector3 position, string key, string group = null, string prefix = "")
        {
            if (group != null)
                for (int index = _messages.Count - 1; index >= 0; index--)
                    if (_messages[index].Group == group) { _messages[index].Label.RemoveFromHierarchy(); _messages.RemoveAt(index); }
            var label = new Label(prefix + Strings.Get(key));
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.width = _settings.MessageWidth;
            label.style.fontSize = _settings.FontSize;
            label.style.color = _settings.Ink;
            label.style.backgroundColor = _settings.Bubble;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingTop = 6; label.style.paddingBottom = 6;
            label.style.borderBottomLeftRadius = 8; label.style.borderBottomRightRadius = 8;
            _overlay.Add(label);
            _messages.Add(new FloatingMessage { Label = label, Position = position, Remaining = _settings.MessageSeconds, Group = group });
        }

        /// <summary>전체 화면을 어둡게 덮고 한국어 패배 제목과 설명을 표시한다.</summary>
        private void RunLost()
        {
            // Story 014: 사유별 제목은 011 HUD의 패배 화면이 보여 주므로, IRunLossInfo가 있으면 겹치는 제목을 만들지 않는다.
            if (GameServices.Get<IRunLossInfo>() != null) return;
            var shade = new VisualElement { pickingMode = PickingMode.Ignore };
            shade.style.position = Position.Absolute;
            shade.style.left = 0; shade.style.right = 0; shade.style.top = 0; shade.style.bottom = 0;
            shade.style.backgroundColor = _settings.DefeatShade;
            shade.style.alignItems = Align.Center; shade.style.justifyContent = Justify.Center;
            var title = new Label(Strings.Get("defeat_title"));
            title.style.color = _settings.Ink; title.style.fontSize = _settings.FontSize * 2;
            var body = new Label(Strings.Get("defeat_body"));
            body.style.color = _settings.Ink; body.style.fontSize = _settings.FontSize;
            shade.Add(title); shade.Add(body); _overlay.Add(shade);
        }

        void Update()
        {
            for (int index = _messages.Count - 1; index >= 0; index--)
            {
                FloatingMessage message = _messages[index];
                message.Remaining -= Time.unscaledDeltaTime;
                if (message.Remaining <= 0) { message.Label.RemoveFromHierarchy(); _messages.RemoveAt(index); continue; }
                message.Position += Vector3.up * (_settings.RiseSpeed * Time.unscaledDeltaTime);
                Vector2 panel = RuntimePanelUtils.CameraTransformWorldToPanel(_overlay.panel, message.Position, _anchors.MapCamera);
                message.Label.style.left = panel.x - _settings.MessageWidth / 2;
                message.Label.style.top = panel.y;
            }
            for (int index = _debris.Count - 1; index >= 0; index--)
            {
                Debris piece = _debris[index]; piece.Remaining -= Time.unscaledDeltaTime;
                if (piece.Remaining <= 0) { Destroy(piece.Renderer.gameObject); _debris.RemoveAt(index); continue; }
                piece.Renderer.transform.position += piece.Velocity * Time.unscaledDeltaTime;
                Color color = piece.Renderer.color; color.a = piece.Remaining / _settings.DebrisSeconds; piece.Renderer.color = color;
            }
        }

        void OnDestroy()
        {
            GameServices.Ready -= Create;
            if (_hubs == null) return;
            _hubs.ProsperityChanged -= ProsperityChanged; _hubs.HubRuined -= HubRuined; _hubs.RunLost -= RunLost;
            _npcs.NpcJoined -= NpcJoined; _facilities.FacilityBuilt -= FacilityBuilt;
            _waypoints.EncounterStarted -= EncounterStarted; _waypoints.EncounterEnded -= EncounterEnded;
            if (_encounter != null) { _encounter.RoundResolved -= RoundResolved; _encounter.EventResolved -= EventResolved; }
        }

        private sealed class FloatingMessage
        {
            public Label Label { get; set; }
            public Vector3 Position { get; set; }
            public float Remaining { get; set; }
            public string Group { get; set; }
        }

        private sealed class Debris
        {
            public SpriteRenderer Renderer { get; set; }
            public Vector3 Velocity { get; set; }
            public float Remaining { get; set; }
        }
    }
}
