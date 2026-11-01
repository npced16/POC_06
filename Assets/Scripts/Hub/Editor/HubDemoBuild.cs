using System;
using System.IO;

using UnityEngine;

using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

using IronRing.Hub.Demo;

namespace IronRing.Hub.Editor
{
    /// <summary>Creates the isolated scene once, preserves subsequent edits, and builds a demo player.</summary>
    public static class HubDemoBuild
    {
        private const string SCENE_PATH = "Assets/Scenes/Demo/Story004/HubDemo.unity";

        /// <summary>Creates initial camera/view and profile reference only when the scene is absent; builds an isolated demo player.</summary>
        public static void Build()
        {
            if (!File.Exists(SCENE_PATH))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                var view = new GameObject("Hub Demo").AddComponent<HubDemoView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_profiles").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Hub/Data/HubProfiles.json");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, SCENE_PATH);
            }
            Directory.CreateDirectory("Builds/Demo/Story004");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH, "Assets/Scenes/SampleScene.unity" },
                locationPathName = "Builds/Demo/Story004/Game.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Hub demo build failed.");
        }
    }
}
