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
            return announcer;
        }

        /// <summary>id에 해당하는 승객 이름을 풀에서 찾아 반환하고, 정의가 없으면 id를 반환한다.</summary>
        private string Name(string id) => _passengers.Pool.Find(id)?.Name ?? id;

        /// <summary>id가 waypoint에서 탑승하면 이름과 요청을 토스트 대기열에 추가한다.</summary>
        private void OnBoarded(string id, string waypoint)
        {
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
                line += " · " + WorkLabel(npc.WorkKind) + " +" + npc.WorkBonus;
            Enqueue(line);
        }

        /// <summary>직무 종류를 받아 합류 알림에 쓸 짧은 한국어 효과 이름을 반환한다.</summary>
        private static string WorkLabel(string kind)
        {
            switch (kind)
            {
                case "income": return "매 바퀴 마르크";
                case "housing": return "주거";
                case "light": return "빛";
                default: return "일손";
            }
        }

        /// <summary>id가 열차를 떠나면 떠남 알림을 토스트 대기열에 추가한다.</summary>
        private void OnLeft(string id) => Enqueue(string.Format(_passengers.Pool.LeaveToastFormat, Subject(Name(id))));

        /// <summary>이름 뒤에 주격 조사(이/가)를 붙인다. 마지막 글자에 받침이 있으면 "이", 없으면 "가". 한글이 아니면 "가".</summary>
        private static string Subject(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            char last = name[name.Length - 1];
            bool hangul = last >= '가' && last <= '힣';
            bool batchim = hangul && (last - 0xAC00) % 28 != 0;
            return name + (batchim ? "이" : "가");
        }

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
        }
    }
}
