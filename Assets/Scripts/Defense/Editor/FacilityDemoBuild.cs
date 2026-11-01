using System.IO;

using UnityEngine;

using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

using IronRing.Defense.Demo;

namespace IronRing.Defense.Editor
{
    /// <summary>Creates only the owned demo scene and builds a separate player without modifying shared build settings.</summary>
    public static class FacilityDemoBuild
    {
        /// <summary>Creates the Story006 scene once, preserves subsequent authoring and builds the dedicated evidence player.</summary>
        public static void Build()
        {
            const string SCENE = "Assets/Scenes/Demo/Story006/FacilityDemo.unity";
            Directory.CreateDirectory("Assets/Scenes/Demo/Story006");
            if (!File.Exists(SCENE))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Demo Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                var view = new GameObject("Facility Hub").AddComponent<FacilityDemoView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_catalogData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Defense/Data/Facilities.json");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, SCENE);
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity", SCENE },
                locationPathName = "Builds/Demo/Story006/Game.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Story006 demo build failed: " + report.summary.result);
        }
    }
}
