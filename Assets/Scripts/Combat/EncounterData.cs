using System;

using UnityEngine;

namespace IronRing.Combat
{
    [Serializable]
    public sealed class EncounterData
    {
        [SerializeField] private float _eventRatio;
        [SerializeField] private int _rounds;
        [SerializeField] private float _trainStrength;
        [SerializeField] private int _enemyMin;
        [SerializeField] private int _enemyMax;
        [SerializeField] private string _rewardResource;
        [SerializeField] private int _rewardAmount;
        [SerializeField] private string[] _penaltyResources;
        [SerializeField] private int _penaltyAmount;
        [SerializeField] private float _prosperityPenalty;
        [Tooltip("Story 016: prosperity added to the next hub after a combat victory (0 = none).")]
        [SerializeField] private float _victoryProsperity;
        [Tooltip("Story 016: prosperity added to the next hub after a resolved trackside event (0 = none).")]
        [SerializeField] private float _eventProsperity;
        [SerializeField] private EncounterEvent[] _events;
        [Tooltip("Story 033: true = an occupied waypoint runs a real-time raid (RaidSystem) instead of the dice combat and event roll.")]
        [SerializeField] private bool _raidMode;
        public float EventRatio => _eventRatio;
        public int Rounds => _rounds;
        public float TrainStrength => _trainStrength;
        public int EnemyMin => _enemyMin;
        public int EnemyMax => _enemyMax;
        public string RewardResource => _rewardResource;
        public int RewardAmount => _rewardAmount;
        public string[] PenaltyResources => _penaltyResources;
        public int PenaltyAmount => _penaltyAmount;
        public float ProsperityPenalty => _prosperityPenalty;
        /// <summary>Story 016: next-hub prosperity gain after a victory.</summary>
        public float VictoryProsperity => _victoryProsperity;
        /// <summary>Story 016: next-hub prosperity gain after a resolved event.</summary>
        public float EventProsperity => _eventProsperity;
        public EncounterEvent[] Events => _events;
        /// <summary>Story 033: waypoint encounters become real-time raids while the waypoint is occupied (dice combat code kept).</summary>
        public bool RaidMode => _raidMode;

        /// <summary>Validates loaded encounter inputs and throws if their ranges cannot produce valid encounters.</summary>
        public void Validate()
        {
            if (float.IsNaN(_eventRatio) || _eventRatio < 0 || _eventRatio > 1 || _rounds < 1 || _rounds % 2 == 0 ||
                float.IsNaN(_trainStrength) || float.IsInfinity(_trainStrength) || _trainStrength <= 0 ||
                float.IsNaN(_prosperityPenalty) || float.IsInfinity(_prosperityPenalty) || _prosperityPenalty > 0 ||
                float.IsNaN(_victoryProsperity) || float.IsInfinity(_victoryProsperity) || _victoryProsperity < 0 ||
                float.IsNaN(_eventProsperity) || float.IsInfinity(_eventProsperity) || _eventProsperity < 0 ||
                _enemyMin <= 0 || _enemyMax < _enemyMin || _enemyMax == int.MaxValue ||
                string.IsNullOrWhiteSpace(_rewardResource) || _rewardAmount < 0 || _penaltyAmount < 0 ||
                _penaltyResources == null || _penaltyResources.Length == 0 || _events == null || _events.Length == 0)
                throw new ArgumentException("Invalid encounter data.");
            foreach (string resource in _penaltyResources)
                if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("Empty penalty resource.");
            foreach (EncounterEvent entry in _events)
                if (entry == null || string.IsNullOrWhiteSpace(entry.Resource) || string.IsNullOrWhiteSpace(entry.Title))
                    throw new ArgumentException("Invalid event data.");
        }
    }

    [Serializable]
    public sealed class EncounterEvent
    {
        [SerializeField] private string _title;
        [SerializeField] private string _resource;
        [SerializeField] private int _delta;
        public string Title => _title;
        public string Resource => _resource;
        public int Delta => _delta;
    }
}
