using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small overlay: turn in quest materials vs. chat. Auto-built at runtime if missing.
/// </summary>
public class ForgeQuestChoiceUI : MonoBehaviour
{
    public static ForgeQuestChoiceUI Instance { get; private set; }

    public int LastChoice { get; private set; } = -1;

    public static bool IsBlockingGameplay { get; private set; }

    private GameObject _panel;
    private Button _btnA;
    private Button _btnB;
    private TMP_Text _labelA;
    private TMP_Text _labelB;
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

    public IEnumerator RunRoutine(string buttonAText, string buttonBText)
    {
        BuildUiIfNeeded();
        LastChoice = -1;
        _picked = null;

        if (_labelA != null)
            _labelA.text = buttonAText;
        if (_labelB != null)
            _labelB.text = buttonBText;

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

    private void BuildUiIfNeeded()
    {
        if (_panel != null)
            return;

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
        var rt = _panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520f, 200f);
        rt.anchoredPosition = new Vector2(0f, 40f);

        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.15f, 0.96f);

        _btnA = CreateButton(_panel.transform, "BtnTurnIn", new Vector2(0f, 40f), out _labelA);
        _btnB = CreateButton(_panel.transform, "BtnChat", new Vector2(0f, -50f), out _labelB);

        _btnA.onClick.AddListener(() => { _picked = 0; });
        _btnB.onClick.AddListener(() => { _picked = 1; });

        _panel.SetActive(false);
    }

    private static Button CreateButton(Transform parent, string name, Vector2 anchoredPos, out TMP_Text tmp)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(460f, 56f);
        rt.anchoredPosition = anchoredPos;

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
