using System.IO;

using UnityEngine;

using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

using IronRing.Npc.Demo;

namespace IronRing.Npc.Editor
{
    public static class Story003Build
    {
        private const string SCENE_PATH = "Assets/Scenes/Demo/Story003/NpcJoinDemo.unity";

        /// <summary>Creates the isolated authored demo once, preserving subsequent edits, and builds only its explicit scene list.</summary>
        public static void Build()
        {
            if (!File.Exists(SCENE_PATH))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Demo Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.075f, 0.09f);
                var view = new GameObject("NPC Arrival").AddComponent<NpcDemoView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_poolJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Npc/Data/NpcPool.json");
                serialized.FindProperty("_visitJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/Npc/Data/NpcDemoVisit.json");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, SCENE_PATH);
            }
            Directory.CreateDirectory("Builds/Demo/Story003");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity", SCENE_PATH },
                locationPathName = "Builds/Demo/Story003/Game.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new System.InvalidOperationException("Story 003 demo build failed.");
        }
    }
}
