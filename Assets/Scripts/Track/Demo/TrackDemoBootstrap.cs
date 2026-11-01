using System;

using UnityEngine;

namespace IronRing.Track
{
    /// <summary>Explicitly selected story demonstration; normal scenes keep their authored composition.</summary>
    public static class TrackDemoBootstrap
    {
        /// <summary>Creates the train view only when --track-demo is supplied and no authored controller exists.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--track-demo") < 0) return;
            Application.runInBackground = true;
            if (UnityEngine.Object.FindFirstObjectByType<TrainController>() != null) return;
            var host = new GameObject("Ring Track Train");
            host.AddComponent<TrainController>();
        }
    }
}
