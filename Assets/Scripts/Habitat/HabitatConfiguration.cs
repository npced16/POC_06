using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Habitat
{
    /// <summary>JSON adapter for habitat definitions and starting resource stock; the core has no Unity dependency.</summary>
    [Serializable]
    public sealed class HabitatConfiguration
    {
        [SerializeField] private ResourceEntry[] _resources;
        [SerializeField] private HubEntry[] _hubs;

        /// <summary>Deserializes json, validates all definitions and stock, and returns a usable data adapter.</summary>
        public static HabitatConfiguration Parse(string json)
        {
            var configuration = JsonUtility.FromJson<HabitatConfiguration>(json);
            if (configuration == null) throw new ArgumentException("Missing habitat configuration.");
            configuration.CreateStock();
            configuration.CreateDefinitions();
            return configuration;
        }

        /// <summary>Converts configured resource entries into a new dictionary of non-negative starting quantities.</summary>
        public IReadOnlyDictionary<string, int> CreateStock()
        {
            if (_resources == null || _resources.Length == 0) throw new ArgumentException("Resources are required.");
            var stock = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var resource in _resources)
            {
                if (resource == null || string.IsNullOrWhiteSpace(resource.Id) || resource.StartingStock < 0)
                    throw new ArgumentException("Resource data is invalid.");
                stock.Add(resource.Id, resource.StartingStock);
            }
            return stock;
        }

        /// <summary>Converts configured hubs into immutable definitions and validates every allowed resource against the catalog.</summary>
        public IReadOnlyList<HabitatDefinition> CreateDefinitions()
        {
            if (_hubs == null || _hubs.Length == 0) throw new ArgumentException("Hubs are required.");
            var stock = CreateStock();
            var definitions = new List<HabitatDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hub in _hubs)
            {
                if (hub == null) throw new ArgumentException("Missing hub entry.");
                var definition = hub.CreateDefinition();
                if (!ids.Add(definition.HubId)) throw new ArgumentException("Duplicate hub id.");
                foreach (string id in definition.AllowedResources)
                    if (!stock.ContainsKey(id)) throw new ArgumentException("Hub references unknown resource.");
                definitions.Add(definition);
            }
            return definitions.AsReadOnly();
        }

        [Serializable]
        private sealed class ResourceEntry
        {
            [SerializeField] private string _id;
            [SerializeField] private int _startingStock;

            public string Id => _id;
            public int StartingStock => _startingStock;
        }

        [Serializable]
        private sealed class HubEntry
        {
            [SerializeField] private string _hubId;
            [SerializeField] private int _slotCount;
            [SerializeField] private string[] _allowedResources;
            [SerializeField] private string[] _pattern;
            [SerializeField] private bool _flagSticky;

            /// <summary>Copies this JSON hub's settings into a validated pure C# definition.</summary>
            public HabitatDefinition CreateDefinition()
            {
                return new HabitatDefinition(_hubId, _slotCount, _allowedResources, _pattern, _flagSticky);
            }
        }
    }
}
