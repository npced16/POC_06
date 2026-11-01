using System;
using System.Collections.Generic;

using IronRing.Combat;
using IronRing.Contracts.Presentation;

namespace IronRing.Bootstrap
{
    /// <summary>실제 조우 결과를 표현 계약의 라운드 및 한국어 이벤트 키로 전달한다.</summary>
    public sealed class EncounterPresentationSource : IEncounterPresentationSource, ICombatRoundCommand
    {
        private readonly WaypointResolver _resolver;
        private readonly IReadOnlyDictionary<string, string> _eventKeys;
        public bool CanAdvance => _resolver.IsEncounterActive;
        public bool IsCombat => _resolver.IsEncounterActive && _resolver.Combat != null;
        public int TotalRounds => IsCombat ? _resolver.Combat.TotalRounds : 0;
        public (int train, int enemy) Strengths => !IsCombat
            ? (0, 0) : ((int)Math.Round(_resolver.Combat.TrainStrength), _resolver.Combat.EnemyStrength);
        public event Action<int, bool> RoundResolved;
        public event Action<string> EventResolved;

        /// <summary>resolver와 title별 eventKeys를 받아 규칙을 복제하지 않고 표시 정보를 연결한다.</summary>
        public EncounterPresentationSource(WaypointResolver resolver, IReadOnlyDictionary<string, string> eventKeys)
        {
            _resolver = resolver;
            _eventKeys = eventKeys;
        }

        /// <summary>실제 조우를 한 단계 진행하고 확정된 라운드 결과 또는 문자열 테이블 키를 발행한다.</summary>
        public void Advance()
        {
            if (!_resolver.IsEncounterActive) return;
            CombatEncounter combat = _resolver.Combat;
            string title = _resolver.EventTitle;
            int previousRound = combat?.Round ?? 0;
            _resolver.Advance();
            if (combat != null && combat.Round > previousRound)
                RoundResolved?.Invoke(combat.Round, combat.LastRoundWon);
            else if (combat == null && !_resolver.IsEncounterActive)
                EventResolved?.Invoke(_eventKeys.TryGetValue(title, out string key) ? key : "event_supply");
        }

        /// <summary>HUD 요청을 실제 조우 진행에 전달하며 진행 중이 아니면 아무 상태도 바꾸지 않는다.</summary>
        public void AdvanceCombatRound() => Advance();
    }
}
