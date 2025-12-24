using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Scene names (must be added to Build Settings)")]
    [SerializeField] private string interactiveSceneName = "Interactive";

    [Header("Layout")]
    [SerializeField] private float buttonWidth = 320f;
    [SerializeField] private float buttonHeight = 48f;
    [SerializeField] private float verticalSpacing = 12f;

    private void Awake()
    {
        EnsureCamera();
    }

    private static void EnsureCamera()
    {
        if (Camera.main != null)
        {
            if (Camera.main.GetComponent<AudioListener>() == null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }
            return;
        }

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";

        var cam = cameraGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 3.2f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        cameraGo.AddComponent<AudioListener>();
    }

    private void OnGUI()
    {
        const float topPadding = 24f;

        var centerX = (Screen.width - buttonWidth) * 0.5f;
        var y = topPadding;

        GUI.Label(new Rect(centerX, y, buttonWidth, 32f), "Line Drawing Algorithms");
        y += 44f;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "DDA"))
        {
            LoadInteractive(LineAlgorithm.DDA);
        }
        y += buttonHeight + verticalSpacing;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "Bresenham"))
        {
            LoadInteractive(LineAlgorithm.Bresenham);
        }
        y += buttonHeight + verticalSpacing;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "Bresenham (All Slopes)"))
        {
            LoadInteractive(LineAlgorithm.BresenhamFull);
        }
    }

    private void LoadInteractive(LineAlgorithm algorithm)
    {
        if (string.IsNullOrWhiteSpace(interactiveSceneName))
        {
            Debug.LogError("Interactive scene name is empty.");
            return;
        }

        InteractiveLineDemo.SetSelectedAlgorithm(algorithm);
        SceneManager.LoadScene(interactiveSceneName);
    }
}
