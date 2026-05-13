using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small overlay: forge quest choices (turn-in, chat, end day, etc.). Auto-built at runtime if missing.
/// </summary>
public class ForgeQuestChoiceUI : MonoBehaviour
{
    public static ForgeQuestChoiceUI Instance { get; private set; }

    public int LastChoice { get; private set; } = -1;

    public static bool IsBlockingGameplay { get; private set; }

    private GameObject _panel;
    private RectTransform _panelRect;
    private Button _btnA;
    private Button _btnB;
    private Button _btnC;
    private RectTransform _btnARect;
    private RectTransform _btnBRect;
    private RectTransform _btnCRect;
    private TMP_Text _labelA;
    private TMP_Text _labelB;
    private TMP_Text _labelC;
    private int? _picked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUiIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static ForgeQuestChoiceUI GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<ForgeQuestChoiceUI>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(ForgeQuestChoiceUI)}]");
        return go.AddComponent<ForgeQuestChoiceUI>();
    }

    /// <param name="buttonCText">If null or empty, only two buttons are shown.</param>
    public IEnumerator RunRoutine(string buttonAText, string buttonBText, string buttonCText = null)
    {
        BuildUiIfNeeded();
        LastChoice = -1;
        _picked = null;

        var three = !string.IsNullOrEmpty(buttonCText);

        if (_labelA != null)
            _labelA.text = buttonAText;
        if (_labelB != null)
            _labelB.text = buttonBText;
        if (_labelC != null)
            _labelC.text = three && buttonCText != null ? buttonCText : string.Empty;

        ApplyChoiceLayout(three);

        IsBlockingGameplay = true;

        try
        {
            if (_panel != null)
                _panel.SetActive(true);

            yield return new WaitUntil(() => _picked.HasValue);

            LastChoice = _picked.Value;
            _picked = null;

            if (_panel != null)
                _panel.SetActive(false);
        }
        finally
        {
            IsBlockingGameplay = false;
        }
    }

    private void ApplyChoiceLayout(bool threeButtons)
    {
        if (_panelRect == null)
            return;

        _panelRect.sizeDelta = new Vector2(520f, threeButtons ? 300f : 200f);

        if (_btnC != null)
            _btnC.gameObject.SetActive(threeButtons);

        if (_btnARect != null)
            _btnARect.anchoredPosition = threeButtons ? new Vector2(0f, 92f) : new Vector2(0f, 40f);
        if (_btnBRect != null)
            _btnBRect.anchoredPosition = threeButtons ? new Vector2(0f, 0f) : new Vector2(0f, -50f);
        if (_btnCRect != null && threeButtons)
            _btnCRect.anchoredPosition = new Vector2(0f, -92f);
    }

    private void BuildUiIfNeeded()
    {
        if (_panel != null && _btnC != null)
            return;

        if (_panel != null)
        {
            var canvasRoot = _panel.transform.parent != null ? _panel.transform.parent.gameObject : _panel;
            Destroy(canvasRoot);
            _panel = null;
            _panelRect = null;
            _btnA = null;
            _btnB = null;
            _btnC = null;
            _btnARect = null;
            _btnBRect = null;
            _btnCRect = null;
            _labelA = null;
            _labelB = null;
            _labelC = null;
        }

        var canvasGo = new GameObject("ForgeQuestChoiceCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        _panel = new GameObject("ChoicePanel", typeof(RectTransform));
        _panel.transform.SetParent(canvasGo.transform, false);
        _panelRect = _panel.GetComponent<RectTransform>();
        _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRect.pivot = new Vector2(0.5f, 0.5f);
        _panelRect.sizeDelta = new Vector2(520f, 200f);
        _panelRect.anchoredPosition = new Vector2(0f, 40f);

        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.15f, 0.96f);

        _btnA = CreateButton(_panel.transform, "BtnA", new Vector2(0f, 40f), out _labelA, out _btnARect);
        _btnB = CreateButton(_panel.transform, "BtnB", new Vector2(0f, -50f), out _labelB, out _btnBRect);
        _btnC = CreateButton(_panel.transform, "BtnC", new Vector2(0f, -92f), out _labelC, out _btnCRect);
        _btnC.gameObject.SetActive(false);

        _btnA.onClick.AddListener(() => { _picked = 0; });
        _btnB.onClick.AddListener(() => { _picked = 1; });
        _btnC.onClick.AddListener(() => { _picked = 2; });

        _panel.SetActive(false);
    }

    private static Button CreateButton(Transform parent, string name, Vector2 anchoredPos, out TMP_Text tmp, out RectTransform buttonRt)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        buttonRt = go.GetComponent<RectTransform>();
        buttonRt.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRt.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRt.pivot = new Vector2(0.5f, 0.5f);
        buttonRt.sizeDelta = new Vector2(460f, 56f);
        buttonRt.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.22f, 0.32f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 22f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var trt = tmp.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null)
            tmp.font = font;

        return btn;
    }
}
