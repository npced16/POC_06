#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;

using UnityEngine;

using UnityEditor;

namespace IronRing.Tools
{
    public sealed class CoordinatorCapture : MonoBehaviour
    {
        private const string CAPTURE_KEY = "IronRing.Bridge.Capture";

        /// <summary>세션의 캡처 예약을 읽어 Play 모드에만 임시 캡처 컴포넌트를 생성한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (!SessionState.GetBool(CAPTURE_KEY + ".Armed", false)) return;
            SessionState.SetBool(CAPTURE_KEY + ".Armed", false);
            var host = new GameObject("Coordinator Bridge Capture");
            DontDestroyOnLoad(host);
            host.AddComponent<CoordinatorCapture>();
        }

        IEnumerator Start()
        {
            // 편집기가 포커스를 잃어도(다른 창, 대화상자) 프레임이 계속 진행되어 캡처가 끝나도록 한다.
            Application.runInBackground = true;
            int frames = SessionState.GetInt(CAPTURE_KEY + ".Frames", 0);
            float seconds = SessionState.GetFloat(CAPTURE_KEY + ".Seconds", 0);
            double start = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < Math.Max(1, frames); i++) yield return null;
            if (frames == 0)
                while (Time.realtimeSinceStartupAsDouble - start < seconds) yield return null;
            yield return new WaitForEndOfFrame();
            string path = SessionState.GetString(CAPTURE_KEY + ".Path", "");
            try { ScreenCapture.CaptureScreenshot(path); }
            catch (Exception ex) { Complete(ex.ToString()); yield break; }
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
                if (File.Exists(path) && new FileInfo(path).Length > 8)
                {
                    Complete("");
                    yield break;
                }
            }
            Complete("Screenshot write timed out after 30 seconds.");
        }

        /// <summary>캡처 오류 문자열을 받아 Editor가 Play 종료를 수행하도록 완료 상태를 기록한다.</summary>
        private static void Complete(string error)
        {
            SessionState.SetString(CAPTURE_KEY + ".Error", error);
            SessionState.SetBool(CAPTURE_KEY + ".Done", true);
        }
    }
}
#endif
