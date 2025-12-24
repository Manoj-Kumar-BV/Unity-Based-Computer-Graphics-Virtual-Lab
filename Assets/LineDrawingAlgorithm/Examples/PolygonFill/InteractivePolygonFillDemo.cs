using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractivePolygonFillDemo : MonoBehaviour
{
    private const string FillClipName = "Scanline Polygon Fill";

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Canvas")]
    [SerializeField] private int resolution = 64;
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color pointColor = Color.yellow;
    [SerializeField] private Color fillColor = Color.red;

    [Header("Animation")]
    [SerializeField] private bool animate = true;
    [SerializeField] private float fillDurationSeconds = 0.9f;

    [Header("Education")]
    [SerializeField] private bool showCalculations = true;

    [Header("Quad")]
    [SerializeField] private float quadSize = 5f;

    [Header("Audio (optional)")]
    [SerializeField] private bool audioEnabled = true;
    [SerializeField] private bool autoPlayExplanationOnStart = true;
    [SerializeField] private AudioClip fillExplanation;

    private GameObject _quad;
    private Texture2D _texture;
    private AudioSource _audioSource;

    private readonly List<Vector2Int> _vertices = new();
    private bool _isClosed;
    private bool _isFilling;

    private string _status = "Click vertices to create a polygon.";
    private string _lastCalcText;
    private int _currentScanline;

    private void Awake()
    {
        EnsureCamera();
        EnsureQuadAndTexture();
        EnsureAudio();

        fillExplanation ??= LoadClipFromResourcesByName(FillClipName);

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

        GUI.Label(new Rect(x, y, w, h), "Algorithm: Scanline Polygon Fill");
        y += h;

        GUI.Label(new Rect(x, y, w, 7f * h), GetDescriptionText());
        y += 7f * h;

        if (audioEnabled && fillExplanation == null)
        {
            GUI.Label(new Rect(x, y, w, h), "Audio: missing clip (put mp3 under Assets/Resources/LineDrawingAlgorithm/ with name 'Scanline Polygon Fill')");
        }
        else
        {
            GUI.Label(new Rect(x, y, w, h), audioEnabled ? "Audio: on" : "Audio: off");
        }
        y += h;

        GUI.Label(new Rect(x, y, w, h), $"Vertices: {_vertices.Count}  |  Closed: {_isClosed}  |  Filling: {_isFilling}");
        y += h;

        GUI.Label(new Rect(x, y, w, h), _status);
        y += h;

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
                GUI.Label(new Rect(x, y, w, h), "Fill a polygon to see the math.");
                y += h;
            }

            if (_isFilling)
            {
                GUI.Label(new Rect(x, y, w, h), $"Current scanline y = {_currentScanline}");
                y += h;
            }
        }

        // Controls
        if (GUI.Button(new Rect(x, y, 140f, 34f), "Undo"))
        {
            UndoVertex();
        }

        if (GUI.Button(new Rect(x + 152f, y, 140f, 34f), "Close"))
        {
            ClosePolygon();
        }

        if (GUI.Button(new Rect(x + 304f, y, 140f, 34f), "Fill"))
        {
            StartFill();
        }

        if (GUI.Button(new Rect(x + 456f, y, 140f, 34f), "Clear"))
        {
            ResetAll();
        }
        y += 42f;

        audioEnabled = GUI.Toggle(new Rect(x, y, 160f, 34f), audioEnabled, "Audio On");

        if (GUI.Button(new Rect(x + 172f, y, 140f, 34f), "Play"))
        {
            PlayExplanation();
        }

        if (GUI.Button(new Rect(x + 324f, y, 140f, 34f), "Back"))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void OnPixelClicked(Vector2Int pixel)
    {
        if (_isFilling)
        {
            return;
        }

        if (_isClosed)
        {
            _status = "Polygon is closed. Press Fill or Clear.";
            return;
        }

        _vertices.Add(pixel);
        PlotPoint(pixel, pointColor);

        if (_vertices.Count >= 2)
        {
            var a = _vertices[^2];
            var b = _vertices[^1];
            DrawPixelsImmediate(GetLinePixelsDDA(a, b), pointColor);
        }

        _texture.Apply();
        _status = "Add more vertices, then press Close.";
    }

    private void UndoVertex()
    {
        if (_isFilling)
        {
            return;
        }

        if (_vertices.Count == 0)
        {
            return;
        }

        if (_isClosed)
        {
            _status = "Cannot undo after closing. Clear to start again.";
            return;
        }

        _vertices.RemoveAt(_vertices.Count - 1);
        RedrawOutlineOnly();
        _status = "Last vertex removed.";
    }

    private void ClosePolygon()
    {
        if (_isFilling)
        {
            return;
        }

        if (_isClosed)
        {
            return;
        }

        if (_vertices.Count < 3)
        {
            _status = "Need at least 3 vertices to close.";
            return;
        }

        _isClosed = true;
        RedrawOutlineOnly();
        _status = "Polygon closed. Press Fill.";
    }

    private void StartFill()
    {
        if (_isFilling)
        {
            return;
        }

        if (!_isClosed)
        {
            _status = "Close the polygon first.";
            return;
        }

        StartCoroutine(AnimateScanlineFill());
    }

    private IEnumerator AnimateScanlineFill()
    {
        _isFilling = true;
        _status = "Filling…";

        // Determine scanline range
        var minY = int.MaxValue;
        var maxY = int.MinValue;
        for (var i = 0; i < _vertices.Count; i++)
        {
            minY = Mathf.Min(minY, _vertices[i].y);
            maxY = Mathf.Max(maxY, _vertices[i].y);
        }

        minY = Mathf.Clamp(minY, 0, resolution - 1);
        maxY = Mathf.Clamp(maxY, 0, resolution - 1);

        var totalScanlines = Mathf.Max(1, maxY - minY + 1);
        var delay = Mathf.Max(0.01f, fillDurationSeconds / totalScanlines);

        for (var y = minY; y <= maxY; y++)
        {
            _currentScanline = y;

            var intersections = ComputeIntersectionsAtScanline(y, _vertices);
            intersections.Sort();

            _lastCalcText = BuildCalcText(y, intersections);

            // Fill between pairs
            for (var i = 0; i + 1 < intersections.Count; i += 2)
            {
                var xStart = Mathf.CeilToInt(intersections[i]);
                var xEnd = Mathf.FloorToInt(intersections[i + 1]);

                for (var x = xStart; x <= xEnd; x++)
                {
                    PlotPoint(new Vector2Int(x, y), fillColor);
                }
            }

            // Keep outline visible
            DrawOutline(pointColor);

            _texture.Apply();
            yield return new WaitForSeconds(delay);
        }

        _isFilling = false;
        _status = "Done. Clear to try another polygon.";
    }

    private static List<float> ComputeIntersectionsAtScanline(int y, List<Vector2Int> vertices)
    {
        var xs = new List<float>();

        for (var i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];

            // Ignore horizontal edges
            if (a.y == b.y)
            {
                continue;
            }

            // Use half-open rule: include ymin, exclude ymax to avoid double counting.
            var ymin = Mathf.Min(a.y, b.y);
            var ymax = Mathf.Max(a.y, b.y);
            if (y < ymin || y >= ymax)
            {
                continue;
            }

            // Intersection x = x0 + (y - y0) * (x1 - x0) / (y1 - y0)
            var t = (y - a.y) / (float)(b.y - a.y);
            var x = a.x + t * (b.x - a.x);
            xs.Add(x);
        }

        return xs;
    }

    private static string BuildCalcText(int y, List<float> intersections)
    {
        var list = intersections.Count == 0 ? "(none)" : string.Join(", ", intersections.ConvertAll(v => v.ToString("0.00")));

        return
            "Calculations (Scanline Fill):\n" +
            "For each scanline y, find intersections of polygon edges with that horizontal line.\n" +
            "Sort intersection x values, then fill between pairs: (x0..x1), (x2..x3), ...\n\n" +
            $"scanline y = {y}\n" +
            $"intersections x = {list}";
    }

    private void ResetAll()
    {
        if (_isFilling)
        {
            return;
        }

        _vertices.Clear();
        _isClosed = false;
        _status = "Click vertices to create a polygon.";
        _lastCalcText = null;
        _currentScanline = 0;
        ClearTexture();
    }

    private void RedrawOutlineOnly()
    {
        ClearTexture(apply: false);
        DrawOutline(pointColor);
        _texture.Apply();
    }

    private void DrawOutline(Color c)
    {
        if (_vertices.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _vertices.Count - 1; i++)
        {
            DrawPixelsImmediate(GetLinePixelsDDA(_vertices[i], _vertices[i + 1]), c);
        }

        if (_isClosed && _vertices.Count >= 3)
        {
            DrawPixelsImmediate(GetLinePixelsDDA(_vertices[^1], _vertices[0]), c);
        }

        for (var i = 0; i < _vertices.Count; i++)
        {
            PlotPoint(_vertices[i], c);
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

    private static string GetDescriptionText()
    {
        return
            "Scanline polygon fill fills a closed polygon by sweeping horizontal scanlines.\n" +
            "For each scanline, compute intersection points with polygon edges, sort them, and fill between pairs.\n" +
            "This approach is widely used in raster graphics and helps demonstrate inside/outside parity.\n\n" +
            "How to use: click vertices, press Close, then press Fill.";
    }

    private AudioClip GetExplanationClip() => fillExplanation;

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
            _quad.name = "PolygonCanvas";
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
