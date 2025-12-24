using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractiveClippingDemo : MonoBehaviour
{
    private const string SelectedAlgorithmKey = "Clipping.SelectedAlgorithm";

    private const string CohenClipName = "Cohen Sutherland Line Clipping";
    private const string LiangClipName = "Liang Barsky Line Clipping";

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Canvas")]
    [SerializeField] private int resolution = 64;
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color pointColor = Color.yellow;
    [SerializeField] private Color lineColor = Color.red;

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
    [SerializeField] private AudioClip cohenExplanation;
    [SerializeField] private AudioClip liangExplanation;

    private GameObject _quad;
    private Texture2D _texture;
    private AudioSource _audioSource;

    private Vector2Int? _w1;
    private Vector2Int? _w2;
    private Vector2Int? _p1;
    private Vector2Int? _p2;

    private bool _isDrawing;
    private string _status = "Click window corner 1.";
    private string _lastCalcText;
    private int _drawProgress;
    private int _drawTotal;

    private ClippingAlgorithm _algorithm;

    public static void SetSelectedAlgorithm(ClippingAlgorithm algorithm)
    {
        PlayerPrefs.SetInt(SelectedAlgorithmKey, (int)algorithm);
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        _algorithm = (ClippingAlgorithm)PlayerPrefs.GetInt(SelectedAlgorithmKey, (int)ClippingAlgorithm.CohenSutherland);

        EnsureCamera();
        EnsureQuadAndTexture();
        EnsureAudio();

        cohenExplanation ??= LoadClipFromResourcesByName(CohenClipName);
        liangExplanation ??= LoadClipFromResourcesByName(LiangClipName);

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
        const float w = 520f;
        const float h = 28f;

        var x = pad;
        var y = pad;

        GUI.Label(new Rect(x, y, w, h), $"Algorithm: {GetAlgorithmLabel(_algorithm)}");
        y += h;

        GUI.Label(new Rect(x, y, w, 7f * h), GetDescriptionText(_algorithm));
        y += 7f * h;

        if (audioEnabled && GetClipForAlgorithm(_algorithm) == null)
        {
            GUI.Label(new Rect(x, y, w, h), "Audio: missing clip (put mp3 under Assets/Resources/LineDrawingAlgorithm/ with the algorithm name)");
        }
        else
        {
            GUI.Label(new Rect(x, y, w, h), audioEnabled ? "Audio: on" : "Audio: off");
        }
        y += h;

        GUI.Label(new Rect(x, y, w, h), _status);
        y += h;

        // Algorithm selection
        var cohen = GUI.Toggle(new Rect(x, y, 220f, h), _algorithm == ClippingAlgorithm.CohenSutherland, "Cohen–Sutherland");
        var liang = GUI.Toggle(new Rect(x + 240f, y, 220f, h), _algorithm == ClippingAlgorithm.LiangBarsky, "Liang–Barsky");
        y += h;

        if (cohen && _algorithm != ClippingAlgorithm.CohenSutherland)
        {
            _algorithm = ClippingAlgorithm.CohenSutherland;
            SetSelectedAlgorithm(_algorithm);
            if (audioEnabled && autoPlayExplanationOnStart)
            {
                PlayExplanation();
            }
        }
        else if (liang && _algorithm != ClippingAlgorithm.LiangBarsky)
        {
            _algorithm = ClippingAlgorithm.LiangBarsky;
            SetSelectedAlgorithm(_algorithm);
            if (audioEnabled && autoPlayExplanationOnStart)
            {
                PlayExplanation();
            }
        }

        showCalculations = GUI.Toggle(new Rect(x, y, 180f, h), showCalculations, "Show calculations");
        y += h;

        if (showCalculations)
        {
            if (!string.IsNullOrWhiteSpace(_lastCalcText))
            {
                GUI.Label(new Rect(x, y, w, 8f * h), _lastCalcText);
                y += 8f * h;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, h), "Clip a line to see the math.");
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

        _w1 = null;
        _w2 = null;
        _p1 = null;
        _p2 = null;
        _status = "Click window corner 1.";
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

        if (_w1 == null)
        {
            _w1 = pixel;
            _status = $"Window corner 1 set at ({pixel.x}, {pixel.y}). Click window corner 2.";
            PlotPoint(pixel, pointColor);
            _texture.Apply();
            return;
        }

        if (_w2 == null)
        {
            _w2 = pixel;
            _status = "Window set. Click line start point.";
            RedrawWindowAndLine();
            return;
        }

        if (_p1 == null)
        {
            _p1 = pixel;
            _status = $"Line start set at ({pixel.x}, {pixel.y}). Click line end point.";
            RedrawWindowAndLine();
            return;
        }

        _p2 = pixel;

        var rect = GetWindowRect(_w1.Value, _w2.Value);
        var a = _p1.Value;
        var b = _p2.Value;

        var accepted = false;
        Vector2 clippedA = a;
        Vector2 clippedB = b;
        var log = string.Empty;

        if (_algorithm == ClippingAlgorithm.CohenSutherland)
        {
            accepted = CohenSutherlandClip(a, b, rect, out clippedA, out clippedB, out log);
        }
        else
        {
            accepted = LiangBarskyClip(a, b, rect, out clippedA, out clippedB, out log);
        }

        _lastCalcText = log;

        // Draw
        RedrawWindowAndLine();

        if (!accepted)
        {
            _status = "Rejected (line is outside the window). Clear and try again.";
            return;
        }

        var clippedPixels = GetLinePixelsDDA(Vector2Int.RoundToInt(clippedA), Vector2Int.RoundToInt(clippedB));
        _drawProgress = 0;
        _drawTotal = clippedPixels.Count;

        if (!animate)
        {
            DrawPixelsImmediate(clippedPixels, lineColor);
            _texture.Apply();
            _status = "Accepted. Clipped line drawn. Clear to try another.";
            return;
        }

        StartCoroutine(AnimateDrawPixels(clippedPixels));
    }

    private IEnumerator AnimateDrawPixels(List<Vector2Int> pixels)
    {
        _isDrawing = true;
        _status = "Drawing clipped line…";

        var total = Mathf.Max(1, pixels.Count);
        var duration = Mathf.Max(0.05f, drawDurationSeconds);
        var delayPerBatch = duration / Mathf.Max(1, total / Mathf.Max(1, pixelsPerFrame));

        for (var i = 0; i < pixels.Count; i++)
        {
            PlotPoint(pixels[i], lineColor);
            _drawProgress = i + 1;

            if ((i + 1) % Mathf.Max(1, pixelsPerFrame) == 0)
            {
                _texture.Apply();
                yield return new WaitForSeconds(delayPerBatch);
            }
        }

        _texture.Apply();
        _status = "Accepted. Clipped line drawn. Clear to try another.";
        _isDrawing = false;
    }

    private void RedrawWindowAndLine()
    {
        ClearTexture(apply: false);

        if (_w1.HasValue && _w2.HasValue)
        {
            var rect = GetWindowRect(_w1.Value, _w2.Value);
            DrawRectBorder(rect, pointColor);
        }

        if (_p1.HasValue)
        {
            PlotPoint(_p1.Value, pointColor);
        }

        if (_p2.HasValue)
        {
            PlotPoint(_p2.Value, pointColor);
        }

        if (_p1.HasValue && _p2.HasValue)
        {
            var pixels = GetLinePixelsDDA(_p1.Value, _p2.Value);
            DrawPixelsImmediate(pixels, pointColor);
        }

        _texture.Apply();
    }

    private RectInt GetWindowRect(Vector2Int a, Vector2Int b)
    {
        var minX = Mathf.Min(a.x, b.x);
        var maxX = Mathf.Max(a.x, b.x);
        var minY = Mathf.Min(a.y, b.y);
        var maxY = Mathf.Max(a.y, b.y);

        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }

    private void DrawRectBorder(RectInt rect, Color c)
    {
        var x0 = rect.xMin;
        var x1 = rect.xMax;
        var y0 = rect.yMin;
        var y1 = rect.yMax;

        DrawPixelsImmediate(GetLinePixelsDDA(new Vector2Int(x0, y0), new Vector2Int(x1, y0)), c);
        DrawPixelsImmediate(GetLinePixelsDDA(new Vector2Int(x1, y0), new Vector2Int(x1, y1)), c);
        DrawPixelsImmediate(GetLinePixelsDDA(new Vector2Int(x1, y1), new Vector2Int(x0, y1)), c);
        DrawPixelsImmediate(GetLinePixelsDDA(new Vector2Int(x0, y1), new Vector2Int(x0, y0)), c);
    }

    private static string GetAlgorithmLabel(ClippingAlgorithm algorithm)
    {
        return algorithm switch
        {
            ClippingAlgorithm.CohenSutherland => "Cohen–Sutherland Line Clipping",
            ClippingAlgorithm.LiangBarsky => "Liang–Barsky Line Clipping",
            _ => "Line Clipping",
        };
    }

    private static string GetDescriptionText(ClippingAlgorithm algorithm)
    {
        return algorithm switch
        {
            ClippingAlgorithm.CohenSutherland =>
                "Cohen–Sutherland clips a line against a rectangular window using region outcodes.\n" +
                "At each step it chooses an endpoint outside the window and intersects with one window boundary.\n" +
                "It repeats until the line is trivially accepted (both outcodes 0) or rejected (shared outside region).\n\n" +
                "How to use: click 2 points for window corners, then click 2 points for the line.",

            ClippingAlgorithm.LiangBarsky =>
                "Liang–Barsky clips a line using a parametric form: P(t) = P0 + t(P1-P0), where 0<=t<=1.\n" +
                "It computes entry and exit parameters (t0, t1) for each boundary and avoids repeated intersection recomputation.\n\n" +
                "How to use: click 2 points for window corners, then click 2 points for the line.",

            _ => "Line clipping clips a line segment to a rectangular window.",
        };
    }

    private AudioClip GetClipForAlgorithm(ClippingAlgorithm algorithm)
    {
        return algorithm switch
        {
            ClippingAlgorithm.CohenSutherland => cohenExplanation,
            ClippingAlgorithm.LiangBarsky => liangExplanation,
            _ => null,
        };
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

        var clip = GetClipForAlgorithm(_algorithm);
        if (clip == null)
        {
            return;
        }

        _audioSource.Stop();
        _audioSource.clip = clip;
        _audioSource.Play();
    }

    // --- Algorithms ---

    private const int OutLeft = 1;
    private const int OutRight = 2;
    private const int OutBottom = 4;
    private const int OutTop = 8;

    private static int ComputeOutCode(Vector2 p, RectInt rect)
    {
        var code = 0;

        if (p.x < rect.xMin) code |= OutLeft;
        else if (p.x > rect.xMax) code |= OutRight;

        if (p.y < rect.yMin) code |= OutBottom;
        else if (p.y > rect.yMax) code |= OutTop;

        return code;
    }

    private static bool CohenSutherlandClip(Vector2Int a, Vector2Int b, RectInt rect, out Vector2 clippedA, out Vector2 clippedB, out string log)
    {
        var x0 = (float)a.x;
        var y0 = (float)a.y;
        var x1 = (float)b.x;
        var y1 = (float)b.y;

        clippedA = new Vector2(x0, y0);
        clippedB = new Vector2(x1, y1);

        var steps = new List<string>();
        steps.Add("Calculations (Cohen–Sutherland):");
        steps.Add($"window: xmin={rect.xMin}, xmax={rect.xMax}, ymin={rect.yMin}, ymax={rect.yMax}");
        steps.Add($"P0=({x0},{y0}), P1=({x1},{y1})");

        var out0 = ComputeOutCode(new Vector2(x0, y0), rect);
        var out1 = ComputeOutCode(new Vector2(x1, y1), rect);

        var iter = 0;
        while (true)
        {
            iter++;
            steps.Add($"iter {iter}: out0={out0}, out1={out1}");

            if ((out0 | out1) == 0)
            {
                steps.Add("trivial accept (out0|out1 == 0)");
                clippedA = new Vector2(x0, y0);
                clippedB = new Vector2(x1, y1);
                log = string.Join("\n", steps);
                return true;
            }

            if ((out0 & out1) != 0)
            {
                steps.Add("trivial reject (out0 & out1 != 0)");
                log = string.Join("\n", steps);
                return false;
            }

            var outOut = out0 != 0 ? out0 : out1;
            float x = 0f;
            float y = 0f;

            var dx = x1 - x0;
            var dy = y1 - y0;

            if ((outOut & OutTop) != 0)
            {
                y = rect.yMax;
                x = x0 + dx * (y - y0) / Mathf.Max(0.0001f, dy);
                steps.Add($"clip to TOP: y=ymax={rect.yMax}, x = x0 + dx*(y-y0)/dy = {x}");
            }
            else if ((outOut & OutBottom) != 0)
            {
                y = rect.yMin;
                x = x0 + dx * (y - y0) / Mathf.Max(0.0001f, dy);
                steps.Add($"clip to BOTTOM: y=ymin={rect.yMin}, x = {x}");
            }
            else if ((outOut & OutRight) != 0)
            {
                x = rect.xMax;
                y = y0 + dy * (x - x0) / Mathf.Max(0.0001f, dx);
                steps.Add($"clip to RIGHT: x=xmax={rect.xMax}, y = y0 + dy*(x-x0)/dx = {y}");
            }
            else if ((outOut & OutLeft) != 0)
            {
                x = rect.xMin;
                y = y0 + dy * (x - x0) / Mathf.Max(0.0001f, dx);
                steps.Add($"clip to LEFT: x=xmin={rect.xMin}, y = {y}");
            }

            if (outOut == out0)
            {
                x0 = x;
                y0 = y;
                out0 = ComputeOutCode(new Vector2(x0, y0), rect);
                steps.Add($"update P0 -> ({x0},{y0})");
            }
            else
            {
                x1 = x;
                y1 = y;
                out1 = ComputeOutCode(new Vector2(x1, y1), rect);
                steps.Add($"update P1 -> ({x1},{y1})");
            }

            if (iter > 16)
            {
                steps.Add("stopped (safety iteration cap)");
                log = string.Join("\n", steps);
                return false;
            }
        }
    }

    private static bool LiangBarskyClip(Vector2Int a, Vector2Int b, RectInt rect, out Vector2 clippedA, out Vector2 clippedB, out string log)
    {
        var x0 = (float)a.x;
        var y0 = (float)a.y;
        var x1 = (float)b.x;
        var y1 = (float)b.y;

        var dx = x1 - x0;
        var dy = y1 - y0;

        var t0 = 0f;
        var t1 = 1f;

        var steps = new List<string>();
        steps.Add("Calculations (Liang–Barsky):");
        steps.Add($"window: xmin={rect.xMin}, xmax={rect.xMax}, ymin={rect.yMin}, ymax={rect.yMax}");
        steps.Add($"P(t) = P0 + t*(P1-P0), 0<=t<=1");
        steps.Add($"P0=({x0},{y0}), P1=({x1},{y1})");
        steps.Add($"dx={dx}, dy={dy}");

        bool ClipTest(float p, float q, ref float tE, ref float tL, string name)
        {
            if (Math.Abs(p) < 0.0001f)
            {
                if (q < 0)
                {
                    steps.Add($"{name}: parallel and outside -> reject");
                    return false;
                }
                steps.Add($"{name}: parallel and inside -> keep");
                return true;
            }

            var r = q / p;
            steps.Add($"{name}: p={p}, q={q}, r=q/p={r}");

            if (p < 0)
            {
                if (r > tL) { steps.Add("  r > t1 -> reject"); return false; }
                if (r > tE) { tE = r; steps.Add($"  update t0={tE}"); }
            }
            else
            {
                if (r < tE) { steps.Add("  r < t0 -> reject"); return false; }
                if (r < tL) { tL = r; steps.Add($"  update t1={tL}"); }
            }

            return true;
        }

        // Left: x >= xmin -> -dx*t <= x0 - xmin
        if (!ClipTest(-dx, x0 - rect.xMin, ref t0, ref t1, "Left")) { clippedA = default; clippedB = default; log = string.Join("\n", steps); return false; }
        // Right: x <= xmax -> dx*t <= xmax - x0
        if (!ClipTest(dx, rect.xMax - x0, ref t0, ref t1, "Right")) { clippedA = default; clippedB = default; log = string.Join("\n", steps); return false; }
        // Bottom: y >= ymin
        if (!ClipTest(-dy, y0 - rect.yMin, ref t0, ref t1, "Bottom")) { clippedA = default; clippedB = default; log = string.Join("\n", steps); return false; }
        // Top: y <= ymax
        if (!ClipTest(dy, rect.yMax - y0, ref t0, ref t1, "Top")) { clippedA = default; clippedB = default; log = string.Join("\n", steps); return false; }

        if (t0 > t1)
        {
            steps.Add("t0 > t1 -> reject");
            clippedA = default;
            clippedB = default;
            log = string.Join("\n", steps);
            return false;
        }

        clippedA = new Vector2(x0 + t0 * dx, y0 + t0 * dy);
        clippedB = new Vector2(x0 + t1 * dx, y0 + t1 * dy);

        steps.Add($"accept: t0={t0}, t1={t1}");
        steps.Add($"P(t0)=({clippedA.x},{clippedA.y}), P(t1)=({clippedB.x},{clippedB.y})");

        log = string.Join("\n", steps);
        return true;
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

    // --- Common infra ---

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
            _quad.name = "ClipCanvas";
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

    private void ClearTexture(bool apply = true)
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

        if (apply)
        {
            _texture.Apply();
        }
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
