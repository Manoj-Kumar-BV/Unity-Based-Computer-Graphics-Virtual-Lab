using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractiveCircleDemo : MonoBehaviour
{
    private const string CircleClipName = "Midpoint Circle Algorithm";

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Canvas")]
    [SerializeField] private int resolution = 64;
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color pointColor = Color.yellow;
    [SerializeField] private Color circleColor = Color.red;

    [Header("Animation")]
    [SerializeField] private bool animate = true;
    [SerializeField] private float drawDurationSeconds = 0.6f;
    [SerializeField] private int pixelsPerFrame = 16;

    [Header("Education")]
    [SerializeField] private bool showCalculations = true;

    [Header("Quad")]
    [SerializeField] private float quadSize = 5f;

    [Header("Audio (optional)")]
    [SerializeField] private bool audioEnabled = true;
    [SerializeField] private bool autoPlayExplanationOnStart = true;
    [SerializeField] private AudioClip circleExplanation;

    private GameObject _quad;
    private Texture2D _texture;
    private AudioSource _audioSource;

    private Vector2Int? _center;
    private Vector2Int? _radiusPoint;
    private bool _isDrawing;

    private string _status = "Click the circle center.";
    private string _lastCalcText;
    private int _drawProgress;
    private int _drawTotal;

    private void Awake()
    {
        EnsureCamera();
        EnsureQuadAndTexture();
        EnsureAudio();

        if (circleExplanation == null)
        {
            circleExplanation = LoadClipFromResourcesByName(CircleClipName);
        }

        ClearTexture();

        if (audioEnabled && autoPlayExplanationOnStart)
        {
            PlayExplanation();
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (TryGetPixelFromMouse(out var pixel))
            {
                OnPixelClicked(pixel);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void OnGUI()
    {
        const float pad = 16f;
        const float w = 460f;
        const float h = 28f;

        var x = pad;
        var y = pad;

        GUI.Label(new Rect(x, y, w, h), "Algorithm: Midpoint Circle Drawing");
        y += h;

        GUI.Label(new Rect(x, y, w, 6f * h), GetDescriptionText());
        y += 6f * h;

        if (audioEnabled && circleExplanation == null)
        {
            GUI.Label(new Rect(x, y, w, h), "Audio: missing clip (put mp3 under Assets/Resources/LineDrawingAlgorithm/ with name 'Midpoint Circle Algorithm')");
        }
        else
        {
            GUI.Label(new Rect(x, y, w, h), audioEnabled ? "Audio: on" : "Audio: off");
        }
        y += h;

        GUI.Label(new Rect(x, y, w, h), _status);
        y += h;

        showCalculations = GUI.Toggle(new Rect(x, y, 180f, h), showCalculations, "Show calculations");
        y += h;

        if (showCalculations)
        {
            if (!string.IsNullOrWhiteSpace(_lastCalcText))
            {
                GUI.Label(new Rect(x, y, w, 6f * h), _lastCalcText);
                y += 6f * h;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, h), "Draw a circle to see the math.");
                y += h;
            }

            if (_isDrawing && _drawTotal > 0)
            {
                GUI.Label(new Rect(x, y, w, h), $"Progress: {_drawProgress}/{_drawTotal} pixels");
                y += h;
            }
        }

        if (GUI.Button(new Rect(x, y, 140f, 34f), "Clear"))
        {
            ResetAll();
        }

        audioEnabled = GUI.Toggle(new Rect(x + 152f, y + 8f, 160f, 34f), audioEnabled, "Audio On");

        if (GUI.Button(new Rect(x + 324f, y, 140f, 34f), "Play"))
        {
            PlayExplanation();
        }
        y += 42f;

        if (GUI.Button(new Rect(x, y, 140f, 34f), "Back"))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void ResetAll()
    {
        if (_isDrawing)
        {
            return;
        }

        _center = null;
        _radiusPoint = null;
        _status = "Click the circle center.";
        _lastCalcText = null;
        _drawProgress = 0;
        _drawTotal = 0;
        ClearTexture();
    }

    private void OnPixelClicked(Vector2Int pixel)
    {
        if (_isDrawing)
        {
            return;
        }

        if (_center == null)
        {
            _center = pixel;
            PlotPoint(pixel, pointColor);
            _texture.Apply();
            _status = $"Center set at ({pixel.x}, {pixel.y}). Click a point on the radius.";
            return;
        }

        _radiusPoint = pixel;
        PlotPoint(pixel, pointColor);
        _texture.Apply();

        var center = _center.Value;
        var r = Mathf.RoundToInt(Vector2.Distance(new Vector2(center.x, center.y), new Vector2(pixel.x, pixel.y)));
        r = Mathf.Clamp(r, 0, resolution - 1);

        var pixels = GetCirclePixelsMidpoint(center, r, out var calc);
        _lastCalcText = calc;
        _drawProgress = 0;
        _drawTotal = pixels.Count;

        if (!animate)
        {
            DrawPixelsImmediate(pixels, circleColor);
            _texture.Apply();
            _status = $"Drew circle (r={r}). Click the next center.";
            _center = null;
            _radiusPoint = null;
            _drawProgress = _drawTotal;
            return;
        }

        StartCoroutine(AnimateDrawPixels(pixels, r));
    }

    private IEnumerator AnimateDrawPixels(List<Vector2Int> pixels, int r)
    {
        _isDrawing = true;
        _status = $"Drawing circle (r={r})…";

        var total = Mathf.Max(1, pixels.Count);
        var duration = Mathf.Max(0.05f, drawDurationSeconds);
        var delayPerBatch = duration / Mathf.Max(1, total / Mathf.Max(1, pixelsPerFrame));

        for (var i = 0; i < pixels.Count; i++)
        {
            PlotPoint(pixels[i], circleColor);
            _drawProgress = i + 1;

            if ((i + 1) % Mathf.Max(1, pixelsPerFrame) == 0)
            {
                _texture.Apply();
                yield return new WaitForSeconds(delayPerBatch);
            }
        }

        _texture.Apply();
        _status = $"Done. Click the next center.";
        _center = null;
        _radiusPoint = null;
        _isDrawing = false;
    }

    private static List<Vector2Int> GetCirclePixelsMidpoint(Vector2Int center, int r, out string calcText)
    {
        var unique = new HashSet<Vector2Int>();
        var ordered = new List<Vector2Int>();

        void AddSym(int x, int y)
        {
            var cx = center.x;
            var cy = center.y;

            var pts = new[]
            {
                new Vector2Int(cx + x, cy + y),
                new Vector2Int(cx + y, cy + x),
                new Vector2Int(cx - y, cy + x),
                new Vector2Int(cx - x, cy + y),
                new Vector2Int(cx - x, cy - y),
                new Vector2Int(cx - y, cy - x),
                new Vector2Int(cx + y, cy - x),
                new Vector2Int(cx + x, cy - y),
            };

            for (var i = 0; i < pts.Length; i++)
            {
                if (unique.Add(pts[i]))
                {
                    ordered.Add(pts[i]);
                }
            }
        }

        var x0 = 0;
        var y0 = r;
        var p = 1 - r;

        AddSym(x0, y0);

        while (x0 < y0)
        {
            x0++;
            if (p < 0)
            {
                p = p + 2 * x0 + 1;
            }
            else
            {
                y0--;
                p = p + 2 * x0 + 1 - 2 * y0;
            }

            AddSym(x0, y0);
        }

        calcText =
            "Calculations (Midpoint Circle):\n" +
            $"r = {r}\n" +
            "start: x = 0, y = r\n" +
            $"p0 = 1 - r = {1 - r}\n" +
            "rule: if p < 0 then p = p + 2x + 1\n" +
            "else y = y - 1 and p = p + 2x + 1 - 2y\n" +
            $"plotted pixels = {ordered.Count} (with 8-way symmetry)";

        return ordered;
    }

    private static string GetDescriptionText()
    {
        return
            "The Midpoint Circle algorithm draws a circle using only integer arithmetic and symmetry.\n" +
            "It starts at (0, r) and decides at each step whether to move East or South-East based on a decision parameter p.\n" +
            "Using 8-way symmetry, each computed point is mirrored into all octants to complete the circle.\n\n" +
            "How to use: click the center, then click any point on the radius.";
    }

    private void EnsureCamera()
    {
        if (Camera.main != null)
        {
            EnsureSingleAudioListener(Camera.main.gameObject);
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

        EnsureSingleAudioListener(cameraGo);
    }

    private static void EnsureSingleAudioListener(GameObject target)
    {
        var existing = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var listener in existing)
        {
            if (listener != null)
            {
                listener.enabled = false;
            }
        }

        var audioListener = target.GetComponent<AudioListener>();
        if (audioListener == null)
        {
            audioListener = target.AddComponent<AudioListener>();
        }
        audioListener.enabled = true;
    }

    private void EnsureQuadAndTexture()
    {
        if (_quad == null)
        {
            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "CircleCanvas";
            _quad.transform.position = Vector3.zero;
            _quad.transform.localScale = new Vector3(quadSize, quadSize, 1f);
        }

        _texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        _texture.filterMode = FilterMode.Point;
        _texture.wrapMode = TextureWrapMode.Clamp;

        var shader = FindFirstAvailableShader(
            "Unlit/Texture",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default"
        );

        if (shader == null)
        {
            Debug.LogError("Could not find a suitable Unlit shader to display the texture.");
            return;
        }

        var mat = new Material(shader);
        mat.mainTexture = _texture;

        var renderer = _quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
    }

    private void EnsureAudio()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 1f;
    }

    private void PlayExplanation()
    {
        if (!audioEnabled)
        {
            return;
        }

        if (_audioSource == null)
        {
            return;
        }

        if (circleExplanation == null)
        {
            return;
        }

        _audioSource.Stop();
        _audioSource.clip = circleExplanation;
        _audioSource.Play();
    }

    private static Shader FindFirstAvailableShader(params string[] names)
    {
        foreach (var name in names)
        {
            var shader = Shader.Find(name);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private bool TryGetPixelFromMouse(out Vector2Int pixel)
    {
        pixel = default;

        var cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit))
        {
            return false;
        }

        if (hit.collider == null || hit.collider.gameObject != _quad)
        {
            return false;
        }

        var uv = hit.textureCoord;

        var x = Mathf.Clamp((int)(uv.x * resolution), 0, resolution - 1);
        var y = Mathf.Clamp((int)(uv.y * resolution), 0, resolution - 1);

        pixel = new Vector2Int(x, y);
        return true;
    }

    private void ClearTexture()
    {
        if (_texture == null)
        {
            return;
        }

        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                _texture.SetPixel(x, y, backgroundColor);
            }
        }

        _texture.Apply();
    }

    private void PlotPoint(Vector2Int p, Color c)
    {
        if (p.x < 0 || p.y < 0 || p.x >= resolution || p.y >= resolution)
        {
            return;
        }

        _texture.SetPixel(p.x, p.y, c);
    }

    private void DrawPixelsImmediate(List<Vector2Int> pixels, Color c)
    {
        for (var i = 0; i < pixels.Count; i++)
        {
            PlotPoint(pixels[i], c);
        }
    }

    private static AudioClip LoadClipFromResourcesByName(string clipName)
    {
        var clip = Resources.Load<AudioClip>($"LineDrawingAlgorithm/{clipName}");
        clip ??= Resources.Load<AudioClip>($"Audio/{clipName}");
        clip ??= Resources.Load<AudioClip>(clipName);
        if (clip != null)
        {
            return clip;
        }

        var all = Resources.LoadAll<AudioClip>(string.Empty);
        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] != null && string.Equals(all[i].name, clipName, StringComparison.OrdinalIgnoreCase))
            {
                return all[i];
            }
        }

        return null;
    }
}
