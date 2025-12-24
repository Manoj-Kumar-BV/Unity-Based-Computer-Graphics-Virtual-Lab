#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartScene
{
    private const string MainMenuScenePath = "Assets/LineDrawingAlgorithm/Examples/MainMenu/MainMenu.unity";

    static PlayModeStartScene()
    {
        EditorApplication.delayCall += Configure;
    }

    private static void Configure()
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
        if (sceneAsset != null)
        {
            EditorSceneManager.playModeStartScene = sceneAsset;
        }
    }
}
#endif
