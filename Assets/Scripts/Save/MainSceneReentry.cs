using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRing.Save
{
    /// <summary>
    /// 스펙 003 FR-021. 같은 플레이 세션에서 Main 씬을 다시 열 때 설치기·화면의 조립 구독을 처음 열 때와 같이 되살린다.
    /// MainRoot.OnDestroy의 GameServices.Reset()이 Composing·Ready 구독을 지우고, Unity는 RuntimeInitializeOnLoadMethod 훅을
    /// 플레이 진입 때 한 번만 부르기 때문이다. 처음 Main 로드는 기존 BeforeSceneLoad·AfterSceneLoad 경로가 처리하므로 건너뛴다.
    /// 다시 부르는 대상은 SubsystemRegistration 훅(설치 표시·정적 참조 지우기)과 BeforeSceneLoad 또는 AfterSceneLoad 훅(구독)을
    /// 함께 가진 타입이다(설치기·화면 부트 쌍). 순서는 첫 진입과 같이 SubsystemRegistration → BeforeSceneLoad → AfterSceneLoad이며,
    /// RunResultScreen.Restart와 같은 방식이다. SaveInstaller는 종료 가로채기 이벤트를 지우지 않도록 따로 조립 구독만 되살린다.
    /// 캡처 도구(IronRing.Tools)와 이 클래스는 건너뛴다. 대상 타입의 SubsystemRegistration 훅은 Ready 뒤에 만든 정적 상태만 지워야 한다.
    /// </summary>
    public static class MainSceneReentry
    {
        private const string TOOLS_NAMESPACE = "IronRing.Tools";
        private const string PROJECT_NAMESPACE = "IronRing";

        private static int _mainLoads;

        /// <summary>도메인 재로드가 꺼져 있으므로 플레이 진입마다 로드 횟수를 지우고 sceneLoaded를 한 번만 구독한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _mainLoads = 0;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 씬이 열린 뒤 Start 전에 불린다. scene이 Main이고 이번 세션 두 번째 이후 로드이면 설치기·화면 훅을 다시 부른다.
        /// _mainLoads를 늘린다.
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneNames.Main) return;
            _mainLoads++;
            if (_mainLoads <= 1) return;
            List<Type> types = PairedHookTypes();
            int reset = InvokeHooks(types, RuntimeInitializeLoadType.SubsystemRegistration);
            SaveInstaller.SubscribeComposing();
            int before = InvokeHooks(types, RuntimeInitializeLoadType.BeforeSceneLoad);
            int after = InvokeHooks(types, RuntimeInitializeLoadType.AfterSceneLoad);
            Debug.Log("SAVE: Main 재진입 " + _mainLoads + "회째, 설치기·화면 훅 다시 부름 (Subsystem " + reset + ", Before " + before + ", After " + after + ")");
        }

        /// <summary>
        /// 프로젝트 어셈블리에서 SubsystemRegistration 훅과 BeforeSceneLoad 또는 AfterSceneLoad 훅을 함께 가진 타입을 찾아 돌려준다.
        /// IronRing 밖 네임스페이스, 캡처 도구, 이 클래스, SaveInstaller는 뺀다.
        /// </summary>
        private static List<Type> PairedHookTypes()
        {
            List<Type> result = new List<Type>();
            foreach (Type type in typeof(MainSceneReentry).Assembly.GetTypes())
            {
                if (type.Namespace == null || !type.Namespace.StartsWith(PROJECT_NAMESPACE, StringComparison.Ordinal)) continue;
                if (type.Namespace.StartsWith(TOOLS_NAMESPACE, StringComparison.Ordinal)) continue;
                if (type == typeof(MainSceneReentry) || type == typeof(SaveInstaller)) continue;
                bool reset = false;
                bool boot = false;
                foreach (MethodInfo method in StaticMethods(type))
                {
                    RuntimeInitializeOnLoadMethodAttribute hook = Hook(method);
                    if (hook == null) continue;
                    if (hook.loadType == RuntimeInitializeLoadType.SubsystemRegistration) reset = true;
                    if (hook.loadType == RuntimeInitializeLoadType.BeforeSceneLoad || hook.loadType == RuntimeInitializeLoadType.AfterSceneLoad) boot = true;
                }
                if (reset && boot) result.Add(type);
            }
            return result;
        }

        /// <summary>types 각각에서 loadType 훅(인자 없는 static)을 모두 부르고 부른 수를 돌려준다. 예외는 기록하고 계속한다.</summary>
        private static int InvokeHooks(List<Type> types, RuntimeInitializeLoadType loadType)
        {
            int count = 0;
            foreach (Type type in types)
            {
                foreach (MethodInfo method in StaticMethods(type))
                {
                    RuntimeInitializeOnLoadMethodAttribute hook = Hook(method);
                    if (hook == null || hook.loadType != loadType) continue;
                    try
                    {
                        method.Invoke(null, null);
                        count++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            return count;
        }

        /// <summary>type에 직접 선언된 static 메서드 목록을 돌려준다.</summary>
        private static MethodInfo[] StaticMethods(Type type)
        {
            return type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        }

        /// <summary>method가 인자 없는 RuntimeInitializeOnLoadMethod 훅이면 그 특성을, 아니면 null을 돌려준다.</summary>
        private static RuntimeInitializeOnLoadMethodAttribute Hook(MethodInfo method)
        {
            if (method.GetParameters().Length != 0) return null;
            return method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
        }
    }
}
