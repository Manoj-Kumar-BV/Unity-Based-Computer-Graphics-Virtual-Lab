using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class InteractiveLineDemo : MonoBehaviour
{
    private const string SelectedAlgorithmKey = "LineDrawing.SelectedAlgorithm";

    private const string DdaClipName = "DDA Algorithm";
    private const string BresenhamClipName = "Bresenham's Line Algorithm";

#if UNITY_EDITOR
    private const string DdaClipAssetPath = "Assets/LineDrawingAlgorithm/DDA Algorithm.mp3";
    private const string BresenhamClipAssetPath = "Assets/LineDrawingAlgorithm/Bresenham's Line Algorithm.mp3";
#endif

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Canvas")]
    [SerializeField] private int resolution = 64;
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color pointColor = Color.yellow;
    [SerializeField] private Color lineColor = Color.red;

    [Header("Animation")]
    [Tooltip("When true, the final line is drawn gradually after the 2nd click.")]
    [SerializeField] private bool animateLineDrawing = true;
    [Tooltip("Approximate total time (seconds) to draw a line.")]
    [SerializeField] private float lineDrawDurationSeconds = 0.5f;
    [Tooltip("How many pixels to draw per frame while animating.")]
    [SerializeField] private int pixelsPerFrame = 12;

    [Header("Education")]
    [SerializeField] private bool showCalculations = true;

    [Header("Quad")]
    [SerializeField] private float quadSize = 5f;

    [Header("Audio (optional)")]
    [SerializeField] private bool audioEnabled = true;
    [SerializeField] private bool autoPlayExplanationOnStart = true;
    [SerializeField] private AudioClip ddaExplanation;
    [SerializeField] private AudioClip bresenhamExplanation;
    [SerializeField] private AudioClip bresenhamAllSlopesExplanation;

    private GameObject _quad;
    private Texture2D _texture;
    private Vector2Int? _p1;
    private Vector2Int? _p2;
    private Vector2Int? _hoverPixel;
    private AudioSource _audioSource;
    private bool _isDrawing;

    private bool _hasLastLine;
    private Vector2Int _lastP1;
    private Vector2Int _lastP2;
    private string _lastCalcText;
    private int _drawProgress;
    private int _drawTotal;

    private LineAlgorithm _algorithm;
    private string _status = "Click the first point.";

    private void Awake()
    {
        _algorithm = (LineAlgorithm)PlayerPrefs.GetInt(SelectedAlgorithmKey, (int)LineAlgorithm.DDA);

        EnsureCamera();
        EnsureQuadAndTexture();
        EnsureAudio();
        AutoAssignAudioClipsIfMissing();
        ClearTexture();

        if (audioEnabled && autoPlayExplanationOnStart)
        {
            PlayExplanation();
        }
    }

    private void AutoAssignAudioClipsIfMissing()
    {
        // Priority order:
        // 1) Inspector assignment (scene/prefab)
        // 2) Editor-only AssetDatabase (convenience while developing)
        // 3) Runtime Resources (works in Windows builds)

        if (ddaExplanation == null)
        {
#if UNITY_EDITOR
            ddaExplanation = AssetDatabase.LoadAssetAtPath<AudioClip>(DdaClipAssetPath);
#endif
            ddaExplanation ??= LoadClipFromResourcesByName(DdaClipName);
        }

        if (bresenhamExplanation == null)
        {
#if UNITY_EDITOR
            bresenhamExplanation = AssetDatabase.LoadAssetAtPath<AudioClip>(BresenhamClipAssetPath);
#endif
            bresenhamExplanation ??= LoadClipFromResourcesByName(BresenhamClipName);
        }

        // Reuse the same clip for the all-slopes variant unless a dedicated clip is provided.
        bresenhamAllSlopesExplanation ??= bresenhamExplanation;
    }

    private static AudioClip LoadClipFromResourcesByName(string clipName)
    {
        // Works only if clips are under an Assets/Resources folder.
        // Fast path: try common subfolders.
        var clip = Resources.Load<AudioClip>($"LineDrawingAlgorithm/{clipName}");
        clip ??= Resources.Load<AudioClip>($"Audio/{clipName}");
        clip ??= Resources.Load<AudioClip>(clipName);
        if (clip != null)
        {
            return clip;
        }

        // Fallback: scan all resources audio clips (fine for this small project).
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

    private void Update()
    {
        if (TryGetPixelFromMouse(out var hover))
        {
            _hoverPixel = hover;
        }
        else
        {
            _hoverPixel = null;
        }

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
        const float w = 420f;
        const float h = 28f;

        var x = pad;
        var y = pad;

        GUI.Label(new Rect(x, y, w, h), $"Algorithm: {GetAlgorithmLabel(_algorithm)}");
        y += h;

        var description = GetAlgorithmDescription(_algorithm);
        var descriptionHeight = 5f * h;
        GUI.Label(new Rect(x, y, w, descriptionHeight), description);
        y += descriptionHeight;

        if (audioEnabled && GetExplanationClip(_algorithm) == null)
        {
            GUI.Label(new Rect(x, y, w, h), "Audio: missing clip (assign in Inspector or put mp3 under Assets/Resources)");
        }
        else
        {
            GUI.Label(new Rect(x, y, w, h), audioEnabled ? "Audio: on" : "Audio: off");
        }
        y += h;

        if (_hoverPixel.HasValue)
        {
            var p = _hoverPixel.Value;
            GUI.Label(new Rect(x, y, w, h), $"Hover: ({p.x}, {p.y})");
        }
        else
        {
            GUI.Label(new Rect(x, y, w, h), "Hover: (outside canvas)");
        }
        y += h;

        GUI.Label(new Rect(x, y, w, h), _status);
        y += h;

        showCalculations = GUI.Toggle(new Rect(x, y, 180f, h), showCalculations, "Show calculations");
        y += h;

        if (showCalculations)
        {
            if (_hasLastLine && !string.IsNullOrWhiteSpace(_lastCalcText))
            {
                var calcHeight = 6f * h;
                GUI.Label(new Rect(x, y, w, calcHeight), _lastCalcText);
                y += calcHeight;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, h), "Draw a line to see the math.");
                y += h;
            }

            if (_isDrawing && _drawTotal > 0)
            {
                GUI.Label(new Rect(x, y, w, h), $"Progress: {_drawProgress}/{_drawTotal} pixels");
                y += h;
            }
        }

        GUI.Label(new Rect(x, y, w, h), _isDrawing ? "Tip: drawing…" : "Tip: press Esc to go back.");
        y += h + 8f;

        if (GUI.Button(new Rect(x, y, 140f, 34f), "Clear"))
        {
            _p1 = null;
            _p2 = null;
            _status = "Click the first point.";
            _hasLastLine = false;
            _lastCalcText = null;
            ClearTexture();
        }

        audioEnabled = GUI.Toggle(new Rect(x + 152f, y + 8f, 160f, 34f), audioEnabled, "Audio On");
        y += 42f;

        if (GUI.Button(new Rect(x, y, 140f, 34f), "Back"))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void OnPixelClicked(Vector2Int pixel)
    {
        if (_isDrawing)
        {
            return;
        }

        if (_p1 == null)
        {
            _p1 = pixel;
            PlotPoint(pixel);
            _texture.Apply();
            _status = $"First point set at ({pixel.x}, {pixel.y}). Click the second point.";
            return;
        }

        _p2 = pixel;
        PlotPoint(pixel);

        var p1 = _p1.Value;
        var p2 = _p2.Value;

        if (!TryGetLinePixels(p1, p2, out var pixels))
        {
            _status = "This algorithm can't draw that slope/direction. Try Bresenham (All Slopes) or DDA.";
            _p1 = null;
            _p2 = null;
            _texture.Apply();
            return;
        }

        _hasLastLine = true;
        _lastP1 = p1;
        _lastP2 = p2;
        _lastCalcText = BuildCalculationText(p1, p2, pixels);
        _drawProgress = 0;
        _drawTotal = pixels.Count;

        if (!animateLineDrawing)
        {
            DrawPixelsImmediate(pixels, lineColor);
            _texture.Apply();
            _status = $"Drew line from ({p1.x}, {p1.y}) to ({p2.x}, {p2.y}). Click the next first point.";
            _p1 = null;
            _p2 = null;
            _drawProgress = _drawTotal;
            return;
        }

        StartCoroutine(AnimateDrawLine(p1, p2, pixels));
    }

    private bool TryGetLinePixels(Vector2Int p1, Vector2Int p2, out List<Vector2Int> pixels)
    {
        pixels = null;

        switch (_algorithm)
        {
            case LineAlgorithm.DDA:
                pixels = GetLinePixelsDDA(p1, p2);
                return true;

            case LineAlgorithm.Bresenham:
                return TryGetLinePixelsBresenhamOctant1(p1, p2, out pixels);

            case LineAlgorithm.BresenhamFull:
                pixels = GetLinePixelsBresenhamAllSlopes(p1, p2);
                return true;

            default:
                pixels = GetLinePixelsDDA(p1, p2);
                return true;
        }
    }

    private static List<Vector2Int> GetLinePixelsDDA(Vector2Int p1, Vector2Int p2)
    {
        var pixels = new List<Vector2Int>();

        var dx = p2.x - p1.x;
        var dy = p2.y - p1.y;

        var steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps == 0)
        {
            pixels.Add(p1);
            return pixels;
        }

        var xIncrement = dx / (float)steps;
        var yIncrement = dy / (float)steps;

        var x = (float)p1.x;
        var y = (float)p1.y;
        for (var i = 0; i <= steps; i++)
        {
            pixels.Add(new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y)));
            x += xIncrement;
            y += yIncrement;
        }

        return pixels;
    }

    private bool TryGetLinePixelsBresenhamOctant1(Vector2Int p1, Vector2Int p2, out List<Vector2Int> pixels)
    {
        pixels = null;

        // The simple implementation in LineDrawing.Bresenham assumes:
        // - left to right (x1 <= x2)
        // - slope between 0 and 1 (0 <= dy <= dx)
        // - dy >= 0
        if (p1.x > p2.x)
        {
            (p1, p2) = (p2, p1);
        }

        var dx = p2.x - p1.x;
        var dy = p2.y - p1.y;

        if (dx <= 0)
        {
            return false;
        }

        if (dy < 0 || dy > dx)
        {
            return false;
        }

        pixels = new List<Vector2Int>();

        var pk = 2 * dy - dx;
        var y = p1.y;

        for (int x = p1.x; x <= p2.x; x++)
        {
            pixels.Add(new Vector2Int(x, y));
            if (pk > 0)
            {
                y++;
                pk = pk - 2 * dx;
            }
            pk = pk + 2 * dy;
        }

        return true;
    }

    private static List<Vector2Int> GetLinePixelsBresenhamAllSlopes(Vector2Int p1, Vector2Int p2)
    {
        // Mirrors the logic from the existing BresenhamFull example.
        var pixels = new List<Vector2Int>();

        if (Math.Abs(p2.y - p1.y) < Math.Abs(p2.x - p1.x))
        {
            if (p1.x > p2.x)
            {
                AddBresenhamLowPixels(p2, p1, pixels);
            }
            else
            {
                AddBresenhamLowPixels(p1, p2, pixels);
            }
        }
        else
        {
            if (p1.y > p2.y)
            {
                AddBresenhamHighPixels(p2, p1, pixels);
            }
            else
            {
                AddBresenhamHighPixels(p1, p2, pixels);
            }
        }

        return pixels;
    }

    private static void AddBresenhamLowPixels(Vector2Int p1, Vector2Int p2, List<Vector2Int> pixels)
    {
        var dx = p2.x - p1.x;
        var dy = p2.y - p1.y;
        var yi = 1;
        if (dy < 0)
        {
            yi = -1;
            dy = -dy;
        }
        var d = 2 * dy - dx;
        var y = p1.y;

        for (var x = p1.x; x <= p2.x; x++)
        {
            pixels.Add(new Vector2Int(x, y));
            if (d > 0)
            {
                y += yi;
                d += 2 * (dy - dx);
            }
            else
            {
                d += 2 * dy;
            }
        }
    }

    private static void AddBresenhamHighPixels(Vector2Int p1, Vector2Int p2, List<Vector2Int> pixels)
    {
        var dx = p2.x - p1.x;
        var dy = p2.y - p1.y;
        var xi = 1;
        if (dx < 0)
        {
            xi = -1;
            dx = -dx;
        }
        var d = 2 * dx - dy;
        var x = p1.x;

        for (var y = p1.y; y <= p2.y; y++)
        {
            pixels.Add(new Vector2Int(x, y));
            if (d > 0)
            {
                x += xi;
                d += 2 * (dx - dy);
            }
            else
            {
                d += 2 * dx;
            }
        }
    }

    private void DrawPixelsImmediate(List<Vector2Int> pixels, Color color)
    {
        for (var i = 0; i < pixels.Count; i++)
        {
            var p = pixels[i];
            if (p.x < 0 || p.y < 0 || p.x >= resolution || p.y >= resolution)
            {
                continue;
            }
            _texture.SetPixel(p.x, p.y, color);
        }
    }

    private IEnumerator AnimateDrawLine(Vector2Int p1, Vector2Int p2, List<Vector2Int> pixels)
    {
        _isDrawing = true;
        _status = $"Drawing from ({p1.x}, {p1.y}) to ({p2.x}, {p2.y})…";

        _drawProgress = 0;
        _drawTotal = pixels.Count;

        var total = Mathf.Max(1, pixels.Count);
        var duration = Mathf.Max(0.05f, lineDrawDurationSeconds);
        var delayPerBatch = duration / Mathf.Max(1, total / Mathf.Max(1, pixelsPerFrame));

        for (var i = 0; i < pixels.Count; i++)
        {
            var p = pixels[i];
            if (p.x >= 0 && p.y >= 0 && p.x < resolution && p.y < resolution)
            {
                _texture.SetPixel(p.x, p.y, lineColor);
            }

            _drawProgress = i + 1;

            var isBatchEnd = (i % Mathf.Max(1, pixelsPerFrame)) == 0;
            if (isBatchEnd)
            {
                _texture.Apply();
                yield return new WaitForSeconds(delayPerBatch);
            }
        }

        _texture.Apply();
        _status = $"Drew line from ({p1.x}, {p1.y}) to ({p2.x}, {p2.y}). Click the next first point.";
        _p1 = null;
        _p2 = null;
        _isDrawing = false;
    }

    private string BuildCalculationText(Vector2Int p1, Vector2Int p2, List<Vector2Int> pixels)
    {
        var dx = p2.x - p1.x;
        var dy = p2.y - p1.y;

        switch (_algorithm)
        {
            case LineAlgorithm.DDA:
            {
                var steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                var xInc = steps == 0 ? 0f : dx / (float)steps;
                var yInc = steps == 0 ? 0f : dy / (float)steps;
                return
                    "Calculations (DDA):\n" +
                    $"Δx = {dx}, Δy = {dy}\n" +
                    $"steps = max(|Δx|,|Δy|) = {steps}\n" +
                    $"x_inc = Δx/steps = {xInc:0.###}\n" +
                    $"y_inc = Δy/steps = {yInc:0.###}\n" +
                    $"plotted pixels ≈ {pixels.Count}";
            }

            case LineAlgorithm.Bresenham:
            {
                // The octant-1 version swaps endpoints if needed.
                var a = p1;
                var b = p2;
                var swapped = false;
                if (a.x > b.x)
                {
                    (a, b) = (b, a);
                    swapped = true;
                }

                var ddx = b.x - a.x;
                var ddy = b.y - a.y;
                var p0 = 2 * ddy - ddx;

                return
                    "Calculations (Bresenham - Octant 1):\n" +
                    $"Δx = {ddx}, Δy = {ddy}" + (swapped ? " (points swapped)\n" : "\n") +
                    $"p0 = 2Δy - Δx = {p0}\n" +
                    "rule: if p >= 0 then y++ and p += 2Δy - 2Δx\n" +
                    "else p += 2Δy\n" +
                    $"plotted pixels = {pixels.Count}";
            }

            case LineAlgorithm.BresenhamFull:
            {
                var absDx = Mathf.Abs(dx);
                var absDy = Mathf.Abs(dy);
                var isLow = absDy < absDx;

                if (isLow)
                {
                    var a = p1;
                    var b = p2;
                    var swapped = false;
                    if (a.x > b.x)
                    {
                        (a, b) = (b, a);
                        swapped = true;
                    }
                    var ddx = b.x - a.x;
                    var ddy = b.y - a.y;
                    var yi = ddy < 0 ? -1 : 1;
                    var ddyAbs = Math.Abs(ddy);
                    var d0 = 2 * ddyAbs - ddx;

                    return
                        "Calculations (Bresenham - All Slopes):\n" +
                        $"case: |Δy| < |Δx| (step in x)" + (swapped ? " (points swapped)\n" : "\n") +
                        $"Δx = {ddx}, Δy = {ddy}, yi = {yi}\n" +
                        $"D0 = 2|Δy| - Δx = {d0}\n" +
                        "rule: if D > 0 then y += yi and D += 2(|Δy| - Δx)\n" +
                        "else D += 2|Δy|\n" +
                        $"plotted pixels = {pixels.Count}";
                }
                else
                {
                    var a = p1;
                    var b = p2;
                    var swapped = false;
                    if (a.y > b.y)
                    {
                        (a, b) = (b, a);
                        swapped = true;
                    }

                    var ddxRaw = b.x - a.x;
                    var ddy = b.y - a.y;
                    var xi = ddxRaw < 0 ? -1 : 1;
                    var ddx = Math.Abs(ddxRaw);
                    var d0 = 2 * ddx - ddy;

                    return
                        "Calculations (Bresenham - All Slopes):\n" +
                        $"case: |Δy| >= |Δx| (step in y)" + (swapped ? " (points swapped)\n" : "\n") +
                        $"Δx = {ddxRaw}, Δy = {ddy}, xi = {xi}\n" +
                        $"D0 = 2|Δx| - Δy = {d0}\n" +
                        "rule: if D > 0 then x += xi and D += 2(|Δx| - Δy)\n" +
                        "else D += 2|Δx|\n" +
                        $"plotted pixels = {pixels.Count}";
                }
            }

            default:
                return $"Δx = {dx}, Δy = {dy}";
        }
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
            _quad.name = "LineCanvas";
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

        var clip = GetExplanationClip(_algorithm);
        if (clip == null)
        {
            return;
        }

        _audioSource.Stop();
        _audioSource.clip = clip;
        _audioSource.Play();
    }

    private AudioClip GetExplanationClip(LineAlgorithm algorithm)
    {
        return algorithm switch
        {
            LineAlgorithm.DDA => ddaExplanation,
            LineAlgorithm.Bresenham => bresenhamExplanation,
            LineAlgorithm.BresenhamFull => bresenhamAllSlopesExplanation != null ? bresenhamAllSlopesExplanation : bresenhamExplanation,
            _ => null,
        };
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

    private void PlotPoint(Vector2Int p)
    {
        if (_texture == null)
        {
            return;
        }

        _texture.SetPixel(p.x, p.y, pointColor);
    }


    private static string GetAlgorithmLabel(LineAlgorithm algorithm)
    {
        return algorithm switch
        {
            LineAlgorithm.DDA => "DDA",
            LineAlgorithm.Bresenham => "Bresenham (Octant 1)",
            LineAlgorithm.BresenhamFull => "Bresenham (All Slopes)",
            _ => algorithm.ToString(),
        };
    }

    private static string GetAlgorithmDescription(LineAlgorithm algorithm)
    {
        return algorithm switch
        {
            LineAlgorithm.DDA =>
                "DDA (Digital Differential Analyzer) steps from the start point to the end point in small increments.\n" +
                "It uses floating-point math and rounds to the nearest pixel each step.\n" +
                "Easy to understand and works for any slope, but can be slower and may look less crisp.\n" +
                "Goal: see how continuous lines become pixels.",

            LineAlgorithm.Bresenham =>
                "Bresenham uses only integer math to decide which pixel is closest to the ideal line.\n" +
                "This version is the simplest form and only works reliably in one direction/octant.\n" +
                "If your line is steep or goes the other way, try ‘All Slopes’.\n" +
                "Goal: see how an error term guides pixel choices.",

            LineAlgorithm.BresenhamFull =>
                "Bresenham (All Slopes) is the practical version: it handles steep/shallow and both directions.\n" +
                "It chooses between two variants depending on whether the line is more horizontal or vertical.\n" +
                "Still integer-based and efficient.\n" +
                "Goal: learn how symmetry splits the problem into cases.",

            _ => "",
        };
    }

    public static void SetSelectedAlgorithm(LineAlgorithm algorithm)
    {
        PlayerPrefs.SetInt(SelectedAlgorithmKey, (int)algorithm);
        PlayerPrefs.Save();
    }
}
