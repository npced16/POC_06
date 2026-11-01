using System;
using System.IO;

using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace IronRing.Habitat.Demo
{
    /// <summary>Creates only the story-owned demo scene and builds a standalone visual evidence player.</summary>
    public static class Story002Builder
    {
        private const string SCENE_PATH = "Assets/Scenes/Demo/Story002/HabitatDemo.unity";
        private const string PANEL_PATH = "Assets/Scenes/Demo/Story002/HabitatPanel.asset";

        /// <summary>Creates initial demo assets only when absent, preserving authored scene settings, then builds the demo player.</summary>
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes/Demo/Story002");
            if (!File.Exists(SCENE_PATH)) CreateScene();
            Directory.CreateDirectory("Builds/Demo/Story002");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity", SCENE_PATH },
                locationPathName = "Builds/Demo/Story002/Game.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Story002 build failed.");
        }

        /// <summary>Creates the initial Story002 scene, camera, panel and serialized demo references once through Editor APIs.</summary>
        private static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Demo Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.067f, 0.098f, 0.114f);
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            AssetDatabase.CreateAsset(panel, PANEL_PATH);
            var go = new GameObject("Habitat Workshop");
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/Habitat/Resources/Story002/HabitatDemo.uxml");
            var view = go.AddComponent<HabitatDemoView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_configuration").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Habitat/Resources/Story002/habitats.json");
            serialized.FindProperty("_styleSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Habitat/Resources/Story002/HabitatDemo.uss");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
        }
    }
}
