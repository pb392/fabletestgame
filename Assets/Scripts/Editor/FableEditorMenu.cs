using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fable.EditorTools
{
    /// <summary>
    /// The game builds itself in code, so it runs from any scene. A standalone build still
    /// needs one scene in the build list; this menu item creates it.
    /// </summary>
    public static class FableEditorMenu
    {
        [MenuItem("Fable/Create Boot Scene")]
        static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true) };
            AssetDatabase.Refresh();
        }

        /// <summary>Builds a Mac app to Builds/macOS/Fablebound.app. Needs the Mac Build Support module.</summary>
        [MenuItem("Fable/Build macOS App")]
        static void BuildMac()
        {
            const string scenePath = "Assets/Scenes/Boot.unity";
            if (!System.IO.File.Exists(scenePath)) CreateBootScene();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = "Builds/macOS/Fablebound.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Mac build written to Builds/macOS/Fablebound.app");
                EditorUtility.RevealInFinder("Builds/macOS/Fablebound.app");
            }
            else Debug.LogError("Mac build failed: " + report.summary.result + ". See the Console for details.");
        }
    }
}
