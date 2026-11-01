using System;
using System.Collections.Generic;
using System.IO;

using UnityEditor;
using UnityEditor.Build.Reporting;
namespace IronRing.Bootstrap.Editor
{
    /// <summary>Creates the authored Main scene once and makes both dedicated and project command builds playable.</summary>
    public static class MainBuild
    {
        private const string SCENE = "Assets/Scenes/Main.unity";

        /// <summary>Creates missing Main assets with real system data, preserves existing scene authoring, sets Main first and builds the player.</summary>
        public static void Build()
        {
            BuildTopDownMain.Build();
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(SCENE, true) };
            foreach (var entry in EditorBuildSettings.scenes) if (entry.path != SCENE) scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
            Directory.CreateDirectory("Builds/Demo/Main");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SCENE }, locationPathName = "Builds/Demo/Main/Game.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Main build failed: " + report.summary.result);
        }

    }
}
