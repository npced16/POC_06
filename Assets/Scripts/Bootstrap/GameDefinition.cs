using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using IronRing.Combat;
using IronRing.Habitat;
using IronRing.Hub;
using IronRing.Interior;
using IronRing.Npc;
using IronRing.Track;

namespace IronRing.Bootstrap
{
    /// <summary>Validated production data shared by the playable composition and its integration tests.</summary>
    public sealed class GameDefinition
    {
        /// <summary>Authored track data or the real movement runtime, according to the owning type.</summary>
        public TrackData Track { get; private set; }
        /// <summary>Authored habitat and initial inventory configuration.</summary>
        public HabitatConfiguration Habitats { get; private set; }
        /// <summary>Validated habitat recipes keyed by canonical hub identity.</summary>
        public IReadOnlyList<HabitatDefinition> Recipes { get; private set; }
        /// <summary>Production NPC pool or recruiter owned by this composition.</summary>
        public NpcPoolData Npcs { get; private set; }
        /// <summary>Authored prosperity limits and growth coefficients.</summary>
        public IReadOnlyList<HubDefinition> HubDefinitions { get; private set; }
        /// <summary>Authored combat and trackside event configuration.</summary>
        public EncounterData Encounters { get; private set; }
        /// <summary>Authored presentation timing, demo seeds and display names.</summary>
        public MainSettings Settings { get; private set; }
        /// <summary>Story 012 hub interior grid and investigation rules; null when the composition was built without interior data.</summary>
        public InteriorData Interior { get; private set; }
        /// <summary>스토리 048. 처음에 잠긴 역(ring-track.json `_locked`). 런 상태(RunState)는 이 역을 열릴 때까지 세지 않는다.</summary>
        public IReadOnlyList<string> LockedHubIds { get; private set; }
        /// <summary>스토리 048. 처음부터 열린 역(결과 "정착 N/N"의 N).</summary>
        public IReadOnlyList<string> OpenHubIds { get; private set; }

        /// <summary>Parses the authored JSON inputs (interior optional) and rejects cross-system identity mismatches before constructing a run.</summary>
        public static GameDefinition Parse(string track, string habitats, string npcs, string hubs, string combat, string settings, string interior = null)
        {
            var result = new GameDefinition
            {
                Track = TrackData.Parse(track), Habitats = HabitatConfiguration.Parse(habitats),
                Npcs = NpcPoolData.FromJson(npcs), HubDefinitions = HubData.Parse(hubs),
                Encounters = JsonUtility.FromJson<EncounterData>(combat),
                Settings = JsonUtility.FromJson<MainSettings>(settings),
                Interior = interior == null ? null : InteriorData.Parse(interior)
            };
            result.Recipes = result.Habitats.CreateDefinitions();
            result.Encounters.Validate();
            result.Settings.Validate();
            // Story 032: the run uses only the hubs that are stations on the track. Hub profiles and habitat recipes for other hubs
            // (kept so the 4-hub ring can come back through data) are ignored; every station still needs a profile and a recipe.
            var allHubs = new HashSet<string>(result.HubDefinitions.Select(hub => hub.Id));
            var ids = new HashSet<string>(result.Track.CreateTrack().Nodes.Where(node => node.IsStation).Select(node => node.Id));
            if (ids.Count == 0 || !ids.IsSubsetOf(allHubs) || !ids.IsSubsetOf(result.Recipes.Select(recipe => recipe.HubId)))
                throw new ArgumentException("Hub identities differ across production data.");
            result.HubDefinitions = result.HubDefinitions.Where(hub => ids.Contains(hub.Id)).ToList().AsReadOnly();
            // 스토리 048: 잠긴 역도 프로필·레시피·내부는 처음부터 갖고(열리면 바로 쓰도록), 세는 것만 열린 역으로 한다.
            result.LockedHubIds = result.Track.Nodes.Where(node => node.IsStation && node.IsLocked).Select(node => node.Id).ToList().AsReadOnly();
            result.OpenHubIds = result.HubDefinitions.Select(hub => hub.Id).Where(id => !result.LockedHubIds.Contains(id)).ToList().AsReadOnly();
            result.Recipes = result.Recipes.Where(recipe => ids.Contains(recipe.HubId)).ToList().AsReadOnly();
            foreach (var npc in result.Npcs.Definitions)
                if (npc.EligibleHubIds.Any(id => !allHubs.Contains(id))) throw new ArgumentException("NPC references an unknown hub.");
            var resources = new HashSet<string>(result.Habitats.CreateStock().Keys);
            // Story 039: only "resource" events name a resource.
            if (result.Encounters.Events.Any(e => e.Effect == EncounterData.EFFECT_RESOURCE && !resources.Contains(e.Resource)))
                throw new ArgumentException("Encounter references an unknown resource.");
            if (result.Interior != null && result.Interior.BuriedResources.Any(buried => !resources.Contains(buried.Id)))
                throw new ArgumentException("Interior buried resource references an unknown resource.");
            return result;
        }
    }

    [Serializable]
    public sealed class MainSettings
    {
        [Header("Simulation and demonstration")]
        [SerializeField] private int _npcSeed;
        [SerializeField] private int _combatSeed;
        [SerializeField] private float _combatStepSeconds;
        [SerializeField] private float _stationDwellSeconds;
        [SerializeField] private float _demoTimeScale;
        [SerializeField] private float _capturePauseSeconds;
        [SerializeField] private int _maxDemoLaps;
        [SerializeField] private string _demoHubId;
        [SerializeField] private NodeName[] _names;
        [Header("Encounter localization")]
        [SerializeField] private NodeName[] _eventKeys;
        /// <summary>실제 이벤트 제목을 한국어 문자열 테이블 키에 대응시키는 제작 설정.</summary>
        public IReadOnlyDictionary<string, string> EventKeys => _eventKeys.ToDictionary(entry => entry.Id, entry => entry.Name);
        /// <summary>Initial deterministic recruitment random seed.</summary>
        public int NpcSeed => _npcSeed;
        /// <summary>Initial deterministic encounter random seed.</summary>
        public int CombatSeed => _combatSeed;
        /// <summary>Scaled seconds between displayed encounter steps.</summary>
        public float CombatStepSeconds => _combatStepSeconds;
        /// <summary>Scaled demo wait before departing a station.</summary>
        public float StationDwellSeconds => _stationDwellSeconds;
        /// <summary>Time scale used only by the integration demonstration.</summary>
        public float DemoTimeScale => _demoTimeScale;
        /// <summary>Real-time pause before rendering checkpoint evidence.</summary>
        public float CapturePauseSeconds => _capturePauseSeconds;
        /// <summary>Maximum allowed demo circuits before reporting failure.</summary>
        public int MaxDemoLaps => _maxDemoLaps;
        /// <summary>Canonical hub selected for the recruitment demonstration.</summary>
        public string DemoHubId => _demoHubId;

        /// <summary>Returns an authored display name for id while preserving the canonical identifier in simulation.</summary>
        public string DisplayName(string id)
        {
            foreach (NodeName name in _names) if (name.Id == id) return name.Name;
            return id;
        }

        /// <summary>Rejects invalid timing and demo bounds from the external settings input.</summary>
        public void Validate()
        {
            if (_combatStepSeconds <= 0 || _stationDwellSeconds < 0 || _demoTimeScale <= 0 || _capturePauseSeconds <= 0 ||
                _maxDemoLaps < 1 || _names == null || string.IsNullOrEmpty(_demoHubId))
                throw new ArgumentException("Invalid Main settings.");
        }
    }

    [Serializable]
    public sealed class NodeName
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        /// <summary>Canonical node identity used by the real systems.</summary>
        public string Id => _id;
        /// <summary>Authored human-readable node name.</summary>
        public string Name => _name;
    }
}
