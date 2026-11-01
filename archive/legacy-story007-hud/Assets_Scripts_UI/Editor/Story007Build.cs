using System.IO;

using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

using IronRing.UI.Demo;

namespace IronRing.UI.Editor
{
    /// <summary>Creates and builds only the Story007 demonstration using Unity serialization.</summary>
    public static class Story007Build
    {
        private const string SCENE = "Assets/Scenes/Demo/Story007/Story007Hud.unity";
        private const string PANEL = "Assets/UI/Story007/HudPanel.asset";
        /// <summary>Creates missing demo assets once, preserves existing authored scenes, and builds a dedicated player; throws on build failure.</summary>
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes/Demo/Story007");
            if (!File.Exists(SCENE))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL);
                if (panel == null)
                {
                    panel = ScriptableObject.CreateInstance<PanelSettings>();
                    panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    panel.referenceResolution = new Vector2Int(1280, 720);
                    AssetDatabase.CreateAsset(panel, PANEL);
                }
                var camera = new GameObject("Demo Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.1f, 0.12f);
                var root = new GameObject("Story007 HUD");
                var document = root.AddComponent<UIDocument>();
                document.panelSettings = panel;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Story007/Hud.uxml");
                var view = root.AddComponent<HudDocument>();
                root.AddComponent<HudDemoBootstrap>().Configure(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/UI/Story007/DemoData.json"), view);
                EditorSceneManager.SaveScene(scene, SCENE);
                AssetDatabase.SaveAssets();
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity", SCENE },
                locationPathName = "Builds/Demo/Story007/Game.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new System.InvalidOperationException("Story007 build failed: " + report.summary.result);
        }
    }
}
