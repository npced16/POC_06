using IronRing.Contracts;

namespace IronRing.Combat
{
    public sealed class CombatEncounter
    {
        private readonly IRandomSource _random;
        public float TrainStrength { get; }
        public int EnemyStrength { get; }
        public int TotalRounds { get; }
        public int Round { get; private set; }
        public int Wins { get; private set; }
        public bool LastRoundWon { get; private set; }
        public bool IsComplete => Round == TotalRounds;
        public EncounterOutcome Outcome => Wins > TotalRounds / 2 ? EncounterOutcome.Victory : EncounterOutcome.Defeat;

        /// <summary>Uses data, facility bonus and injected randomness to initialize a single auto-resolved combat.</summary>
        public CombatEncounter(EncounterData data, float bonus, IRandomSource random)
        {
            _random = random;
            TrainStrength = data.TrainStrength + bonus;
            EnemyStrength = random.NextInt(data.EnemyMin, data.EnemyMax + 1);
            TotalRounds = data.Rounds;
        }

        /// <summary>Consumes one random roll, updates round and win counts, and returns false if combat already ended.</summary>
        public bool AdvanceRound()
        {
            if (IsComplete) return false;
            LastRoundWon = _random.NextFloat() < TrainStrength / (TrainStrength + EnemyStrength);
            Round++;
            if (LastRoundWon) Wins++;
            return true;
        }
    }
}
