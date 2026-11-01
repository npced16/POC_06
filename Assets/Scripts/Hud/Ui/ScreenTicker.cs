using System;
using System.Collections.Generic;

using UnityEngine;

namespace IronRing.Hud.Ui
{
    /// <summary>
    /// 스토리 022. 꺼진(SetActive false) 상황 화면 대신 매 프레임 일을 돌려 주는 숨은 오브젝트. 도감의 C 키·토스트 큐,
    /// NPC 대화의 늦은 서비스 연결처럼 화면이 닫혀 있어도 돌아야 하는 일만 등록한다. UI를 만들지 않고 게임 상태도 갖지 않는다.
    /// </summary>
    public sealed class ScreenTicker : MonoBehaviour
    {
        private static ScreenTicker _instance;
        private static readonly List<Action> Ticks = new List<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; Ticks.Clear(); }

        /// <summary>tick을 매 프레임 부르도록 등록한다(처음 등록 때 숨은 오브젝트를 만든다). 같은 대리자는 한 번만 들어간다.</summary>
        public static void Add(Action tick)
        {
            if (tick == null || Ticks.Contains(tick)) return;
            Ticks.Add(tick);
            if (_instance == null) _instance = new GameObject(nameof(ScreenTicker)).AddComponent<ScreenTicker>();
        }

        /// <summary>등록을 푼다.</summary>
        public static void Remove(Action tick) { Ticks.Remove(tick); }

        void Update()
        {
            for (int i = Ticks.Count - 1; i >= 0; i--)
            {
                if (i >= Ticks.Count) continue;
                try { Ticks[i]?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
