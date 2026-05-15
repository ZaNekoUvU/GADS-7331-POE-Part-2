using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// FF-style dialogue overlay (speaker + line). Leave references empty for automatic runtime layout.
/// Advance with Interact (same binding as NPCs / blacksmith). Player movement and NPC wander pause while open.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class SimpleRpgDialogueUI : MonoBehaviour
{
    public static SimpleRpgDialogueUI Instance { get; private set; }

    /// <summary>True while a line is on screen (including waiting for advance).</summary>
    public static bool IsDialogueOpen { get; private set; }

    /// <summary>
    /// Set to <see cref="Time.frameCount"/> when the player advances/closes dialogue.
    /// Other interact handlers should ignore <c>WasPressedThisFrame</c> on this frame to avoid double-firing.
    /// </summary>
    public static int InteractConsumedByDialogueFrame { get; private set; } = -1;

    [Tooltip("Optional. If null, falls back to Keyboard E (same as other scripts).")]
    [SerializeField] private InputActionReference advanceAction;

    [Tooltip("Leave empty for automatic runtime layout.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Leave empty for automatic runtime layout.")]
    [SerializeField] private TMP_Text speakerLabel;

    [Tooltip("Leave empty for automatic runtime layout.")]
    [SerializeField] private TMP_Text lineLabel;

    private bool _advanceAllowed = true;

    private void Reset()
    {
        EnsureCanvasStack();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(SimpleRpgDialogueUI)}: Duplicate instance on '{name}' — destroying.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureCanvasStack();

        if (panelRoot == null || speakerLabel == null || lineLabel == null)
            BuildRuntimeUiIfNeeded();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (advanceAction != null)
            advanceAction.action.Enable();
    }

    private void OnDisable()
    {
        if (advanceAction != null)
            advanceAction.action.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsDialogueOpen = false;
            InteractConsumedByDialogueFrame = -1;
        }
    }

    /// <summary>Creates a dialogue overlay in the active scene if none exists yet.</summary>
    public static SimpleRpgDialogueUI GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<SimpleRpgDialogueUI>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(SimpleRpgDialogueUI)}]");
        go.AddComponent<SimpleRpgDialogueUI>();
        return Instance;
    }

    /// <summary>Dialogue the player can dismiss immediately with advance.</summary>
    public void Show(string speaker, string line)
    {
        ShowInternal(speaker, line, advanceInitiallyAllowed: true);
    }

    /// <summary>Show dialogue but ignore advance until <see cref="SetDialogueLineAndAllowAdvance"/> (e.g. waiting on the AI gateway).</summary>
    public void ShowAwaitingLine(string speaker, string placeholderLine)
    {
        ShowInternal(speaker, placeholderLine, advanceInitiallyAllowed: false);
    }

    /// <summary>Replace body text and allow the player to advance (after model generation).</summary>
    public void SetDialogueLineAndAllowAdvance(string line)
    {
        if (lineLabel != null)
            lineLabel.text = line;
        _advanceAllowed = true;
    }

    /// <summary>Hide overlay and unlock gameplay (error / scene teardown).</summary>
    public void AbortDialogue()
    {
        StopAllCoroutines();
        if (panelRoot != null)
            panelRoot.SetActive(false);
        IsDialogueOpen = false;
        _advanceAllowed = true;
    }

    private void ShowInternal(string speaker, string line, bool advanceInitiallyAllowed)
    {
        if (panelRoot == null || speakerLabel == null || lineLabel == null)
            BuildRuntimeUiIfNeeded();

        if (panelRoot == null || speakerLabel == null || lineLabel == null)
        {
            Debug.LogError(
                $"{nameof(SimpleRpgDialogueUI)}: Cannot show dialogue — UI not built. Assign panel and labels or use full auto layout.",
                this);
            IsDialogueOpen = false;
            return;
        }

        StopAllCoroutines();
        IsDialogueOpen = true;
        _advanceAllowed = advanceInitiallyAllowed;
        StartCoroutine(ShowRoutine(speaker, line));
    }

    private IEnumerator ShowRoutine(string speaker, string line)
    {
        if (speakerLabel != null)
        {
            var hasSpeaker = !string.IsNullOrEmpty(speaker);
            speakerLabel.gameObject.SetActive(hasSpeaker);
            speakerLabel.text = speaker;
        }

        if (lineLabel != null)
            lineLabel.text = line;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        yield return null;

        while (true)
        {
            yield return null;
            if (_advanceAllowed && WasAdvancePressedThisFrame())
                break;
        }

        InteractConsumedByDialogueFrame = Time.frameCount;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        IsDialogueOpen = false;
        _advanceAllowed = true;
    }

    private bool WasAdvancePressedThisFrame()
    {
        if (advanceAction != null && advanceAction.action != null)
            return advanceAction.action.WasPressedThisFrame();

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private void EnsureCanvasStack()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        if (GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    private void BuildRuntimeUiIfNeeded()
    {
        if (panelRoot != null && speakerLabel != null && lineLabel != null)
            return;

        var fullAuto = panelRoot == null && speakerLabel == null && lineLabel == null;
        if (!fullAuto)
        {
            Debug.LogError(
                $"{nameof(SimpleRpgDialogueUI)}: Assign panel + both TMP labels in the Inspector, or leave all three empty for automatic UI.",
                this);
            return;
        }

        for (var i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        var panelRt = CreatePanel(out var panelImg);
        panelRoot = panelRt.gameObject;
        panelImg.color = new Color(0.06f, 0.06f, 0.14f, 0.94f);

        speakerLabel = CreateTmpText("Speaker", panelRt, font, 26f, new Color(1f, 0.92f, 0.55f, 1f));
        var speakerRt = speakerLabel.rectTransform;
        speakerRt.anchorMin = new Vector2(0f, 1f);
        speakerRt.anchorMax = new Vector2(1f, 1f);
        speakerRt.pivot = new Vector2(0.5f, 1f);
        speakerRt.anchoredPosition = new Vector2(0f, -16f);
        speakerRt.sizeDelta = new Vector2(-48f, 36f);
        speakerLabel.fontStyle = FontStyles.Bold;
        speakerLabel.alignment = TextAlignmentOptions.TopLeft;

        lineLabel = CreateTmpText("Line", panelRt, font, 24f, Color.white);
        var lineRt = lineLabel.rectTransform;
        lineRt.anchorMin = new Vector2(0f, 0f);
        lineRt.anchorMax = new Vector2(1f, 1f);
        lineRt.offsetMin = new Vector2(24f, 24f);
        lineRt.offsetMax = new Vector2(-24f, -56f);
        lineLabel.alignment = TextAlignmentOptions.TopLeft;
        lineLabel.textWrappingMode = TextWrappingModes.Normal;
    }

    private RectTransform CreatePanel(out Image image)
    {
        var go = new GameObject("DialoguePanel", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 32f);
        rt.sizeDelta = new Vector2(0f, 220f);

        image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return rt;
    }

    private static TextMeshProUGUI CreateTmpText(string name, RectTransform parent, TMP_FontAsset font, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.text = string.Empty;

        var rt = tmp.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return tmp;
    }
}
