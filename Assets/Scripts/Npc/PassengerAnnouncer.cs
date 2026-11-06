using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Npc
{
    /// <summary>
    /// IPassengers 탑승 이벤트가 활성 휴식지 사건에서 발생하면 초상 대화를 순서대로 열고,
    /// 그 밖의 승객 이벤트는 기존 한국어 토스트로 알린다. Passengers에는 화면 참조를 두지 않는다.
    /// </summary>
    public sealed class PassengerAnnouncer : MonoBehaviour
    {
        private readonly Queue<string> _queue = new Queue<string>();
        private Passengers _passengers;
        private float _wait;

        /// <summary>Creates the announcer for passengers (call once after the system is installed).</summary>
        public static PassengerAnnouncer Create(Passengers passengers)
        {
            var go = new GameObject(nameof(PassengerAnnouncer));
            var announcer = go.AddComponent<PassengerAnnouncer>();
            announcer._passengers = passengers;
            passengers.Boarded += announcer.OnBoarded;
            passengers.Disembarked += announcer.OnDisembarked;
            passengers.Left += announcer.OnLeft;
            passengers.Returned += announcer.OnReturned;
            return announcer;
        }

        /// <summary>id에 해당하는 승객 이름을 풀에서 찾아 반환하고, 정의가 없으면 id를 반환한다.</summary>
        private string Name(string id) => _passengers.Pool.Find(id)?.Name ?? id;

        /// <summary>id가 waypoint에서 탑승하면 이름과 요청을 토스트 대기열에 추가한다.</summary>
        private void OnBoarded(string id, string waypoint)
        {
            if (_passengers.IsReturning(id))
            {
                Enqueue(string.Format(_passengers.Pool.ReboardToastFormat, Subject(Name(id))));
                return;
            }
            NpcDefinition npc = _passengers.Pool.Find(id);
            Enqueue(string.Format(_passengers.Pool.BoardToastFormat, Subject(Name(id)), _passengers.OriginName(id),
                PassengerRequest.Label(npc) ?? _passengers.Proposal(id)));
        }

        /// <summary>id가 hub에 내리면 이름과 직무 보너스를 토스트 대기열에 추가한다.</summary>
        private void OnDisembarked(string id, string hub)
        {
            string line = string.Format(_passengers.Pool.DisembarkToastFormat, Subject(Name(id)), _passengers.OriginName(id));
            NpcDefinition npc = _passengers.Pool.Find(id);
            if (npc != null && npc.WorkBonus > 0)
                line += " · " + PassengerRequest.WorkLabel(npc.WorkKind) + " +" + npc.WorkBonus;
            Enqueue(line);
        }

        /// <summary>스펙 005. 다시 탄 주민 id가 hub에 내리면 재하차 문구를 토스트 대기열에 추가한다.</summary>
        private void OnReturned(string id, string hub) => Enqueue(string.Format(_passengers.Pool.ReturnToastFormat, Subject(Name(id))));

        /// <summary>id가 열차를 떠나면 떠남 알림을 토스트 대기열에 추가한다.</summary>
        private void OnLeft(string id) => Enqueue(string.Format(_passengers.Pool.LeaveToastFormat, Subject(Name(id))));

        /// <summary>name 뒤에 주격 조사를 붙인 문자열을 PassengerRequest.Subject로 만들어 반환한다.</summary>
        private static string Subject(string name) => PassengerRequest.Subject(name);

        /// <summary>text를 기록하고 화면에 순서대로 표시할 토스트 대기열에 추가한다.</summary>
        private void Enqueue(string text)
        {
            UnityEngine.Debug.Log("PASSENGER: toast \"" + text + "\"");
            _queue.Enqueue(text);
        }

        void Update()
        {
            if (IronRing.Contracts.Presentation.GameServices.Get<IronRing.Contracts.IRestEvents>()?.IsActive == true) return;
            if (_wait > 0f) { _wait -= Time.unscaledDeltaTime; return; }
            if (_queue.Count == 0) return;
            float seconds = _passengers.Pool.ToastSeconds;
            IronRing.Hud.Ui.ToastScreen.Show(_queue.Dequeue(), seconds);
            _wait = seconds;
        }

        void OnDestroy()
        {
            if (_passengers == null) return;
            _passengers.Boarded -= OnBoarded;
            _passengers.Disembarked -= OnDisembarked;
            _passengers.Left -= OnLeft;
            _passengers.Returned -= OnReturned;
        }
    }
}
