using System;
using System.Collections.Generic;
using System.Linq;

using IronRing.Combat;
using IronRing.Contracts;
using IronRing.Habitat;
using IronRing.Hub;
using IronRing.Interior;
using IronRing.Inventory;
using IronRing.Npc;
using IronRing.Track;

namespace IronRing.Bootstrap
{
    /// <summary>Owns and wires real systems; no demonstration fakes or duplicated gameplay rules.</summary>
    public sealed class GameSession : IDisposable
    {
        private readonly GameDefinition _data;
        /// <summary>Authored track data or the real movement runtime, according to the owning type.</summary>
        public RingTrack Track { get; }
        /// <summary>Real resource inventory shared by placement, construction and encounters.</summary>
        public ResourceInventory Inventory { get; }
        /// <summary>Real recipe evaluator using this session docking state.</summary>
        public HabitatEvaluator Habitat { get; }
        /// <summary>Production NPC pool or recruiter owned by this composition.</summary>
        public NpcRecruiter Npcs { get; }
        /// <summary>Real prosperity, ruin and run-loss state.</summary>
        public RunState Hubs { get; }
        /// <summary>Real encounter resolver holding this session track.</summary>
        public WaypointResolver Waypoints { get; }
        /// <summary>Story 012 hub interiors (cells and investigations); null when the definition carries no interior data.</summary>
        public HubInterior Interior { get; }
        /// <summary>Habitat proxy handed to NpcRecruiter; starts on the slot evaluator and is rebound by BindHabitat after composing.</summary>
        public HabitatLink HabitatLink { get; }

        /// <summary>Constructs real systems from data and an optional controller-owned track, using reproducible demo seeds when requested.</summary>
        public GameSession(GameDefinition data, bool deterministic, RingTrack track = null)
        {
            _data = data;
            Track = track ?? data.Track.CreateTrack();
            Inventory = new ResourceInventory(data.Habitats.CreateStock());
            Habitat = new HabitatEvaluator(data.Recipes, Inventory, Track);
            int entropy = deterministic ? 0 : Environment.TickCount;
            if (data.Interior != null)
            {
                Interior = new HubInterior(data.Interior, Track.Nodes.Where(node => node.IsStation).Select(node => node.Id).ToList(),
                    Inventory, null, new SeededRandom(data.Interior.Seed ^ entropy));
                if (Track.IsStopped) Interior.OnDocked(Track.CurrentNode.HubId);
            }
            // NpcRecruiter는 GameServices.Composing 뒤에 결정되는 IHabitatSystem(013이 태그 서식지로 교체할 수 있다)을 써야 하지만,
            // 014 설치자가 Composing 시점에 RunState(INpcRecruiter 필요)를 요구하므로 전달 프록시를 주입하고 Bootstrap이 BindHabitat로 재바인딩한다.
            HabitatLink = new HabitatLink(Habitat);
            Npcs = new NpcRecruiter(data.Npcs.Definitions, data.Npcs.JoinChance, HabitatLink, new SeededRandom(data.Settings.NpcSeed ^ entropy));
            Hubs = new RunState(data.HubDefinitions, Npcs);
            Waypoints = new WaypointResolver(data.Encounters, Track, Inventory, Hubs, new SeededRandom(data.Settings.CombatSeed ^ entropy));
            Track.Arrived += OnArrived;
            Track.LapCompleted += Hubs.OnLapCompleted;
            Hubs.RunLost += OnRunLost;
        }

        /// <summary>Points NpcRecruiter (through its proxy) at the habitat implementation that won GameServices.Composing; null keeps the slot evaluator.</summary>
        public void BindHabitat(IHabitatSystem habitat) => HabitatLink.Bind(habitat ?? Habitat);

        /// <summary>Routes committed arrivals to NPC or encounter logic; habitat reads docking directly from this same track.</summary>
        private void OnArrived(ITrackNode node)
        {
            if (node.Kind == TrackNodeKind.Hub)
            {
                Npcs.OnHubVisited(node.HubId);
                Interior?.OnDocked(node.HubId);
            }
            else Waypoints.OnWaypointArrived(node);
        }

        /// <summary>Holds the real track when every hub is ruined so normal play cannot depart after defeat.</summary>
        private void OnRunLost() => Track.RequestHold("run-lost");

        /// <summary>Places only missing configured recipe resources at the docked hub and returns its real completion state.</summary>
        public bool PlaceRecipe(string hubId)
        {
            HabitatDefinition recipe = _data.Recipes.First(item => item.HubId == hubId);
            for (int slot = 0; slot < recipe.SlotCount; slot++)
                if (recipe.Pattern[slot] != null && Habitat.SlotContent(hubId, slot) == null && !Habitat.TryPlace(hubId, slot, recipe.Pattern[slot])) return false;
            return Habitat.IsHabitatComplete(hubId);
        }

        /// <summary>Detaches owned subscriptions and releases encounter holds without changing persistent data assets.</summary>
        public void Dispose()
        {
            Waypoints.Dispose();
            Track.Arrived -= OnArrived;
            Track.LapCompleted -= Hubs.OnLapCompleted;
            Hubs.RunLost -= OnRunLost;
        }
    }

    /// <summary>Seeded LCG with identical integer behavior in tests and players; no scripted outcomes.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        private uint _state;
        /// <summary>Initializes the sequence from the supplied seed.</summary>
        public SeededRandom(int seed) { _state = unchecked((uint)seed); }
        /// <summary>Advances the sequence and returns a uniform 24-bit fraction in [0,1).</summary>
        public float NextFloat() { _state = unchecked(_state * 1664525u + 1013904223u); return (_state >> 8) / 16777216f; }
        /// <summary>Consumes a random fraction and returns an integer in the supplied half-open range.</summary>
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive + (int)(NextFloat() * (maxExclusive - minInclusive));
    }

    /// <summary>
    /// Forwards every habitat query and the flag event to a rebindable target so consumers built before GameServices.Composing
    /// (NpcRecruiter) use whichever IHabitatSystem implementation was registered last, without reflection.
    /// </summary>
    public sealed class HabitatLink : IHabitatSystem
    {
        private IHabitatSystem _target;
        private Action<string> _flagRaised;
        /// <summary>Raised when the current target raises its flag; subscriptions survive rebinding.</summary>
        public event Action<string> EventFlagRaised { add { _flagRaised += value; } remove { _flagRaised -= value; } }
        /// <summary>Current target implementation.</summary>
        public IHabitatSystem Target => _target;
        /// <summary>Starts forwarding to initial.</summary>
        public HabitatLink(IHabitatSystem initial) { Bind(initial); }
        /// <summary>Switches forwarding to target, moving the flag subscription with it.</summary>
        public void Bind(IHabitatSystem target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (ReferenceEquals(target, _target)) return;
            if (_target != null) _target.EventFlagRaised -= Forward;
            _target = target;
            _target.EventFlagRaised += Forward;
        }
        /// <summary>Re-raises the target's flag event to this link's subscribers.</summary>
        private void Forward(string hubId) => _flagRaised?.Invoke(hubId);
        /// <summary>Delegates placement to the current target.</summary>
        public bool TryPlace(string hubId, int slotIndex, string resourceId) => _target.TryPlace(hubId, slotIndex, resourceId);
        /// <summary>Delegates removal to the current target.</summary>
        public bool TryRemove(string hubId, int slotIndex) => _target.TryRemove(hubId, slotIndex);
        /// <summary>Delegates the slot query to the current target.</summary>
        public string SlotContent(string hubId, int slotIndex) => _target.SlotContent(hubId, slotIndex);
        /// <summary>Delegates the completion query to the current target.</summary>
        public bool IsHabitatComplete(string hubId) => _target.IsHabitatComplete(hubId);
        /// <summary>Delegates the flag query to the current target.</summary>
        public bool IsEventFlagSet(string hubId) => _target.IsEventFlagSet(hubId);
    }
}
