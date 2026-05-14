using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen pause overlay (Esc / P). Pauses scaled time, except this script still receives Update.
/// Auto-creates at runtime; optionally place a duplicate in a scene to configure <see cref="mainMenuSceneName"/> in the inspector.
/// </summary>
[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    /// <summary>True while the pause overlay is visible and scaled time is frozen.</summary>
    public static bool IsOpen { get; private set; }

    [Tooltip("Scene name exactly as in File > Build Settings (e.g. MainMenu). Leave empty to disable the main menu button until you add a menu scene.")]
    [SerializeField] private string mainMenuSceneName;

    private GameObject _root;
    private Button _mainMenuButton;
    private TMP_Text _mainMenuLabel;
    private float _timeScaleBeforePause;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<PauseMenuController>() != null)
            return;

        var go = new GameObject($"[{nameof(PauseMenuController)}]");
        go.AddComponent<PauseMenuController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (IsOpen)
            Time.timeScale = _timeScaleBeforePause;

        Instance = null;
        IsOpen = false;
    }

    private void Update()
    {
        if (!WasTogglePausePressedThisFrame())
            return;

        if (IsOpen)
            Resume();
        else
            Open();
    }

    private static bool WasTogglePausePressedThisFrame()
    {
        var k = Keyboard.current;
        return k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
    }

    private void Open()
    {
        if (IsOpen)
            return;

        IsOpen = true;
        _timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        RefreshMainMenuButton();
        _root.SetActive(true);
    }

    private void Resume()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        Time.timeScale = _timeScaleBeforePause;
        _root.SetActive(false);
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        IsOpen = false;
        _root.SetActive(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void LoadMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning($"{nameof(PauseMenuController)}: Assign {nameof(mainMenuSceneName)} (and add that scene to Build Settings) before using main menu.", this);
            return;
        }

        Time.timeScale = 1f;
        IsOpen = false;
        _root.SetActive(false);
        SceneManager.LoadScene(mainMenuSceneName.Trim());
    }

    private static void QuitApplication()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Assign main-menu scene at runtime when using the auto-created pause object (no inspector).</summary>
    public static void SetMainMenuScene(string sceneName)
    {
        if (Instance == null)
            return;

        Instance.mainMenuSceneName = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
        Instance.RefreshMainMenuButton();
    }

    private void RefreshMainMenuButton()
    {
        if (_mainMenuButton == null || _mainMenuLabel == null)
            return;

        var configured = !string.IsNullOrWhiteSpace(mainMenuSceneName);
        _mainMenuButton.interactable = configured;
        _mainMenuLabel.text = configured ? "Main menu" : "Main menu (set scene)";
        _mainMenuLabel.color = configured ? Color.white : new Color(0.65f, 0.65f, 0.7f, 1f);
    }

    private void BuildUi()
    {
        if (_root != null)
            return;

        var canvasGo = new GameObject("PauseMenuCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("PauseRoot", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        var rootRt = _root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var dim = _root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(_root.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(440f, 420f);

        var panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.07f, 0.12f, 0.98f);

        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(panel.transform, false);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        if (font != null)
            title.font = font;
        title.text = "Paused";
        title.fontSize = 36f;
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(1f, 0.93f, 0.6f, 1f);
        title.alignment = TextAlignmentOptions.Center;
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);
        titleRt.sizeDelta = new Vector2(-32f, 48f);

        var hint = CreateTmpLabel(panel.transform, "Esc / P — toggle", font, 16f, new Color(0.75f, 0.76f, 0.8f, 1f));
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 1f);
        hintRt.anchorMax = new Vector2(1f, 1f);
        hintRt.pivot = new Vector2(0.5f, 1f);
        hintRt.anchoredPosition = new Vector2(0f, -76f);
        hintRt.sizeDelta = new Vector2(-32f, 28f);
        hint.alignment = TextAlignmentOptions.Center;

        var y = 24f;
        var spacing = 62f;
        AddMenuButton(panel.transform, "Continue", font, y, Resume);
        y -= spacing;
        AddMenuButton(panel.transform, "Restart", font, y, RestartCurrentScene);
        y -= spacing;
        var mm = AddMenuButton(panel.transform, "Main menu", font, y, LoadMainMenu);
        _mainMenuButton = mm.Item1;
        _mainMenuLabel = mm.Item2;
        y -= spacing;
        AddMenuButton(panel.transform, "Quit game", font, y, QuitApplication);

        RefreshMainMenuButton();
        _root.SetActive(false);
    }

    private static TextMeshProUGUI CreateTmpLabel(Transform parent, string text, TMP_FontAsset font, float size, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static (Button, TMP_Text) AddMenuButton(Transform parent, string label, TMP_FontAsset font, float y, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Btn_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 52f);
        rt.anchoredPosition = new Vector2(0f, y);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.22f, 0.32f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var tmpGo = new GameObject("Text", typeof(RectTransform));
        tmpGo.transform.SetParent(go.transform, false);
        var tmp = tmpGo.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 22f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var trt = tmp.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return (btn, tmp);
    }
}
