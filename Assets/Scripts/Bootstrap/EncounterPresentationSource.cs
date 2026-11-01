using System;
using System.Collections.Generic;

using IronRing.Combat;
using IronRing.Contracts.Presentation;

namespace IronRing.Bootstrap
{
    /// <summary>실제 휴식지 이벤트 결과를 한국어 문자열 키로 전달한다(스토리 039: 전투 라운드 없음).</summary>
    public sealed class EncounterPresentationSource : IEncounterPresentationSource
    {
        private readonly WaypointResolver _resolver;
        private readonly IReadOnlyDictionary<string, string> _eventKeys;
        public event Action<string> EventResolved;

        /// <summary>resolver와 title별 eventKeys를 받아 규칙을 복제하지 않고 표시 정보를 연결한다.</summary>
        public EncounterPresentationSource(WaypointResolver resolver, IReadOnlyDictionary<string, string> eventKeys)
        {
            _resolver = resolver;
            _eventKeys = eventKeys;
        }

        /// <summary>진행 중인 이벤트를 끝내고 그 문자열 테이블 키를 발행한다. 진행 중이 아니면 아무 상태도 바꾸지 않는다.</summary>
        public void Advance()
        {
            if (!_resolver.IsEncounterActive) return;
            string title = _resolver.EventTitle;
            _resolver.Advance();
            if (!_resolver.IsEncounterActive)
                EventResolved?.Invoke(title != null && _eventKeys.TryGetValue(title, out string key) ? key : "event_supply");
        }
    }
}
