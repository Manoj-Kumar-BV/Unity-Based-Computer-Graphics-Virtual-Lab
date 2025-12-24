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
    private const string SelectedModuleKey = "VirtualLab.SelectedModule";
    private const string SelectedClippingAlgorithmKey = "Clipping.SelectedAlgorithm";

    private const string DdaClipName = "DDA Algorithm";
    private const string BresenhamClipName = "Bresenham's Line Algorithm";

    private const string CircleClipName = "Midpoint Circle Algorithm";
    private const string CohenClipName = "Cohen-Sutherland Line Clipping";
    private const string LiangClipName = "Liang-Barsky Line Clipping";
    private const string PolygonFillClipName = "Scanline Polygon Fill";

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
    [SerializeField] private AudioClip circleExplanation;
    [SerializeField] private AudioClip cohenSutherlandExplanation;
    [SerializeField] private AudioClip liangBarskyExplanation;
    [SerializeField] private AudioClip polygonFillExplanation;

    private GameObject _quad;
    private Texture2D _texture;
    private Vector2Int? _p1;
    private Vector2Int? _p2;
    private Vector2Int? _hoverPixel;
    private AudioSource _audioSource;
    private bool _isDrawing;

    private VirtualLabModule _module;
    private ClippingAlgorithm _clippingAlgorithm;

    private Vector2Int? _circleCenter;
    private Vector2Int? _circleRadiusPoint;

    private Vector2Int? _clipCorner1;
    private Vector2Int? _clipCorner2;
    private Vector2Int? _clipLineP1;
    private Vector2Int? _clipLineP2;

    private readonly List<Vector2Int> _polygonVertices = new List<Vector2Int>();
    private bool _polygonClosed;

    private bool _hasLastLine;
    private Vector2Int _lastP1;
    private Vector2Int _lastP2;
    private string _lastCalcText;
    private int _drawProgress;
    private int _drawTotal;

    private LineAlgorithm _algorithm;
    private string _status;

    private void Awake()
    {
        _module = (VirtualLabModule)PlayerPrefs.GetInt(SelectedModuleKey, (int)VirtualLabModule.LineDrawing);
        _algorithm = (LineAlgorithm)PlayerPrefs.GetInt(SelectedAlgorithmKey, (int)LineAlgorithm.DDA);
        _clippingAlgorithm = (ClippingAlgorithm)PlayerPrefs.GetInt(SelectedClippingAlgorithmKey, (int)ClippingAlgorithm.CohenSutherland);

        EnsureCamera();
        EnsureQuadAndTexture();
        EnsureAudio();
        AutoAssignAudioClipsIfMissing();
        ClearTexture();

        ResetStateForModule();

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

        circleExplanation ??= LoadClipFromResourcesByName(CircleClipName);
        cohenSutherlandExplanation ??= LoadClipFromResourcesByName(CohenClipName);
        liangBarskyExplanation ??= LoadClipFromResourcesByName(LiangClipName);
        polygonFillExplanation ??= LoadClipFromResourcesByName(PolygonFillClipName);
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
                OnCanvasClicked(pixel);
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

        GUI.Label(new Rect(x, y, w, h), $"Module: {GetModuleLabel(_module)}");
        y += h;

        GUI.Label(new Rect(x, y, w, h), $"Algorithm: {GetCurrentAlgorithmLabel()}");
        y += h;

        var description = GetCurrentAlgorithmDescription();
        var descriptionHeight = 5f * h;
        GUI.Label(new Rect(x, y, w, descriptionHeight), description);
        y += descriptionHeight;

        if (audioEnabled && GetExplanationClip() == null)
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
                GUI.Label(new Rect(x, y, w, h), "Complete an action to see the math.");
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
            ClearTexture();
            ResetStateForModule();
        }

        if (_module == VirtualLabModule.PolygonFill)
        {
            if (!_polygonClosed)
            {
                if (GUI.Button(new Rect(x + 152f, y, 140f, 34f), "Finish"))
                {
                    ClosePolygonIfPossible();
                }
            }
            else
            {
                if (GUI.Button(new Rect(x + 152f, y, 140f, 34f), "Fill"))
                {
                    StartPolygonFillIfReady();
                }
            }
        }

        audioEnabled = GUI.Toggle(new Rect(x + 304f, y + 8f, 160f, 34f), audioEnabled, "Audio On");
        y += 42f;

        if (GUI.Button(new Rect(x, y, 140f, 34f), "Back"))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void OnCanvasClicked(Vector2Int pixel)
    {
        switch (_module)
        {
            case VirtualLabModule.LineDrawing:
                OnPixelClickedLine(pixel);
                break;

            case VirtualLabModule.CircleDrawing:
                OnPixelClickedCircle(pixel);
                break;

            case VirtualLabModule.LineClipping:
                OnPixelClickedClipping(pixel);
                break;

            case VirtualLabModule.PolygonFill:
                OnPixelClickedPolygon(pixel);
                break;
        }
    }

    private void OnPixelClickedLine(Vector2Int pixel)
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

    private void OnPixelClickedCircle(Vector2Int pixel)
    {
        if (_isDrawing)
        {
            return;
        }

        if (_circleCenter == null)
        {
            _circleCenter = pixel;
            PlotPoint(pixel);
            _texture.Apply();
            _status = $"Center set at ({pixel.x}, {pixel.y}). Click a point on the circle (radius).";
            return;
        }

        _circleRadiusPoint = pixel;

        var center = _circleCenter.Value;
        var r = Mathf.RoundToInt(Vector2.Distance(new Vector2(center.x, center.y), new Vector2(pixel.x, pixel.y)));
        r = Mathf.Max(0, r);

        var steps = new List<CircleStep>();
        var pixels = GetCirclePixelsMidpoint(center, r, steps);

        _hasLastLine = true;
        _lastCalcText = BuildCircleCalculationText(center, r, steps);
        _drawProgress = 0;
        _drawTotal = pixels.Count;

        if (!animateLineDrawing)
        {
            DrawPixelsImmediate(pixels, lineColor);
            _texture.Apply();
            _status = $"Drew circle (r={r}). Click a new center.";
            _circleCenter = null;
            _circleRadiusPoint = null;
            _drawProgress = _drawTotal;
            return;
        }

        StartCoroutine(AnimateDrawPixels(
            pixels,
            lineColor,
            $"Drawing circle (r={r})…",
            $"Drew circle (r={r}). Click a new center.",
            () =>
            {
                _circleCenter = null;
                _circleRadiusPoint = null;
            }
        ));
    }

    private void OnPixelClickedClipping(Vector2Int pixel)
    {
        if (_isDrawing)
        {
            return;
        }

        if (_clipCorner1 == null)
        {
            _clipCorner1 = pixel;
            ClearTexture();
            PlotPoint(pixel);
            _texture.Apply();
            _status = $"Window corner 1 set at ({pixel.x}, {pixel.y}). Click window corner 2.";
            return;
        }

        if (_clipCorner2 == null)
        {
            _clipCorner2 = pixel;
            ClearTexture();
            DrawClippingWindow();
            _texture.Apply();
            _status = "Window set. Click line point 1.";
            return;
        }

        if (_clipLineP1 == null)
        {
            _clipLineP1 = pixel;
            ClearTexture();
            DrawClippingWindow();
            PlotPoint(pixel);
            _texture.Apply();
            _status = $"Line point 1 set at ({pixel.x}, {pixel.y}). Click line point 2.";
            return;
        }

        _clipLineP2 = pixel;

        if (!TryGetClipWindow(out var wMin, out var wMax))
        {
            _status = "Invalid window. Press Clear and set window again.";
            return;
        }

        var p1 = _clipLineP1.Value;
        var p2 = _clipLineP2.Value;

        var accepted = TryClipLine(p1, p2, wMin, wMax, out var c1, out var c2, out var calc);
        _lastCalcText = calc;

        ClearTexture();
        DrawClippingWindow();
        DrawLineAllSlopes(p1, p2, Color.gray);
        PlotPoint(p1);
        PlotPoint(p2);

        if (!accepted)
        {
            _texture.Apply();
            _status = "Line rejected (outside). Click a new line point 1 (window stays).";
            _clipLineP1 = null;
            _clipLineP2 = null;
            return;
        }

        var clippedPixels = GetLinePixelsBresenhamAllSlopes(c1, c2);
        _drawProgress = 0;
        _drawTotal = clippedPixels.Count;

        if (!animateLineDrawing)
        {
            DrawPixelsImmediate(clippedPixels, lineColor);
            _texture.Apply();
            _status = "Clipped line drawn. Click a new line point 1 (window stays).";
            _clipLineP1 = null;
            _clipLineP2 = null;
            _drawProgress = _drawTotal;
            return;
        }

        StartCoroutine(AnimateDrawPixels(
            clippedPixels,
            lineColor,
            "Drawing clipped line…",
            "Clipped line drawn. Click a new line point 1 (window stays).",
            () =>
            {
                _clipLineP1 = null;
                _clipLineP2 = null;
            }
        ));
    }

    private void OnPixelClickedPolygon(Vector2Int pixel)
    {
        if (_isDrawing)
        {
            return;
        }

        if (_polygonClosed)
        {
            _status = "Polygon is already closed. Press Fill or Clear.";
            return;
        }

        _polygonVertices.Add(pixel);

        if (_polygonVertices.Count == 1)
        {
            ClearTexture();
            PlotPoint(pixel);
            _texture.Apply();
            _status = $"Vertex 1 set at ({pixel.x}, {pixel.y}). Click more vertices, then press Finish.";
            _lastCalcText = BuildPolygonBasicsText();
            return;
        }

        var prev = _polygonVertices[_polygonVertices.Count - 2];
        DrawLineAllSlopes(prev, pixel, pointColor);
        PlotPoint(pixel);
        _texture.Apply();

        _status = $"Vertex {_polygonVertices.Count} added. Press Finish when done.";
        _lastCalcText = BuildPolygonBasicsText();
    }

    private void ClosePolygonIfPossible()
    {
        if (_isDrawing)
        {
            return;
        }

        if (_polygonClosed)
        {
            return;
        }

        if (_polygonVertices.Count < 3)
        {
            _status = "Need at least 3 vertices to close a polygon.";
            return;
        }

        var first = _polygonVertices[0];
        var last = _polygonVertices[_polygonVertices.Count - 1];
        DrawLineAllSlopes(last, first, pointColor);
        _texture.Apply();
        _polygonClosed = true;

        _status = "Polygon closed. Press Fill to scanline-fill it.";
        _lastCalcText = BuildPolygonBasicsText();
    }

    private void StartPolygonFillIfReady()
    {
        if (_isDrawing)
        {
            return;
        }

        if (!_polygonClosed)
        {
            _status = "Finish (close) the polygon first.";
            return;
        }

        if (_polygonVertices.Count < 3)
        {
            _status = "Polygon needs at least 3 vertices.";
            return;
        }

        var scanlines = BuildScanlineFill(_polygonVertices, out var fillPixelsCount);
        _drawProgress = 0;
        _drawTotal = fillPixelsCount;
        _lastCalcText = BuildPolygonFillText(scanlines, fillPixelsCount);

        if (!animateLineDrawing)
        {
            DrawScanlinesImmediate(scanlines, lineColor);
            _texture.Apply();
            _status = "Polygon filled. Press Clear to start a new polygon.";
            _drawProgress = _drawTotal;
            return;
        }

        StartCoroutine(AnimateScanlineFill(scanlines, lineColor));
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

        var clip = GetExplanationClip();
        if (clip == null)
        {
            return;
        }

        _audioSource.Stop();
        _audioSource.clip = clip;
        _audioSource.Play();
    }

    private AudioClip GetExplanationClip()
    {
        return _module switch
        {
            VirtualLabModule.LineDrawing => _algorithm switch
            {
                LineAlgorithm.DDA => ddaExplanation,
                LineAlgorithm.Bresenham => bresenhamExplanation,
                LineAlgorithm.BresenhamFull => bresenhamAllSlopesExplanation != null ? bresenhamAllSlopesExplanation : bresenhamExplanation,
                _ => null,
            },
            VirtualLabModule.CircleDrawing => circleExplanation,
            VirtualLabModule.LineClipping => _clippingAlgorithm switch
            {
                ClippingAlgorithm.CohenSutherland => cohenSutherlandExplanation,
                ClippingAlgorithm.LiangBarsky => liangBarskyExplanation,
                _ => null,
            },
            VirtualLabModule.PolygonFill => polygonFillExplanation,
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

    private static string GetModuleLabel(VirtualLabModule module)
    {
        return module switch
        {
            VirtualLabModule.LineDrawing => "Line Drawing",
            VirtualLabModule.CircleDrawing => "Circle Drawing",
            VirtualLabModule.LineClipping => "Line Clipping",
            VirtualLabModule.PolygonFill => "Polygon Fill",
            _ => module.ToString(),
        };
    }

    private string GetCurrentAlgorithmLabel()
    {
        return _module switch
        {
            VirtualLabModule.LineDrawing => GetAlgorithmLabel(_algorithm),
            VirtualLabModule.CircleDrawing => "Midpoint Circle",
            VirtualLabModule.LineClipping => _clippingAlgorithm == ClippingAlgorithm.LiangBarsky ? "Liang–Barsky" : "Cohen–Sutherland",
            VirtualLabModule.PolygonFill => "Scanline Fill",
            _ => "",
        };
    }

    private string GetCurrentAlgorithmDescription()
    {
        return _module switch
        {
            VirtualLabModule.LineDrawing => GetAlgorithmDescription(_algorithm),
            VirtualLabModule.CircleDrawing =>
                "Midpoint Circle draws a circle using only integer decisions and symmetry.\n" +
                "From a center and radius, it computes points in one octant and mirrors them into 8 symmetric positions.\n" +
                "A decision parameter p tells whether the next point moves East or South-East.\n" +
                "Goal: see how symmetry and an error term generate a circle.",
            VirtualLabModule.LineClipping => _clippingAlgorithm == ClippingAlgorithm.LiangBarsky
                ? "Liang–Barsky clips a line to a rectangular window using a parametric form.\n" +
                  "It updates an interval [u1, u2] based on 4 inequalities (left, right, bottom, top).\n" +
                  "If u1 > u2 the line is rejected; otherwise the clipped segment endpoints come from u1 and u2.\n" +
                  "Goal: learn clipping via parameters instead of repeated intersections."
                : "Cohen–Sutherland clips a line to a rectangular window using outcodes.\n" +
                  "Each endpoint gets a 4-bit code (left/right/bottom/top).\n" +
                  "Trivial accept/reject happens with bit operations; otherwise we intersect with a boundary and iterate.\n" +
                  "Goal: learn region coding and step-by-step clipping.",
            VirtualLabModule.PolygonFill =>
                "Scanline Fill fills a polygon by sweeping horizontal scanlines from bottom to top.\n" +
                "For each scanline y, it computes intersections with polygon edges, sorts the x values, then fills pixels between pairs.\n" +
                "This demonstrates how polygons are rasterized into pixels.\n" +
                "Goal: understand intersections and pairwise filling.",
            _ => "",
        };
    }

    private void ResetStateForModule()
    {
        StopAllCoroutines();
        _isDrawing = false;

        _p1 = null;
        _p2 = null;
        _circleCenter = null;
        _circleRadiusPoint = null;
        _clipCorner1 = null;
        _clipCorner2 = null;
        _clipLineP1 = null;
        _clipLineP2 = null;

        _polygonVertices.Clear();
        _polygonClosed = false;

        _hasLastLine = false;
        _lastCalcText = null;
        _drawProgress = 0;
        _drawTotal = 0;

        _status = _module switch
        {
            VirtualLabModule.LineDrawing => "Click the first point.",
            VirtualLabModule.CircleDrawing => "Click the circle center.",
            VirtualLabModule.LineClipping => "Click clipping window corner 1.",
            VirtualLabModule.PolygonFill => "Click polygon vertices, then press Finish.",
            _ => "Ready.",
        };
    }

    private void DrawLineAllSlopes(Vector2Int p1, Vector2Int p2, Color color)
    {
        var pixels = GetLinePixelsBresenhamAllSlopes(p1, p2);
        DrawPixelsImmediate(pixels, color);
    }

    private bool TryGetClipWindow(out Vector2Int min, out Vector2Int max)
    {
        min = default;
        max = default;

        if (_clipCorner1 == null || _clipCorner2 == null)
        {
            return false;
        }

        var a = _clipCorner1.Value;
        var b = _clipCorner2.Value;
        min = new Vector2Int(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        max = new Vector2Int(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        return min.x != max.x && min.y != max.y;
    }

    private void DrawClippingWindow()
    {
        if (!TryGetClipWindow(out var min, out var max))
        {
            return;
        }

        var bl = new Vector2Int(min.x, min.y);
        var br = new Vector2Int(max.x, min.y);
        var tr = new Vector2Int(max.x, max.y);
        var tl = new Vector2Int(min.x, max.y);

        DrawLineAllSlopes(bl, br, pointColor);
        DrawLineAllSlopes(br, tr, pointColor);
        DrawLineAllSlopes(tr, tl, pointColor);
        DrawLineAllSlopes(tl, bl, pointColor);
    }

    private bool TryClipLine(Vector2Int p1, Vector2Int p2, Vector2Int wMin, Vector2Int wMax, out Vector2Int c1, out Vector2Int c2, out string calc)
    {
        if (_clippingAlgorithm == ClippingAlgorithm.LiangBarsky)
        {
            return TryClipLineLiangBarsky(p1, p2, wMin, wMax, out c1, out c2, out calc);
        }

        return TryClipLineCohenSutherland(p1, p2, wMin, wMax, out c1, out c2, out calc);
    }

    private const int OutLeft = 1;
    private const int OutRight = 2;
    private const int OutBottom = 4;
    private const int OutTop = 8;

    private static int ComputeOutCode(Vector2Int p, Vector2Int wMin, Vector2Int wMax)
    {
        var code = 0;

        if (p.x < wMin.x) code |= OutLeft;
        else if (p.x > wMax.x) code |= OutRight;

        if (p.y < wMin.y) code |= OutBottom;
        else if (p.y > wMax.y) code |= OutTop;

        return code;
    }

    private bool TryClipLineCohenSutherland(Vector2Int p1, Vector2Int p2, Vector2Int wMin, Vector2Int wMax, out Vector2Int c1, out Vector2Int c2, out string calc)
    {
        var x0 = (float)p1.x;
        var y0 = (float)p1.y;
        var x1 = (float)p2.x;
        var y1 = (float)p2.y;

        var code0 = ComputeOutCode(p1, wMin, wMax);
        var code1 = ComputeOutCode(p2, wMin, wMax);
        var steps = 0;

        calc = $"Window: min({wMin.x},{wMin.y}) max({wMax.x},{wMax.y})\n" +
               $"Start P0=({p1.x},{p1.y}) code={code0}  P1=({p2.x},{p2.y}) code={code1}\n";

        var accept = false;

        while (true)
        {
            steps++;

            if ((code0 | code1) == 0)
            {
                accept = true;
                calc += $"Step {steps}: Trivial accept (both codes 0).\n";
                break;
            }

            if ((code0 & code1) != 0)
            {
                calc += $"Step {steps}: Trivial reject (code0 & code1 != 0).\n";
                break;
            }

            var codeOut = code0 != 0 ? code0 : code1;
            var x = 0f;
            var y = 0f;
            var boundary = "";

            if ((codeOut & OutTop) != 0)
            {
                boundary = "TOP";
                y = wMax.y;
                x = x0 + (x1 - x0) * (wMax.y - y0) / (y1 - y0);
            }
            else if ((codeOut & OutBottom) != 0)
            {
                boundary = "BOTTOM";
                y = wMin.y;
                x = x0 + (x1 - x0) * (wMin.y - y0) / (y1 - y0);
            }
            else if ((codeOut & OutRight) != 0)
            {
                boundary = "RIGHT";
                x = wMax.x;
                y = y0 + (y1 - y0) * (wMax.x - x0) / (x1 - x0);
            }
            else if ((codeOut & OutLeft) != 0)
            {
                boundary = "LEFT";
                x = wMin.x;
                y = y0 + (y1 - y0) * (wMin.x - x0) / (x1 - x0);
            }

            var ix = Mathf.RoundToInt(x);
            var iy = Mathf.RoundToInt(y);
            ix = Mathf.Clamp(ix, 0, resolution - 1);
            iy = Mathf.Clamp(iy, 0, resolution - 1);

            calc += $"Step {steps}: Intersect with {boundary} -> ({ix},{iy})\n";

            if (codeOut == code0)
            {
                x0 = ix;
                y0 = iy;
                code0 = ComputeOutCode(new Vector2Int(ix, iy), wMin, wMax);
                calc += $"  Update P0 code={code0}\n";
            }
            else
            {
                x1 = ix;
                y1 = iy;
                code1 = ComputeOutCode(new Vector2Int(ix, iy), wMin, wMax);
                calc += $"  Update P1 code={code1}\n";
            }

            if (steps >= 16)
            {
                calc += "Stopped after 16 steps (safety limit).\n";
                break;
            }
        }

        if (!accept)
        {
            c1 = default;
            c2 = default;
            return false;
        }

        c1 = new Vector2Int(Mathf.RoundToInt(x0), Mathf.RoundToInt(y0));
        c2 = new Vector2Int(Mathf.RoundToInt(x1), Mathf.RoundToInt(y1));
        calc += $"Result: ({c1.x},{c1.y}) to ({c2.x},{c2.y})\n";
        return true;
    }

    private bool TryClipLineLiangBarsky(Vector2Int p1, Vector2Int p2, Vector2Int wMin, Vector2Int wMax, out Vector2Int c1, out Vector2Int c2, out string calc)
    {
        var sb = new System.Text.StringBuilder(256);
        var dx = (float)(p2.x - p1.x);
        var dy = (float)(p2.y - p1.y);

        var u1 = 0f;
        var u2 = 1f;

        sb.Append($"Window: min({wMin.x},{wMin.y}) max({wMax.x},{wMax.y})\n");
        sb.Append($"P0=({p1.x},{p1.y}) P1=({p2.x},{p2.y})  dx={dx} dy={dy}\n");

        bool ClipTest(float p, float q, ref float inU1, ref float inU2, string label)
        {
            if (Mathf.Approximately(p, 0f))
            {
                sb.Append($"{label}: p=0 -> ");
                if (q < 0)
                {
                    sb.Append("reject (parallel outside)\n");
                    return false;
                }
                sb.Append("keep (parallel inside)\n");
                return true;
            }

            var r = q / p;
            sb.Append($"{label}: p={p} q={q} r={r}\n");
            if (p < 0)
            {
                if (r > inU2) return false;
                if (r > inU1) inU1 = r;
            }
            else
            {
                if (r < inU1) return false;
                if (r < inU2) inU2 = r;
            }

            sb.Append($"  u1={inU1} u2={inU2}\n");
            return true;
        }

        if (!ClipTest(-dx, p1.x - wMin.x, ref u1, ref u2, "Left")) { c1 = default; c2 = default; sb.Append("Reject\n"); calc = sb.ToString(); return false; }
        if (!ClipTest(dx, wMax.x - p1.x, ref u1, ref u2, "Right")) { c1 = default; c2 = default; sb.Append("Reject\n"); calc = sb.ToString(); return false; }
        if (!ClipTest(-dy, p1.y - wMin.y, ref u1, ref u2, "Bottom")) { c1 = default; c2 = default; sb.Append("Reject\n"); calc = sb.ToString(); return false; }
        if (!ClipTest(dy, wMax.y - p1.y, ref u1, ref u2, "Top")) { c1 = default; c2 = default; sb.Append("Reject\n"); calc = sb.ToString(); return false; }

        var cx0 = p1.x + u1 * dx;
        var cy0 = p1.y + u1 * dy;
        var cx1 = p1.x + u2 * dx;
        var cy1 = p1.y + u2 * dy;

        c1 = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(cx0), 0, resolution - 1), Mathf.Clamp(Mathf.RoundToInt(cy0), 0, resolution - 1));
        c2 = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(cx1), 0, resolution - 1), Mathf.Clamp(Mathf.RoundToInt(cy1), 0, resolution - 1));
        sb.Append($"Result u1={u1} u2={u2} -> ({c1.x},{c1.y}) to ({c2.x},{c2.y})\n");
        calc = sb.ToString();
        return true;
    }

    private struct CircleStep
    {
        public int x;
        public int y;
        public int p;
        public bool choseSE;
    }

    private List<Vector2Int> GetCirclePixelsMidpoint(Vector2Int center, int radius, List<CircleStep> steps)
    {
        var pixels = new List<Vector2Int>();
        var seen = new HashSet<int>();

        void Add(int x, int y)
        {
            if (x < 0 || y < 0 || x >= resolution || y >= resolution)
            {
                return;
            }

            var key = x + y * resolution;
            if (seen.Add(key))
            {
                pixels.Add(new Vector2Int(x, y));
            }
        }

        void Plot8(int x, int y)
        {
            Add(center.x + x, center.y + y);
            Add(center.x - x, center.y + y);
            Add(center.x + x, center.y - y);
            Add(center.x - x, center.y - y);
            Add(center.x + y, center.y + x);
            Add(center.x - y, center.y + x);
            Add(center.x + y, center.y - x);
            Add(center.x - y, center.y - x);
        }

        if (radius == 0)
        {
            Plot8(0, 0);
            steps.Add(new CircleStep { x = 0, y = 0, p = 0, choseSE = false });
            return pixels;
        }

        var x = 0;
        var y = radius;
        var p = 1 - radius;

        while (x <= y)
        {
            Plot8(x, y);

            if (p < 0)
            {
                steps.Add(new CircleStep { x = x, y = y, p = p, choseSE = false });
                p = p + 2 * x + 3;
            }
            else
            {
                steps.Add(new CircleStep { x = x, y = y, p = p, choseSE = true });
                p = p + 2 * (x - y) + 5;
                y--;
            }

            x++;
        }

        return pixels;
    }

    private static string BuildCircleCalculationText(Vector2Int center, int r, List<CircleStep> steps)
    {
        var text = $"Center=({center.x},{center.y})  r={r}\n" +
                   "Midpoint Circle: start x=0, y=r, p=1-r\n";

        var lines = Mathf.Min(6, steps.Count);
        for (var i = 0; i < lines; i++)
        {
            var s = steps[i];
            text += $"Step {i + 1}: x={s.x} y={s.y} p={s.p} -> {(s.choseSE ? "SE" : "E")}\n";
        }

        if (steps.Count > lines)
        {
            text += $"… ({steps.Count - lines} more steps)\n";
        }

        return text;
    }

    private static string BuildPolygonBasicsText(List<Vector2Int> vertices)
    {
        if (vertices == null || vertices.Count == 0)
        {
            return "Vertices: 0";
        }

        var minX = vertices[0].x;
        var maxX = vertices[0].x;
        var minY = vertices[0].y;
        var maxY = vertices[0].y;

        for (var i = 1; i < vertices.Count; i++)
        {
            var v = vertices[i];
            minX = Mathf.Min(minX, v.x);
            maxX = Mathf.Max(maxX, v.x);
            minY = Mathf.Min(minY, v.y);
            maxY = Mathf.Max(maxY, v.y);
        }

        return $"Vertices: {vertices.Count}\nBounds: x[{minX},{maxX}] y[{minY},{maxY}]";
    }

    private string BuildPolygonBasicsText()
    {
        return BuildPolygonBasicsText(_polygonVertices);
    }

    private struct ScanlineSegment
    {
        public int y;
        public int xStart;
        public int xEnd;
    }

    private List<ScanlineSegment> BuildScanlineFill(List<Vector2Int> vertices, out int fillPixelsCount)
    {
        var segments = new List<ScanlineSegment>();
        fillPixelsCount = 0;

        var n = vertices.Count;
        var minY = vertices[0].y;
        var maxY = vertices[0].y;
        for (var i = 1; i < n; i++)
        {
            minY = Mathf.Min(minY, vertices[i].y);
            maxY = Mathf.Max(maxY, vertices[i].y);
        }

        for (var y = minY; y <= maxY; y++)
        {
            var xs = new List<float>();

            for (var i = 0; i < n; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % n];

                if (a.y == b.y)
                {
                    continue;
                }

                // Ensure a.y < b.y
                if (a.y > b.y)
                {
                    (a, b) = (b, a);
                }

                // Include scanlines in [a.y, b.y)
                if (y < a.y || y >= b.y)
                {
                    continue;
                }

                var t = (y - a.y) / (float)(b.y - a.y);
                var x = a.x + t * (b.x - a.x);
                xs.Add(x);
            }

            xs.Sort();
            for (var i = 0; i + 1 < xs.Count; i += 2)
            {
                var x0 = Mathf.CeilToInt(xs[i]);
                var x1 = Mathf.FloorToInt(xs[i + 1]);
                if (x1 < x0)
                {
                    continue;
                }

                x0 = Mathf.Clamp(x0, 0, resolution - 1);
                x1 = Mathf.Clamp(x1, 0, resolution - 1);

                segments.Add(new ScanlineSegment { y = y, xStart = x0, xEnd = x1 });
                fillPixelsCount += (x1 - x0 + 1);
            }
        }

        return segments;
    }

    private static string BuildPolygonFillText(List<ScanlineSegment> segments, int fillPixelsCount)
    {
        var lines = $"Scanlines: {segments.Count}\nFill pixels: {fillPixelsCount}\n";
        if (segments.Count > 0)
        {
            var show = Mathf.Min(4, segments.Count);
            for (var i = 0; i < show; i++)
            {
                var s = segments[i];
                lines += $"y={s.y}: x[{s.xStart},{s.xEnd}]\n";
            }
            if (segments.Count > show)
            {
                lines += $"… ({segments.Count - show} more scanlines)\n";
            }
        }
        return lines;
    }

    private void DrawScanlinesImmediate(List<ScanlineSegment> segments, Color color)
    {
        for (var i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            if (s.y < 0 || s.y >= resolution)
            {
                continue;
            }

            for (var x = s.xStart; x <= s.xEnd; x++)
            {
                _texture.SetPixel(x, s.y, color);
            }
        }
    }

    private IEnumerator AnimateScanlineFill(List<ScanlineSegment> segments, Color color)
    {
        _isDrawing = true;
        _status = "Filling polygon…";

        var duration = Mathf.Max(0.05f, lineDrawDurationSeconds);
        var batches = Mathf.Max(1, segments.Count);
        var delay = duration / batches;

        _drawProgress = 0;

        for (var i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            if (s.y >= 0 && s.y < resolution)
            {
                for (var x = s.xStart; x <= s.xEnd; x++)
                {
                    _texture.SetPixel(x, s.y, color);
                    _drawProgress++;
                }
            }

            if (i % 2 == 0)
            {
                _texture.Apply();
                yield return new WaitForSeconds(delay);
            }
        }

        _texture.Apply();
        _isDrawing = false;
        _status = "Polygon filled. Press Clear to start a new polygon.";
        _drawProgress = _drawTotal;
    }

    private IEnumerator AnimateDrawPixels(List<Vector2Int> pixels, Color color, string drawingStatus, string doneStatus, Action onDone)
    {
        _isDrawing = true;
        _status = drawingStatus;

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
                _texture.SetPixel(p.x, p.y, color);
            }

            _drawProgress = i + 1;

            if ((i + 1) % Mathf.Max(1, pixelsPerFrame) == 0)
            {
                _texture.Apply();
                yield return new WaitForSeconds(delayPerBatch);
            }
        }

        _texture.Apply();
        _isDrawing = false;
        _status = doneStatus;
        _drawProgress = _drawTotal;

        onDone?.Invoke();
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
