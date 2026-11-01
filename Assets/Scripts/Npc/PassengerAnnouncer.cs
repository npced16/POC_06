using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Npc
{
    /// <summary>
    /// Story 023 presentation adapter: turns IPassengers events into Korean toasts (ToastScreen, the 022 UGUI toast) one after
    /// another, and logs each as "PASSENGER: toast …". Keeps Passengers free of UI references. Formats come from NpcPool.json.
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

        private string Name(string id) => _passengers.Pool.Find(id)?.Name ?? id;

        private void OnBoarded(string id, string waypoint) =>
            Enqueue(string.Format(_passengers.Pool.BoardToastFormat, Subject(Name(id)), _passengers.OriginName(id), _passengers.Proposal(id), _passengers.Hint(id)));

        private void OnDisembarked(string id, string hub) =>
            Enqueue(string.Format(_passengers.Pool.DisembarkToastFormat, Subject(Name(id)), _passengers.OriginName(id)));

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

        private void Enqueue(string text)
        {
            UnityEngine.Debug.Log("PASSENGER: toast \"" + text + "\"");
            _queue.Enqueue(text);
        }

        void Update()
        {
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
