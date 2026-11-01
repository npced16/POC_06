using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Combat
{
    /// <summary>
    /// Story 005 / 039. Rest-stop event table (Resources/Story005Encounters.json). Story 039 removed the dice combat entirely:
    /// every arrival at a rest stop resolves exactly one event, picked from that stop's fixed weight table (_restStops) with
    /// the seeded random; a stop without its own table uses every event at weight 1.
    /// </summary>
    [Serializable]
    public sealed class EncounterData
    {
        /// <summary>Effect ids an event may carry (_effect).</summary>
        public const string EFFECT_PROSPERITY = "prosperity";
        public const string EFFECT_CODEX = "codex";
        public const string EFFECT_BOARDING = "boarding";
        public const string EFFECT_DELAY = "delay";
        public const string EFFECT_RESOURCE = "resource";

        [SerializeField] private EncounterEvent[] _events;
        [Tooltip("Story 039: per rest-stop weight tables (node id -> event ids and weights).")]
        [SerializeField] private RestStopTable[] _restStops;
        public EncounterEvent[] Events => _events;
        public RestStopTable[] RestStops => _restStops ?? Array.Empty<RestStopTable>();

        /// <summary>Event by id, or null.</summary>
        public EncounterEvent Find(string id)
        {
            foreach (EncounterEvent entry in _events) if (entry.Id == id) return entry;
            return null;
        }

        /// <summary>Weight table for a rest stop, or null when the stop has none (then every event weighs 1).</summary>
        public RestStopTable TableFor(string stopId)
        {
            foreach (RestStopTable table in RestStops) if (table.Id == stopId) return table;
            return null;
        }

        /// <summary>Validates the loaded event table and throws if it cannot always produce an event.</summary>
        public void Validate()
        {
            if (_events == null || _events.Length == 0) throw new ArgumentException("Invalid encounter data: no events.");
            var ids = new HashSet<string>();
            foreach (EncounterEvent entry in _events)
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Title) || !ids.Add(entry.Id)
                    || float.IsNaN(entry.Amount) || float.IsInfinity(entry.Amount)
                    || (entry.Effect == EFFECT_RESOURCE && string.IsNullOrWhiteSpace(entry.Resource)))
                    throw new ArgumentException("Invalid event data.");
            foreach (RestStopTable table in RestStops)
            {
                if (table == null || string.IsNullOrWhiteSpace(table.Id) || table.Events == null || table.Events.Length == 0)
                    throw new ArgumentException("Invalid rest-stop table.");
                int total = 0;
                foreach (WeightedEvent pick in table.Events)
                {
                    if (pick == null || !ids.Contains(pick.EventId) || pick.Weight < 0) throw new ArgumentException("Invalid rest-stop event: " + table.Id);
                    total += pick.Weight;
                }
                if (total <= 0) throw new ArgumentException("Rest-stop table has no weight: " + table.Id);
            }
        }
    }

    /// <summary>Story 039. One rest-stop event: title (string key via MainSettings _eventKeys), effect and amount.</summary>
    [Serializable]
    public sealed class EncounterEvent
    {
        [SerializeField] private string _id;
        [SerializeField] private string _title;
        [Tooltip("prosperity (next hub +/-amount) | codex (unlock amount recipes) | boarding (boarding chance +amount at this stop) | delay (hold amount more seconds) | resource")]
        [SerializeField] private string _effect;
        [SerializeField] private float _amount;
        [SerializeField] private string _resource;
        [SerializeField] private int _delta;
        public string Id => _id;
        public string Title => _title;
        public string Effect => _effect;
        public float Amount => _amount;
        public string Resource => _resource;
        public int Delta => _delta;
    }

    /// <summary>Story 039. Fixed weight table of one rest stop.</summary>
    [Serializable]
    public sealed class RestStopTable
    {
        [SerializeField] private string _id;
        [SerializeField] private WeightedEvent[] _events;
        public string Id => _id;
        public WeightedEvent[] Events => _events;
    }

    /// <summary>Story 039. Event id and its weight inside a rest-stop table.</summary>
    [Serializable]
    public sealed class WeightedEvent
    {
        [SerializeField] private string _eventId;
        [SerializeField] private int _weight;
        public string EventId => _eventId;
        public int Weight => _weight;
    }
}
