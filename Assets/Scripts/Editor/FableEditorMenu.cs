using UnityEditor;
using UnityEditor.SceneManagement;

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
    }
}
