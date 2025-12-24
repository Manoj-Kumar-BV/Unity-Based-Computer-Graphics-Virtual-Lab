using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Opening : MonoBehaviour
{
    private const string IntroClipName = "Welcome Intro";

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Audio (optional)")]
    [SerializeField] private bool audioEnabled = true;
    [SerializeField] private bool autoPlayOnStart = true;
    [SerializeField] private AudioClip introClip;

    private AudioSource _audioSource;

    private void Awake()
    {
        EnsureCamera();
        EnsureAudio();

        if (introClip == null)
        {
            introClip = LoadClipFromResourcesByName(IntroClipName);
        }

        if (audioEnabled && autoPlayOnStart)
        {
            PlayIntro();
        }
    }

    private void OnGUI()
    {
        const float pad = 16f;
        const float w = 560f;
        const float h = 28f;

        var x = pad;
        var y = pad;

        GUI.Label(new Rect(x, y, w, 36f), "Welcome to Computer Graphics Virtual Lab");
        y += 42f;

        GUI.Label(new Rect(x, y, w, 7f * h), GetIntroText());
        y += 7f * h + 8f;

        if (audioEnabled && introClip == null)
        {
            GUI.Label(new Rect(x, y, w, h), "Audio: missing clip (put mp3 under Assets/Resources/LineDrawingAlgorithm/ with name 'Welcome Intro')");
        }
        else
        {
            GUI.Label(new Rect(x, y, w, h), audioEnabled ? "Audio: on" : "Audio: off");
        }
        y += h + 6f;

        audioEnabled = GUI.Toggle(new Rect(x, y, 180f, 34f), audioEnabled, "Audio On");

        if (GUI.Button(new Rect(x + 200f, y, 140f, 34f), "Play"))
        {
            PlayIntro();
        }

        if (GUI.Button(new Rect(x + 352f, y, 180f, 34f), "Continue"))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void PlayIntro()
    {
        if (!audioEnabled)
        {
            return;
        }

        if (_audioSource == null)
        {
            return;
        }

        if (introClip == null)
        {
            return;
        }

        _audioSource.Stop();
        _audioSource.clip = introClip;
        _audioSource.Play();
    }

    private static string GetIntroText()
    {
        return
            "In this virtual lab, you will learn core Computer Graphics algorithms by doing.\n" +
            "You will select an algorithm, choose points directly on the canvas, and watch how the algorithm plots pixels step by step.\n\n" +
            "Modules included in this lab:\n" +
            "- Line Drawing (DDA, Bresenham, Bresenham All Slopes)\n" +
            "- Circle Drawing (Midpoint Circle)\n" +
            "- Line Clipping (Cohen–Sutherland, Liang–Barsky)\n" +
            "- Polygon Fill (Scanline Fill)\n\n" +
            "Tip: In every module, press Esc to go back.";
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
