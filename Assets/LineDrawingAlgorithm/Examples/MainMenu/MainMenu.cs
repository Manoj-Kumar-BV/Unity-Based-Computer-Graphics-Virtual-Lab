using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    private const string SelectedModuleKey = "VirtualLab.SelectedModule";
    private const string SelectedLineAlgorithmKey = "LineDrawing.SelectedAlgorithm";
    private const string SelectedClippingAlgorithmKey = "Clipping.SelectedAlgorithm";

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

        GUI.Label(new Rect(centerX, y, buttonWidth, 32f), "Computer Graphics Virtual Lab");
        y += 44f;

        GUI.Label(new Rect(centerX, y, buttonWidth, 32f), "Line Drawing");
        y += 40f;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "DDA"))
        {
            LoadLineDrawing(LineAlgorithm.DDA);
        }
        y += buttonHeight + verticalSpacing;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "Bresenham"))
        {
            LoadLineDrawing(LineAlgorithm.Bresenham);
        }
        y += buttonHeight + verticalSpacing;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "Bresenham (All Slopes)"))
        {
            LoadLineDrawing(LineAlgorithm.BresenhamFull);
        }

        y += buttonHeight + 24f;
        GUI.Label(new Rect(centerX, y, buttonWidth, 32f), "Circle Drawing");
        y += 40f;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "Midpoint Circle"))
        {
            LoadModule(VirtualLabModule.CircleDrawing);
        }

        y += buttonHeight + 24f;
        GUI.Label(new Rect(centerX, y, buttonWidth, 32f), "Line Clipping");
        y += 40f;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "Cohen–Sutherland"))
        {
            LoadClipping(ClippingAlgorithm.CohenSutherland);
        }
        y += buttonHeight + verticalSpacing;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "Liang–Barsky"))
        {
            LoadClipping(ClippingAlgorithm.LiangBarsky);
        }

        y += buttonHeight + 24f;
        GUI.Label(new Rect(centerX, y, buttonWidth, 32f), "Polygon Fill");
        y += 40f;

        if (GUI.Button(new Rect(centerX, y, buttonWidth, buttonHeight), "Scanline Fill"))
        {
            LoadModule(VirtualLabModule.PolygonFill);
        }
    }

    private void LoadLineDrawing(LineAlgorithm algorithm)
    {
        if (string.IsNullOrWhiteSpace(interactiveSceneName))
        {
            Debug.LogError("Interactive scene name is empty.");
            return;
        }

        PlayerPrefs.SetInt(SelectedModuleKey, (int)VirtualLabModule.LineDrawing);
        PlayerPrefs.SetInt(SelectedLineAlgorithmKey, (int)algorithm);
        PlayerPrefs.Save();
        SceneManager.LoadScene(interactiveSceneName);
    }

    private void LoadClipping(ClippingAlgorithm algorithm)
    {
        if (string.IsNullOrWhiteSpace(interactiveSceneName))
        {
            Debug.LogError("Interactive scene name is empty.");
            return;
        }

        PlayerPrefs.SetInt(SelectedModuleKey, (int)VirtualLabModule.LineClipping);
        PlayerPrefs.SetInt(SelectedClippingAlgorithmKey, (int)algorithm);
        PlayerPrefs.Save();
        SceneManager.LoadScene(interactiveSceneName);
    }

    private void LoadModule(VirtualLabModule module)
    {
        if (string.IsNullOrWhiteSpace(interactiveSceneName))
        {
            Debug.LogError("Interactive scene name is empty.");
            return;
        }

        PlayerPrefs.SetInt(SelectedModuleKey, (int)module);
        PlayerPrefs.Save();
        SceneManager.LoadScene(interactiveSceneName);
    }
}
