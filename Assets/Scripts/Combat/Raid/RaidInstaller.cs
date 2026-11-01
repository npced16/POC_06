using System;
using System.Linq;

using UnityEngine;

using IronRing.Contracts;
using IronRing.Contracts.Presentation;

namespace IronRing.Combat.Raid
{
    /// <summary>
    /// 스토리 033 조립기. GameServices.Composing에서 Resources/Raid.json으로 RaidSystem을 만들어 WaypointResolver에 붙이고 IRaid로 등록한다.
    /// 시간 진행은 런타임 RaidTicker(MonoBehaviour)가 맡고, --raid-demo 인자가 있으면 같은 오브젝트에 RaidDemoDriver를 붙인다.
    /// </summary>
    public static class RaidInstaller
    {
        /// <summary>설치된 습격 규칙. 설치 전이거나 단독 데모 씬이면 null.</summary>
        public static RaidSystem System { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GameServices.Composing -= Install;
            System = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            GameServices.Composing -= Install;
            GameServices.Composing += Install;
        }

        private static void Install()
        {
            if (!(GameServices.Get<IWaypointResolver>() is WaypointResolver resolver)) return;
            TextAsset json = UnityEngine.Resources.Load<TextAsset>("Raid");
            if (json == null) { UnityEngine.Debug.LogError("Missing Story 033 raid data: Resources/Raid.json"); return; }
            RaidData data = RaidData.Parse(json.text);
            // 034가 열차 장비 제공자를 등록하기 전까지는 거점 시설 수를 감싼 임시 제공자를 쓴다(이미 등록돼 있으면 건드리지 않는다).
            if (GameServices.Get<IRaidLoadout>() == null && GameServices.Get<IFacilityBuilder>() != null)
                GameServices.Register<IRaidLoadout>(new FacilityLoadoutAdapter(GameServices.Get<IFacilityBuilder>(), data));
            System = new RaidSystem(data, resolver, () => GameServices.Get<IRaidLoadout>(), GameServices.Get<ITrackRuntime>());
            resolver.AttachRaid(System);
            GameServices.Register<IRaid>(System);
            var host = new GameObject("RaidTicker");
            host.AddComponent<RaidTicker>().Bind(System);
            if (HasArgument("--raid-demo")) host.AddComponent<Demo.RaidDemoDriver>().Bind(System, Argument("--raid-stage") ?? "start");
        }

        /// <summary>실행 인자(편집기 브리지 캡처의 추가 인자 포함)에 name이 있는지.</summary>
        public static bool HasArgument(string name) => Arguments().Contains(name);

        /// <summary>name 다음 인자 값(없으면 null).</summary>
        public static string Argument(string name)
        {
            string[] arguments = Arguments();
            for (int i = 0; i < arguments.Length - 1; i++) if (arguments[i] == name) return arguments[i + 1];
            return null;
        }

        private static string[] Arguments()
        {
            string extra = "";
#if UNITY_EDITOR
            string phase = UnityEditor.SessionState.GetString("IronRing.Bridge.Phase", "");
            if (phase == "enter-play" || phase == "capture")
                extra = UnityEditor.SessionState.GetString("IronRing.Bridge.Capture.ExtraArgs", "");
#endif
            return Environment.GetCommandLineArgs().Concat(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        }
    }

    /// <summary>스토리 033. 프레임마다 RaidSystem.Tick(deltaTime)을 부른다(규칙은 순수 C#에 있다).</summary>
    public sealed class RaidTicker : MonoBehaviour
    {
        private RaidSystem _system;

        /// <summary>진행시킬 습격 규칙을 연결한다.</summary>
        public void Bind(RaidSystem system) => _system = system;

        private void Update() => _system?.Tick(Time.deltaTime);
    }
}
