#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartScene
{
    private const string OpeningScenePath = "Assets/LineDrawingAlgorithm/Examples/Opening/Opening.unity";

    static PlayModeStartScene()
    {
        EditorApplication.delayCall += Configure;
    }

    private static void Configure()
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(OpeningScenePath);
        if (sceneAsset != null)
        {
            EditorSceneManager.playModeStartScene = sceneAsset;
        }
    }
}
#endif
