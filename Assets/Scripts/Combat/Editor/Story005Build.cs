using System.IO;

using UnityEngine;

using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace IronRing.Combat.Editor
{
    public static class Story005Build
    {
        /// <summary>Creates the isolated demo once, preserving existing scene edits, then builds that scene without changing shared build settings.</summary>
        public static void Build()
        {
            const string scenePath = "Assets/Scenes/Demo/Story005/Story005Combat.unity";
            if (!File.Exists(scenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.055f, 0.075f);
                var view = new GameObject("Combat demo").AddComponent<Demo.CombatDemoView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_encounterData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Combat/Resources/Story005Encounters.json");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            Directory.CreateDirectory("Builds/Demo/Story005");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity", scenePath },
                locationPathName = "Builds/Demo/Story005/Game.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Story005 build failed.");
        }
    }
}
